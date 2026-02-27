using NoticeApp.Wpf.Models;

namespace NoticeApp.Wpf.Services;

public static class ReminderScheduler
{
    public static void Normalize(Reminder reminder, DateTime referenceTime)
    {
        EnsureDefaults(reminder);

        if (reminder.Mode == ReminderMode.OneTime)
        {
            NormalizeOneTime(reminder);
            return;
        }

        reminder.IsCompleted = false;
        if (!reminder.IsEnabled)
        {
            reminder.NextTriggerAt = null;
            return;
        }

        reminder.NextTriggerAt = CalculateNextRecurring(reminder, referenceTime);
    }

    public static void MarkTriggered(Reminder reminder, DateTime triggerTime)
    {
        EnsureDefaults(reminder);

        reminder.LastTriggeredAt = triggerTime;
        reminder.UpdatedAt = DateTime.Now;

        if (reminder.Mode == ReminderMode.OneTime)
        {
            reminder.IsCompleted = true;
            reminder.IsEnabled = false;
            reminder.NextTriggerAt = null;
            return;
        }

        reminder.NextTriggerAt = CalculateNextRecurring(reminder, triggerTime.AddSeconds(1));
    }

    private static void NormalizeOneTime(Reminder reminder)
    {
        if (reminder.OneTimeAt is null)
        {
            reminder.IsEnabled = false;
            reminder.IsCompleted = true;
            reminder.NextTriggerAt = null;
            return;
        }

        if (reminder.IsCompleted)
        {
            reminder.IsEnabled = false;
            reminder.NextTriggerAt = null;
            return;
        }

        reminder.NextTriggerAt = reminder.OneTimeAt;
    }

    private static DateTime? CalculateNextRecurring(Reminder reminder, DateTime fromTime)
    {
        return reminder.RecurrenceType switch
        {
            RecurrenceType.Daily => CalculateDaily(fromTime, reminder.TriggerTime),
            RecurrenceType.Weekly => CalculateWeekly(fromTime, reminder.TriggerTime, reminder.WeeklyDays ?? []),
            RecurrenceType.Monthly => CalculateMonthly(fromTime, reminder.TriggerTime, reminder.MonthlyDay),
            _ => null,
        };
    }

    private static DateTime CalculateDaily(DateTime fromTime, TimeSpan triggerTime)
    {
        var candidate = fromTime.Date.Add(triggerTime);
        if (candidate <= fromTime)
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    private static DateTime? CalculateWeekly(
        DateTime fromTime,
        TimeSpan triggerTime,
        IReadOnlyCollection<DayOfWeek> selectedDays)
    {
        var days = selectedDays.Count == 0
            ? [fromTime.DayOfWeek]
            : selectedDays;

        for (var offset = 0; offset < 14; offset++)
        {
            var candidateDate = fromTime.Date.AddDays(offset);
            if (!days.Contains(candidateDate.DayOfWeek))
            {
                continue;
            }

            var candidate = candidateDate.Add(triggerTime);
            if (candidate > fromTime)
            {
                return candidate;
            }
        }

        return null;
    }

    private static DateTime CalculateMonthly(DateTime fromTime, TimeSpan triggerTime, int dayOfMonth)
    {
        var safeDay = Math.Clamp(dayOfMonth, 1, 31);
        var candidateDay = Math.Min(safeDay, DateTime.DaysInMonth(fromTime.Year, fromTime.Month));
        var candidate = new DateTime(fromTime.Year, fromTime.Month, candidateDay).Add(triggerTime);

        if (candidate > fromTime)
        {
            return candidate;
        }

        var nextMonth = fromTime.AddMonths(1);
        var nextDay = Math.Min(safeDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        return new DateTime(nextMonth.Year, nextMonth.Month, nextDay).Add(triggerTime);
    }

    private static void EnsureDefaults(Reminder reminder)
    {
        reminder.Description ??= string.Empty;
        reminder.WeeklyDays ??= [];
        reminder.MonthlyDay = Math.Clamp(reminder.MonthlyDay, 1, 31);
    }
}
