namespace NoticeApp.Wpf.Models;

public sealed class ReminderListItem
{
    public required Reminder Reminder { get; init; }

    public Guid Id => Reminder.Id;

    public bool IsEnabled => Reminder.IsEnabled;

    public string Description => string.IsNullOrWhiteSpace(Reminder.Description) ? "未命名提醒" : Reminder.Description;

    public string ModeText => Reminder.Mode == ReminderMode.OneTime ? "一次性" : "周期性";

    public string RuleText
    {
        get
        {
            if (Reminder.Mode == ReminderMode.OneTime)
            {
                return Reminder.OneTimeAt is null
                    ? "一次性 · 未设置时间"
                    : $"一次性 · {Reminder.OneTimeAt:yyyy-MM-dd HH:mm:ss}";
            }

            return Reminder.RecurrenceType switch
            {
                RecurrenceType.Daily => $"每天 · {DateTime.Today.Add(Reminder.TriggerTime):HH:mm:ss}",
                RecurrenceType.Weekly => $"每周({BuildWeekText(Reminder.WeeklyDays)}) · {DateTime.Today.Add(Reminder.TriggerTime):HH:mm:ss}",
                RecurrenceType.Monthly => $"每月{Reminder.MonthlyDay}日 · {DateTime.Today.Add(Reminder.TriggerTime):HH:mm:ss}",
                _ => "周期提醒",
            };
        }
    }

    public string StateText
    {
        get
        {
            if (Reminder.IsCompleted)
            {
                return "已完成";
            }

            return Reminder.IsEnabled ? "启用" : "禁用";
        }
    }

    public string MetaText => $"{RuleText} · {StateText}";

    private static string BuildWeekText(IEnumerable<DayOfWeek>? weeklyDays)
    {
        if (weeklyDays is null)
        {
            return "未指定";
        }

        var mapping = new Dictionary<DayOfWeek, string>
        {
            [DayOfWeek.Monday] = "一",
            [DayOfWeek.Tuesday] = "二",
            [DayOfWeek.Wednesday] = "三",
            [DayOfWeek.Thursday] = "四",
            [DayOfWeek.Friday] = "五",
            [DayOfWeek.Saturday] = "六",
            [DayOfWeek.Sunday] = "日",
        };

        var days = weeklyDays
            .Distinct()
            .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
            .Select(day => mapping.TryGetValue(day, out var value) ? value : day.ToString());

        var text = string.Join(",", days);
        return string.IsNullOrWhiteSpace(text) ? "未指定" : text;
    }
}
