using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoticeApp.Wpf.Models;
using NoticeApp.Wpf.Services;

namespace NoticeApp.Wpf;

public partial class ReminderEditorWindow : Window
{
    private readonly Reminder? _sourceReminder;

    public ReminderEditorWindow(Reminder? reminder = null)
    {
        _sourceReminder = reminder;
        InitializeComponent();
        InitializeMonthlyDays();
        LoadReminder();
    }

    public Reminder? ResultReminder { get; private set; }

    private void InitializeMonthlyDays()
    {
        for (var day = 1; day <= 31; day++)
        {
            MonthlyDayComboBox.Items.Add(day.ToString(CultureInfo.InvariantCulture));
        }
    }

    private void LoadReminder()
    {
        WindowTitleTextBlock.Text = _sourceReminder is null ? "添加提醒" : "编辑提醒";
        DescriptionTextBox.Text = _sourceReminder?.Description ?? string.Empty;

        ModeComboBox.SelectedIndex = _sourceReminder?.Mode == ReminderMode.Recurring ? 1 : 0;
        RecurrenceComboBox.SelectedIndex = _sourceReminder?.RecurrenceType switch
        {
            RecurrenceType.Weekly => 1,
            RecurrenceType.Monthly => 2,
            _ => 0,
        };

        var oneTimeValue = _sourceReminder?.OneTimeAt ?? DateTime.Now.AddMinutes(5);
        OneTimeDatePicker.SelectedDate = oneTimeValue.Date;
        OneTimeTimeTextBox.Text = oneTimeValue.ToString("HH:mm:ss");

        RecurringTimeTextBox.Text = DateTime.Today.Add(_sourceReminder?.TriggerTime ?? new TimeSpan(9, 0, 0))
            .ToString("HH:mm:ss");
        MonthlyDayComboBox.SelectedItem = Math.Clamp(_sourceReminder?.MonthlyDay ?? 1, 1, 31).ToString(CultureInfo.InvariantCulture);

        MondayCheckBox.IsChecked = false;
        TuesdayCheckBox.IsChecked = false;
        WednesdayCheckBox.IsChecked = false;
        ThursdayCheckBox.IsChecked = false;
        FridayCheckBox.IsChecked = false;
        SaturdayCheckBox.IsChecked = false;
        SundayCheckBox.IsChecked = false;

        if (_sourceReminder?.WeeklyDays is not null)
        {
            foreach (var day in _sourceReminder.WeeklyDays)
            {
                switch (day)
                {
                    case DayOfWeek.Monday:
                        MondayCheckBox.IsChecked = true;
                        break;
                    case DayOfWeek.Tuesday:
                        TuesdayCheckBox.IsChecked = true;
                        break;
                    case DayOfWeek.Wednesday:
                        WednesdayCheckBox.IsChecked = true;
                        break;
                    case DayOfWeek.Thursday:
                        ThursdayCheckBox.IsChecked = true;
                        break;
                    case DayOfWeek.Friday:
                        FridayCheckBox.IsChecked = true;
                        break;
                    case DayOfWeek.Saturday:
                        SaturdayCheckBox.IsChecked = true;
                        break;
                    case DayOfWeek.Sunday:
                        SundayCheckBox.IsChecked = true;
                        break;
                }
            }
        }

        UpdateModeVisibility();
        UpdateRecurrenceVisibility();
    }

    private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateModeVisibility();
    }

    private void RecurrenceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRecurrenceVisibility();
    }

    private void UpdateModeVisibility()
    {
        var isOneTime = ModeComboBox.SelectedIndex == 0;
        OneTimePanel.Visibility = isOneTime ? Visibility.Visible : Visibility.Collapsed;
        RecurringPanel.Visibility = isOneTime ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateRecurrenceVisibility()
    {
        var isWeekly = RecurrenceComboBox.SelectedIndex == 1;
        var isMonthly = RecurrenceComboBox.SelectedIndex == 2;
        WeekDaysPanel.Visibility = isWeekly || isMonthly ? Visibility.Visible : Visibility.Collapsed;
        MonthlyDayPanel.Visibility = isMonthly ? Visibility.Visible : Visibility.Collapsed;

        if (isMonthly)
        {
            MondayCheckBox.Visibility = Visibility.Collapsed;
            TuesdayCheckBox.Visibility = Visibility.Collapsed;
            WednesdayCheckBox.Visibility = Visibility.Collapsed;
            ThursdayCheckBox.Visibility = Visibility.Collapsed;
            FridayCheckBox.Visibility = Visibility.Collapsed;
            SaturdayCheckBox.Visibility = Visibility.Collapsed;
            SundayCheckBox.Visibility = Visibility.Collapsed;
            return;
        }

        MondayCheckBox.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
        TuesdayCheckBox.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
        WednesdayCheckBox.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
        ThursdayCheckBox.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
        FridayCheckBox.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
        SaturdayCheckBox.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
        SundayCheckBox.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var description = (DescriptionTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            System.Windows.MessageBox.Show(this, "请输入提醒内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reminder = _sourceReminder?.Clone() ?? new Reminder
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            IsEnabled = true,
        };

        reminder.Description = description;
        reminder.UpdatedAt = DateTime.Now;
        reminder.IsCompleted = false;

        if (ModeComboBox.SelectedIndex == 0)
        {
            if (OneTimeDatePicker.SelectedDate is null || !TryParseTime(OneTimeTimeTextBox.Text, out var oneTime))
            {
                System.Windows.MessageBox.Show(this, "请填写正确的一次性提醒时间（HH:mm:ss）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            reminder.Mode = ReminderMode.OneTime;
            reminder.OneTimeAt = OneTimeDatePicker.SelectedDate.Value.Date.Add(oneTime);
            reminder.RecurrenceType = RecurrenceType.Daily;
            reminder.WeeklyDays = [];
            reminder.MonthlyDay = 1;
            reminder.TriggerTime = oneTime;
        }
        else
        {
            if (!TryParseTime(RecurringTimeTextBox.Text, out var recurringTime))
            {
                System.Windows.MessageBox.Show(this, "请填写正确的触发时刻（HH:mm:ss）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            reminder.Mode = ReminderMode.Recurring;
            reminder.OneTimeAt = null;
            reminder.TriggerTime = recurringTime;
            reminder.RecurrenceType = RecurrenceComboBox.SelectedIndex switch
            {
                1 => RecurrenceType.Weekly,
                2 => RecurrenceType.Monthly,
                _ => RecurrenceType.Daily,
            };

            reminder.MonthlyDay = int.TryParse(MonthlyDayComboBox.SelectedItem?.ToString(), out var day) ? day : 1;
            reminder.WeeklyDays = BuildSelectedWeekdays();
            if (reminder.RecurrenceType == RecurrenceType.Weekly && reminder.WeeklyDays.Count == 0)
            {
                System.Windows.MessageBox.Show(this, "每周提醒至少选择一个星期几。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        ReminderScheduler.Normalize(reminder, DateTime.Now);
        ResultReminder = reminder;
        DialogResult = true;
        Close();
    }

    private List<DayOfWeek> BuildSelectedWeekdays()
    {
        var days = new List<DayOfWeek>();
        if (MondayCheckBox.IsChecked == true) days.Add(DayOfWeek.Monday);
        if (TuesdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Tuesday);
        if (WednesdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Wednesday);
        if (ThursdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Thursday);
        if (FridayCheckBox.IsChecked == true) days.Add(DayOfWeek.Friday);
        if (SaturdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Saturday);
        if (SundayCheckBox.IsChecked == true) days.Add(DayOfWeek.Sunday);
        return days;
    }

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out time))
        {
            return true;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time))
        {
            return true;
        }

        return false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
