namespace NoticeApp.Wpf.Models;

public enum ReminderMode
{
    OneTime = 0,
    Recurring = 1,
}

public enum RecurrenceType
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
}

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Description { get; set; } = string.Empty;

    public ReminderMode Mode { get; set; }

    public DateTime? OneTimeAt { get; set; }

    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.Daily;

    public List<DayOfWeek> WeeklyDays { get; set; } = [];

    public int MonthlyDay { get; set; } = 1;

    public TimeSpan TriggerTime { get; set; } = new(9, 0, 0);

    public bool IsEnabled { get; set; } = true;

    public bool IsCompleted { get; set; }

    public DateTime? NextTriggerAt { get; set; }

    public DateTime? LastTriggeredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Reminder Clone()
    {
        return new Reminder
        {
            Id = Id,
            Description = Description,
            Mode = Mode,
            OneTimeAt = OneTimeAt,
            RecurrenceType = RecurrenceType,
            WeeklyDays = [.. WeeklyDays],
            MonthlyDay = MonthlyDay,
            TriggerTime = TriggerTime,
            IsEnabled = IsEnabled,
            IsCompleted = IsCompleted,
            NextTriggerAt = NextTriggerAt,
            LastTriggeredAt = LastTriggeredAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }
}
