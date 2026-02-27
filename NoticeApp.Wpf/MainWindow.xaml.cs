using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NoticeApp.Wpf.Models;
using NoticeApp.Wpf.Services;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace NoticeApp.Wpf;

public partial class MainWindow : Window
{
    private enum CloseBehaviorOption
    {
        MinimizeToTray = 0,
        ExitApplication = 1,
    }

    private static readonly System.Windows.Media.Brush ActiveNavBackground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E6F0FF"));
    private static readonly System.Windows.Media.Brush ActiveNavForeground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0066DE"));
    private static readonly System.Windows.Media.Brush InactiveNavForeground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
    private static readonly System.Windows.Media.Brush TransparentBrush = System.Windows.Media.Brushes.Transparent;

    private readonly ReminderStore _store = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly List<Reminder> _reminders = [];
    private readonly ObservableCollection<ReminderListItem> _items = [];
    private readonly DispatcherTimer _schedulerTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Forms.NotifyIcon _notifyIcon = new();
    private readonly Forms.ContextMenuStrip _trayMenu = new();

    private bool _isExiting;
    private bool _startupHidden;
    private bool _trayHintShown;
    private bool _changingStartupCheckBox;
    private bool _changingNotificationPositionComboBox;
    private IntPtr _trayIconHandle = IntPtr.Zero;
    private CloseBehaviorOption _closeBehavior = CloseBehaviorOption.MinimizeToTray;
    private AppSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();

        ReminderCardItemsControl.ItemsSource = _items;
        DataPathTextBox.Text = _store.StorageFilePath;
        LoadAppSettings();

        SetupTray();
        InitializeReminders();
        UpdateStartupStatus();
        SetActivePage(isHomePage: true);

        _schedulerTimer.Tick += SchedulerTimer_Tick;
        _schedulerTimer.Start();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_startupHidden)
        {
            return;
        }

        _startupHidden = true;
        Dispatcher.BeginInvoke(HideToTray, DispatcherPriority.Background);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (e.Cancel)
        {
            base.OnClosing(e);
            return;
        }

        if (!_isExiting)
        {
            if (_closeBehavior == CloseBehaviorOption.MinimizeToTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            _isExiting = true;
        }

        SaveReminders();
        _notifyIcon.Visible = false;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _schedulerTimer.Stop();
        _notifyIcon.Dispose();
        _trayMenu.Dispose();

        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }

        base.OnClosed(e);
    }

    private void SetupTray()
    {
        _trayMenu.Items.Add("打开主界面", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        _trayMenu.Items.Add("退出程序", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _notifyIcon.Text = "沙漏时钟提醒";
        _notifyIcon.Icon = BuildTrayIcon();
        _notifyIcon.ContextMenuStrip = _trayMenu;
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private void InitializeReminders()
    {
        var now = DateTime.Now;
        _reminders.Clear();
        foreach (var loadedReminder in _store.Load())
        {
            if (loadedReminder is null)
            {
                continue;
            }

            var reminder = SanitizeReminder(loadedReminder);
            ReminderScheduler.Normalize(reminder, now);
            _reminders.Add(reminder);
        }

        SaveReminders();
        RefreshReminderList();
    }

    private void RefreshReminderList(Guid? selectedId = null)
    {
        var sorted = _reminders
            .OrderBy(item => item.IsCompleted)
            .ThenBy(item => item.NextTriggerAt ?? DateTime.MaxValue)
            .ThenBy(item => item.Description)
            .ToList();

        _items.Clear();
        foreach (var reminder in sorted)
        {
            _items.Add(new ReminderListItem { Reminder = reminder });
        }

        StatsTextBlock.Text = $"共 {_reminders.Count} 条提醒";
        EmptyStateTextBlock.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _ = selectedId;
    }

    private Reminder? FindReminderById(Guid id)
    {
        return _reminders.FirstOrDefault(item => item.Id == id);
    }

    private static Guid? ParseReminderId(object? tag)
    {
        if (tag is Guid guid)
        {
            return guid;
        }

        if (tag is string text && Guid.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private void SaveReminders()
    {
        _store.Save(_reminders);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ReminderEditorWindow { Owner = this };
        if (editor.ShowDialog() != true || editor.ResultReminder is null)
        {
            return;
        }

        var reminder = editor.ResultReminder;
        reminder.Id = Guid.NewGuid();
        reminder.CreatedAt = DateTime.Now;
        reminder.UpdatedAt = DateTime.Now;
        ReminderScheduler.Normalize(reminder, DateTime.Now);
        _reminders.Add(reminder);
        SaveReminders();
        RefreshReminderList(reminder.Id);
    }

    private void CardEditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var id = ParseReminderId(element.Tag);
        if (id is null)
        {
            return;
        }

        var selected = FindReminderById(id.Value);
        if (selected is null)
        {
            return;
        }

        var editor = new ReminderEditorWindow(selected.Clone()) { Owner = this };
        if (editor.ShowDialog() != true || editor.ResultReminder is null)
        {
            return;
        }

        var index = _reminders.FindIndex(item => item.Id == selected.Id);
        if (index < 0)
        {
            return;
        }

        var edited = editor.ResultReminder;
        edited.Id = selected.Id;
        edited.CreatedAt = selected.CreatedAt;
        edited.UpdatedAt = DateTime.Now;
        ReminderScheduler.Normalize(edited, DateTime.Now);
        _reminders[index] = edited;
        SaveReminders();
        RefreshReminderList(edited.Id);
    }

    private void CardDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var id = ParseReminderId(element.Tag);
        if (id is null)
        {
            return;
        }

        var selected = FindReminderById(id.Value);
        if (selected is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            $"删除提醒：{selected.Description} ?",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _reminders.RemoveAll(item => item.Id == selected.Id);
        SaveReminders();
        RefreshReminderList();
    }

    private void CardEnableCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        UpdateReminderEnabledFromCheckBox(sender, true);
    }

    private void CardEnableCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateReminderEnabledFromCheckBox(sender, false);
    }

    private void UpdateReminderEnabledFromCheckBox(object sender, bool enabled)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var id = ParseReminderId(element.Tag);
        if (id is null)
        {
            return;
        }

        var reminder = FindReminderById(id.Value);
        if (reminder is null)
        {
            return;
        }

        reminder.IsEnabled = enabled;
        if (enabled)
        {
            reminder.IsCompleted = false;
        }

        reminder.UpdatedAt = DateTime.Now;
        ReminderScheduler.Normalize(reminder, DateTime.Now);
        SaveReminders();
        RefreshReminderList(reminder.Id);
    }

    private void SchedulerTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var now = DateTime.Now;
            var dueReminders = _reminders
                .Where(item => item.IsEnabled && !item.IsCompleted && item.NextTriggerAt is not null && item.NextTriggerAt <= now)
                .ToList();

            if (dueReminders.Count == 0)
            {
                return;
            }

            foreach (var reminder in dueReminders)
            {
                var triggerAt = reminder.NextTriggerAt ?? now;
                ShowReminderPopup(reminder.Description, triggerAt);
                ReminderScheduler.MarkTriggered(reminder, now);
            }

            SaveReminders();
            RefreshReminderList();
        }
        catch (Exception exception)
        {
            ErrorLogger.Log(exception, "MainWindow.SchedulerTimer_Tick");
        }
    }

    private void ShowReminderPopup(string message, DateTime triggerAt)
    {
        SystemSounds.Asterisk.Play();
        var popup = new NotificationWindow(message, triggerAt, _settings.NotificationPlacement);
        popup.Show();
    }

    private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        ApplyStartupSetting(true);
    }

    private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        ApplyStartupSetting(false);
    }

    private void ApplyStartupSetting(bool enabled)
    {
        if (_changingStartupCheckBox)
        {
            return;
        }

        if (StartupManager.SetAutoStartEnabled(enabled))
        {
            UpdateStartupStatus();
            return;
        }

        System.Windows.MessageBox.Show(
            this,
            "无法修改开机自启动设置，请检查权限后重试。",
            "设置失败",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        UpdateStartupStatus();
    }

    private void UpdateStartupStatus()
    {
        _changingStartupCheckBox = true;
        AutoStartCheckBox.IsChecked = StartupManager.IsAutoStartEnabled();
        _changingStartupCheckBox = false;
    }

    private void CloseBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _closeBehavior = CloseBehaviorComboBox.SelectedIndex == 1
            ? CloseBehaviorOption.ExitApplication
            : CloseBehaviorOption.MinimizeToTray;
    }

    private void NotificationPositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingNotificationPositionComboBox)
        {
            return;
        }

        _settings.NotificationPlacement = NotificationPositionComboBox.SelectedIndex switch
        {
            1 => NotificationPlacement.TopRight,
            2 => NotificationPlacement.Center,
            _ => NotificationPlacement.BottomRight,
        };
        _settingsStore.Save(_settings);
    }

    private void ReminderNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActivePage(isHomePage: true);
    }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActivePage(isHomePage: false);
    }

    private void SetActivePage(bool isHomePage)
    {
        HomePageGrid.Visibility = isHomePage ? Visibility.Visible : Visibility.Collapsed;
        SettingsPageGrid.Visibility = isHomePage ? Visibility.Collapsed : Visibility.Visible;
        SetNavButtonState(ReminderNavButton, isHomePage);
        SetNavButtonState(SettingsNavButton, !isHomePage);
    }

    private static void SetNavButtonState(System.Windows.Controls.Button button, bool active)
    {
        button.Background = active ? ActiveNavBackground : TransparentBrush;
        button.Foreground = active ? ActiveNavForeground : InactiveNavForeground;
        button.BorderBrush = active ? ActiveNavForeground : TransparentBrush;
        button.BorderThickness = active ? new Thickness(0, 0, 3, 0) : new Thickness(0);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        WindowState = WindowState.Normal;
        if (_trayHintShown)
        {
            return;
        }

        _trayHintShown = true;
        _notifyIcon.ShowBalloonTip(1500, "沙漏时钟提醒", "应用正在系统托盘运行。", Forms.ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    private Drawing.Icon BuildTrayIcon()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                using var exeIcon = Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (exeIcon is not null)
                {
                    return (Drawing.Icon)exeIcon.Clone();
                }
            }
        }
        catch
        {
            // Fallback to bundled PNG.
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon-bell.png");
        if (!File.Exists(iconPath))
        {
            return Drawing.SystemIcons.Information;
        }

        using var source = Drawing.Image.FromFile(iconPath);
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(Drawing.Color.Transparent);
            graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, new Drawing.Rectangle(4, 4, 24, 24));
        }

        _trayIconHandle = bitmap.GetHicon();
        return Drawing.Icon.FromHandle(_trayIconHandle);
    }

    private static Reminder SanitizeReminder(Reminder reminder)
    {
        reminder.Description ??= string.Empty;
        reminder.WeeklyDays ??= [];
        reminder.MonthlyDay = Math.Clamp(reminder.MonthlyDay, 1, 31);
        reminder.UpdatedAt = reminder.UpdatedAt == default ? DateTime.Now : reminder.UpdatedAt;
        reminder.CreatedAt = reminder.CreatedAt == default ? reminder.UpdatedAt : reminder.CreatedAt;
        return reminder;
    }

    private void LoadAppSettings()
    {
        _settings = _settingsStore.Load();

        _changingNotificationPositionComboBox = true;
        NotificationPositionComboBox.SelectedIndex = _settings.NotificationPlacement switch
        {
            NotificationPlacement.TopRight => 1,
            NotificationPlacement.Center => 2,
            _ => 0,
        };
        _changingNotificationPositionComboBox = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source &&
            FindVisualParent<System.Windows.Controls.Button>(source) is not null)
        {
            return;
        }

        DragMove();
    }

    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T matched)
            {
                return matched;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
