using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.ViewModels;

public sealed class MonthCalendarSnapshot
{
    public required string MonthLabel { get; init; }
    public required IReadOnlyList<MonthWeekVm> Weeks { get; init; }
}

public sealed class MonthWeekVm
{
    public required IReadOnlyList<MonthDayVm> Days { get; init; }
}

public sealed class MonthDayVm
{
    public DateTime Date { get; init; }
    public string DayNumber { get; init; } = "";
    public bool IsCurrentMonth { get; init; }
    public bool IsToday { get; init; }
    public IReadOnlyList<HomeworkCardVm> Items { get; init; } = [];
    public bool IsEmpty => IsCurrentMonth && Items.Count == 0;
}

public sealed class HomeworkCardVm
{
    public required Homework Homework { get; init; }
    public required Lesson Lesson { get; init; }
    public string Title { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Links { get; init; } = [];
    public string Accent { get; init; } = "#60A5FA";
    public bool IsDone { get; init; }
}

public static class HomeworkCalendar
{
    public const string EmptyMonthText = "Домашек в этом месяце нет.";

    public static MonthCalendarSnapshot Build(
        IReadOnlyList<Homework> homework,
        IReadOnlyList<Lesson> lessons,
        DateTime month,
        DateTime now,
        string? search,
        long? lessonFilter)
    {
        var query = (search ?? "").Trim();
        var byId = lessons.ToDictionary(lesson => lesson.Id);
        var visible = homework
            .Where(item => byId.ContainsKey(item.LessonId))
            .Where(item => lessonFilter is null || item.LessonId == lessonFilter)
            .Where(item => Matches(item, byId[item.LessonId], query))
            .ToList();

        var first = new DateTime(month.Year, month.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var weeks = new List<MonthWeekVm>();
        for (var weekStart = AcademicCalendar.StartOfWeek(first);
             weekStart <= last;
             weekStart = weekStart.AddDays(7))
        {
            var days = new List<MonthDayVm>(7);
            for (var day = 0; day < 7; day++)
            {
                var date = weekStart.AddDays(day);
                var inMonth = date.Month == first.Month && date.Year == first.Year;
                var items = inMonth
                    ? visible
                        .Where(item => item.Deadline.Date == date)
                        .OrderBy(item => item.IsDone)
                        .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                        .Select(item => ToCard(item, byId[item.LessonId]))
                        .ToList()
                    : [];
                days.Add(new MonthDayVm
                {
                    Date = date,
                    DayNumber = inMonth ? date.Day.ToString() : "",
                    IsCurrentMonth = inMonth,
                    IsToday = inMonth && date == now.Date,
                    Items = items
                });
            }

            weeks.Add(new MonthWeekVm { Days = days });
        }

        return new MonthCalendarSnapshot
        {
            MonthLabel = AcademicCalendar.MonthTitle(first),
            Weeks = weeks
        };
    }

    public static DateTime NearestDeadline(IReadOnlyList<Homework> items, DateTime today)
    {
        var upcoming = items
            .Select(item => item.Deadline.Date)
            .Where(date => date >= today.Date)
            .OrderBy(date => date)
            .Cast<DateTime?>()
            .FirstOrDefault();
        return upcoming ?? items.Max(item => item.Deadline.Date);
    }

    private static bool Matches(Homework homework, Lesson lesson, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        return homework.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               homework.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               lesson.Subject.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static string OneLine(string? text)
    {
        var parts = (text ?? "").Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }

    private static HomeworkCardVm ToCard(Homework homework, Lesson lesson) => new()
    {
        Homework = homework,
        Lesson = lesson,
        Title = homework.Title,
        Subject = lesson.Subject,
        Description = OneLine(homework.Description),
        Links = LinkLines(homework),
        Accent = LessonAccent.For(lesson),
        IsDone = homework.IsDone
    };

    private static IReadOnlyList<string> LinkLines(Homework homework)
    {
        var lines = new List<string>();
        foreach (var url in new[] { homework.Url, homework.ExtraUrl })
        {
            var line = OneLine(url);
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
