using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.ViewModels;

public sealed class ScheduleSnapshot
{
    public required string WeekLabel { get; init; }
    public required string NextLessonText { get; init; }
    public required IReadOnlyList<DayColumnVm> Days { get; init; }
}

public static class ScheduleComposer
{
    public const string EmptyScheduleText =
        "Пар нет. Добавьте вручную или выберите группу из импортированного файла.";

    public const string NoUpcomingText = "Ближайших пар на неделю нет";

    private static readonly DayOfWeek[] Days =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday
    ];

    public static ScheduleSnapshot Build(
        IReadOnlyList<Lesson> lessons,
        string? search,
        DateTime now,
        DateTime semesterStart)
    {
        var query = (search ?? "").Trim();
        IReadOnlyList<Lesson> visible = query.Length == 0
            ? lessons
            : lessons
                .Where(lesson =>
                    lesson.Subject.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    lesson.Teacher.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var columns = new List<DayColumnVm>(Days.Length);
        foreach (var day in Days)
        {
            var column = new DayColumnVm
            {
                Day = day,
                Title = AcademicCalendar.DayName(day),
                IsToday = now.Date.DayOfWeek == day
            };

            foreach (var lesson in visible.Where(item => item.DayOfWeek == day).OrderBy(item => item.Start))
            {
                column.Lessons.Add(ToCard(lesson, now.Date, semesterStart));
            }

            columns.Add(column);
        }

        return new ScheduleSnapshot
        {
            WeekLabel = AcademicCalendar.WeekLabel(now.Date, semesterStart),
            NextLessonText = visible.Count == 0
                ? EmptyScheduleText
                : BuildNextLessonText(visible, now, semesterStart),
            Days = columns
        };
    }

    private static LessonCardVm ToCard(Lesson lesson, DateTime today, DateTime semesterStart)
    {
        var metaParts = new List<string>();
        var type = AcademicCalendar.TypeLabel(lesson.LessonType);
        if (!string.IsNullOrWhiteSpace(type))
        {
            metaParts.Add(type);
        }

        if (!string.IsNullOrWhiteSpace(lesson.Teacher))
        {
            metaParts.Add(lesson.Teacher);
        }

        if (lesson.Parity != WeekParity.All)
        {
            metaParts.Add(AcademicCalendar.ParityLabel(lesson.Parity));
        }

        if (lesson.WeekFrom is not null || lesson.WeekTo is not null)
        {
            metaParts.Add($"{lesson.WeekFrom ?? 1}–{lesson.WeekTo ?? lesson.WeekFrom} нед.");
        }

        var accent = lesson.LessonType switch
        {
            LessonCodes.Practice => "#34D399",
            LessonCodes.Lab => "#FB923C",
            _ => MeetingLinks.IsCallUrl(lesson.MeetingUrl) ? "#A78BFA" : "#60A5FA"
        };

        return new LessonCardVm
        {
            Lesson = lesson,
            TimeText = lesson.TimeText,
            Subject = lesson.Subject,
            Place = lesson.PlaceText,
            Meta = string.Join(" · ", metaParts),
            Badge = MeetingLinks.IsCallUrl(lesson.MeetingUrl) ? "онлайн" : "",
            IsOnline = lesson.HasLink,
            IsDimmed = !AcademicCalendar.AppliesThisWeek(lesson, today, semesterStart),
            Accent = accent
        };
    }

    private static string BuildNextLessonText(IReadOnlyList<Lesson> lessons, DateTime now, DateTime semesterStart)
    {
        for (var i = 0; i < 7; i++)
        {
            var date = now.Date.AddDays(i);
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            var upcoming = lessons
                .Where(lesson => AcademicCalendar.AppliesOnDate(lesson, date, semesterStart))
                .Where(lesson => i > 0 || date + lesson.Start > now)
                .OrderBy(lesson => lesson.Start)
                .FirstOrDefault();
            if (upcoming is null)
            {
                continue;
            }

            var when = i == 0
                ? "сегодня"
                : i == 1
                    ? "завтра"
                    : AcademicCalendar.DayName(upcoming.DayOfWeek).ToLowerInvariant();
            return $"Ближайшая: {upcoming.Subject} · {when} {upcoming.TimeText} · {upcoming.PlaceText}";
        }

        return NoUpcomingText;
    }
}
