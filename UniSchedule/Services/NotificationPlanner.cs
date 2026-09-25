using UniSchedule.Models;

namespace UniSchedule.Services;

public readonly record struct DueReminder(Lesson Lesson, int OffsetMinutes);

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
}
