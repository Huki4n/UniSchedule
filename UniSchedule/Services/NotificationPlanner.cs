using UniSchedule.Models;

namespace UniSchedule.Services;

public readonly record struct DueReminder(Lesson Lesson, int OffsetMinutes);

public readonly record struct DueHomeworkReminder(Homework Homework, int OffsetMinutes, DateTime FireDate);

public static class NotificationPlanner
{
    public static readonly TimeSpan FireWindow = TimeSpan.FromMinutes(2);

    public static IReadOnlyList<DueReminder> Collect(
        IReadOnlyList<Lesson> lessons,
        AppSettings settings,
        DateTime now,
        Func<long, DateTime, int, bool> wasSent)
    {
        if (!settings.NotificationsEnabled || now.DayOfWeek == DayOfWeek.Sunday)
        {
            return [];
        }

        var due = new List<DueReminder>();
        foreach (var lesson in lessons)
        {
            if (lesson.Id <= 0 || !AcademicCalendar.AppliesOnDate(lesson, now, settings.SemesterStart))
            {
                continue;
            }

            var start = now.Date + lesson.Start;
            if (now >= start)
            {
                continue;
            }

            foreach (var offset in settings.ReminderOffsets)
            {
                var fireAt = start - TimeSpan.FromMinutes(offset);
                if (now < fireAt || now > fireAt + FireWindow)
                {
                    continue;
                }

                if (wasSent(lesson.Id, now.Date, offset))
                {
                    continue;
                }

                due.Add(new DueReminder(lesson, offset));
            }
        }

        return due;
    }

    public static IReadOnlyList<DueHomeworkReminder> CollectHomework(
        IReadOnlyList<Homework> homework,
        AppSettings settings,
        DateTime now,
        Func<long, DateTime, int, bool> wasSent)
    {
        if (!settings.NotificationsEnabled)
        {
            return [];
        }

        var due = new List<DueHomeworkReminder>();
        foreach (var item in homework)
        {
            if (item.Id <= 0 || item.IsDone)
            {
                continue;
            }

            var dueAt = HomeworkDueAt(item.Deadline);
            foreach (var offset in settings.HomeworkReminderMinutes)
            {
                var fireAt = offset == AppSettings.HomeworkMonthOffset
                    ? dueAt.AddMonths(-1)
                    : dueAt.AddMinutes(-offset);
                if (now < fireAt || now > fireAt + FireWindow || wasSent(item.Id, fireAt.Date, offset))
                {
                    continue;
                }

                due.Add(new DueHomeworkReminder(item, offset, fireAt.Date));
            }
        }

        return due;
    }

    public static DateTime HomeworkDueAt(DateTime deadline) => deadline.Date.AddDays(1);

    public static string HomeworkDueText(int offsetMinutes) =>
        "через " + AppSettings.HomeworkReminderSpan(offsetMinutes);
}
