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
        DateTime semesterStart,
        DateTime? viewDate = null,
        IReadOnlyList<Homework>? homework = null)
    {
        var query = (search ?? "").Trim();
        IReadOnlyList<Lesson> visible = query.Length == 0
            ? lessons
            : lessons
                .Where(lesson =>
                    lesson.Subject.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    lesson.Teacher.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var view = (viewDate ?? now).Date;
        var weekStart = AcademicCalendar.StartOfWeek(view);
        var columns = new List<DayColumnVm>(Days.Length);
        for (var i = 0; i < Days.Length; i++)
        {
            var date = weekStart.AddDays(i);
            var column = new DayColumnVm
            {
                Day = Days[i],
                Date = date,
                Title = AcademicCalendar.ColumnTitle(date),
                IsToday = date == now.Date
            };

            foreach (var lesson in visible.Where(item => item.DayOfWeek == date.DayOfWeek).OrderBy(item => item.Start))
            {
                column.Lessons.Add(ToCard(lesson, date, semesterStart, homework));
            }

            columns.Add(column);
        }

        return new ScheduleSnapshot
        {
            WeekLabel = AcademicCalendar.WeekLabel(view, semesterStart),
            NextLessonText = visible.Count == 0
                ? EmptyScheduleText
                : BuildNextLessonText(visible, now, semesterStart),
            Days = columns
        };
    }

    private static LessonCardVm ToCard(
        Lesson lesson,
        DateTime columnDate,
        DateTime semesterStart,
        IReadOnlyList<Homework>? homework)
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

        return new LessonCardVm
        {
            Lesson = lesson,
            TimeText = lesson.TimeText,
            Subject = lesson.Subject,
            Place = lesson.PlaceText,
            Meta = string.Join(" · ", metaParts),
            Badge = MeetingLinks.IsCallUrl(lesson.MeetingUrl) && lesson.PlaceText != "Онлайн" ? "онлайн" : "",
            Links = CardLinks(lesson),
            IsOnline = lesson.HasLink,
            IsDimmed = !AcademicCalendar.AppliesThisWeek(lesson, columnDate, semesterStart),
            Accent = LessonAccent.For(lesson),
            HomeworkLinks = DueLinks(homework, lesson, columnDate)
        };
    }

    private static IReadOnlyList<string> CardLinks(Lesson lesson)
    {
        var links = new List<string>();
        Add(lesson.MeetingUrl);
        Add(lesson.LmsUrl);
        return links;

        void Add(string? url)
        {
            var line = (url ?? "").Trim();
            if (line.Length > 0 && !links.Contains(line, StringComparer.OrdinalIgnoreCase))
            {
                links.Add(line);
            }
        }
    }

    private static IReadOnlyList<HomeworkLinkVm> DueLinks(
        IReadOnlyList<Homework>? homework,
        Lesson lesson,
        DateTime columnDate)
    {
        if (homework is null || lesson.Id <= 0)
        {
            return [];
        }

        return homework
            .Where(item => item.LessonId == lesson.Id && item.Deadline.Date == columnDate.Date)
            .OrderBy(item => item.IsDone)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(item => new HomeworkLinkVm
            {
                Homework = item,
                Label = $"ДЗ · {item.Title.Trim()}"
            })
            .ToList();
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
