using System.Windows;
using System.Windows.Media.Animation;
using NoticeApp.Wpf.Models;

namespace NoticeApp.Wpf;

public partial class NotificationWindow : Window
{
    private static readonly object SlotLock = new();
    private static readonly Dictionary<NotificationPlacement, HashSet<int>> ActiveSlots = new()
    {
        [NotificationPlacement.BottomRight] = [],
        [NotificationPlacement.TopRight] = [],
        [NotificationPlacement.Center] = [],
    };

    private readonly NotificationPlacement _placement;
    private int _slot = -1;

    public NotificationWindow(string message, DateTime triggerAt, NotificationPlacement placement = NotificationPlacement.BottomRight)
    {
        InitializeComponent();

        _placement = placement;
        MessageTextBlock.Text = string.IsNullOrWhiteSpace(message) ? "提醒事项" : message;
        TimeTextBlock.Text = triggerAt.ToString("yyyy-MM-dd HH:mm:ss");

        Loaded += NotificationWindow_Loaded;
    }

    protected override void OnClosed(EventArgs e)
    {
        ReleaseSlot();
        base.OnClosed(e);
    }

    private void NotificationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AllocateSlot();
        PositionWindow();
    }

    private void PositionWindow()
    {
        var area = SystemParameters.WorkArea;
        var targetLeft = area.Right - Width - 14;

        switch (_placement)
        {
            case NotificationPlacement.TopRight:
                Left = area.Right + 8;
                Top = area.Top + 14 + (_slot * (Height + 10));
                SlideTo(targetLeft, 220);
                break;

            case NotificationPlacement.Center:
                Left = area.Left + ((area.Width - Width) / 2);
                Top = area.Top + ((area.Height - Height) / 2) + (_slot * (Height + 10));
                Opacity = 0;
                FadeTo(1, 180);
                break;

            default:
                Left = area.Right + 8;
                Top = area.Bottom - Height - 14 - (_slot * (Height + 10));
                SlideTo(targetLeft, 220);
                break;
        }
    }

    private void SlideTo(double destination, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = destination,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        BeginAnimation(LeftProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void FadeTo(double destination, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = destination,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        BeginAnimation(OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AllocateSlot()
    {
        lock (SlotLock)
        {
            var slots = ActiveSlots[_placement];
            var index = 0;
            while (slots.Contains(index))
            {
                index++;
            }

            slots.Add(index);
            _slot = index;
        }
    }

    private void ReleaseSlot()
    {
        lock (SlotLock)
        {
            if (_slot < 0)
            {
                return;
            }

            if (ActiveSlots.TryGetValue(_placement, out var slots))
            {
                slots.Remove(_slot);
            }

            _slot = -1;
        }
    }

    private void AcknowledgeButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
