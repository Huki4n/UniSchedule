using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.ViewModels;

public sealed class ScheduleSnapshot
{
    public required string WeekLabel { get; init; }
    public required string NextLessonText { get; init; }
    public Lesson? CurrentLesson { get; init; }
    public Lesson? OpenLesson { get; init; }
    public required IReadOnlyList<DayColumnVm> Days { get; init; }
    public required IReadOnlyList<BoardRowVm> Rows { get; init; }
}

public static class ScheduleComposer
{
    public const string EmptyScheduleText =
        "Пар нет. Добавьте вручную или выберите группу из импортированного файла.";

    public const string NoUpcomingText = "Ближайших пар на неделю нет";

    public const string EmptyDayText = "Пар нет";

    private static readonly DayOfWeek[] Days =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
    ];

    public static ScheduleSnapshot Build(
        IReadOnlyList<Lesson> lessons,
        string? search,
        DateTime now,
        DateTime semesterStart,
        DateTime? viewDate = null,
        IReadOnlyList<Homework>? homework = null,
        IReadOnlyList<ElectivePick>? picks = null,
        IReadOnlyList<string>? electiveSubjects = null
    )
    {
        var rows = ElectiveChoice.Display(lessons, picks, search, electiveSubjects);
        var visible = rows.Select(row => row.Lesson).ToList();
        var optionsByLesson = rows.Where(row => row.Options.Count > 1)
            .ToDictionary(row => row.Lesson, row => row.Options);

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
                IsToday = date == now.Date,
            };

            foreach (
                var lesson in visible
                    .Where(item => item.DayOfWeek == date.DayOfWeek)
                    .OrderBy(item => item.Start)
            )
            {
                optionsByLesson.TryGetValue(lesson, out var options);
                column.Lessons.Add(ToCard(lesson, date, semesterStart, homework, lessons, options));
            }

            MarkOverlaps(column.Lessons);
            MarkCurrent(column, now);
            columns.Add(column);
        }

        var boardRows = BuildRows(columns);

        var next =
            visible.Count == 0
                ? (EmptyScheduleText, (Lesson?)null, (Lesson?)null)
                : BuildNextLesson(visible, now, semesterStart);
        return new ScheduleSnapshot
        {
            WeekLabel = AcademicCalendar.WeekLabel(view, semesterStart),
            NextLessonText = next.Item1,
            CurrentLesson = next.Item2,
            OpenLesson = next.Item3,
            Days = columns,
            Rows = boardRows,
        };
    }

    private static List<BoardRowVm> BuildRows(IReadOnlyList<DayColumnVm> columns)
    {
        var starts = columns
            .SelectMany(column => column.Lessons)
            .Select(card => card.Lesson.Start)
            .Distinct()
            .OrderBy(start => start)
            .ToList();
        var rows = new List<BoardRowVm>
        {
            new()
            {
                Cells = columns
                    .Select(day => new BoardCellVm { Day = day, IsHeader = true })
                    .ToList(),
            },
        };
        foreach (var start in starts)
        {
            rows.Add(
                new BoardRowVm
                {
                    Cells = columns
                        .Select(day => new BoardCellVm
                        {
                            Day = day,
                            Cards = day.Lessons.Where(card => card.Lesson.Start == start).ToList(),
                        })
                        .ToList(),
                }
            );
        }

        return rows;
    }

    private static void MarkOverlaps(IReadOnlyList<LessonCardVm> cards)
    {
        var live = cards.Where(card => !card.IsDimmed).ToList();
        for (var i = 0; i < live.Count; i++)
        {
            for (var j = i + 1; j < live.Count; j++)
            {
                var left = live[i].Lesson;
                var right = live[j].Lesson;
                if (left.Start < right.End && right.Start < left.End)
                {
                    live[i].IsOverlap = true;
                    live[j].IsOverlap = true;
                }
            }
        }
    }

    private static void MarkCurrent(DayColumnVm column, DateTime now)
    {
        if (column.Date != now.Date)
        {
            return;
        }

        var current = column
            .Lessons.Where(card => !card.IsDimmed)
            .Where(card =>
                column.Date + card.Lesson.Start <= now && now < column.Date + card.Lesson.End
            )
            .OrderByDescending(card => card.Lesson.Start)
            .FirstOrDefault();
        if (current is not null)
        {
            current.IsCurrent = true;
        }
    }

    private static LessonCardVm ToCard(
        Lesson lesson,
        DateTime columnDate,
        DateTime semesterStart,
        IReadOnlyList<Homework>? homework,
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<Lesson>? options
    )
    {
        var metaParts = new List<string>();
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

        var meta = string.Join(" · ", metaParts);
        var room = string.IsNullOrWhiteSpace(lesson.Room) ? "" : lesson.Room.Trim();
        var detail =
            room.Length == 0 ? meta
            : meta.Length == 0 ? room
            : $"{meta} · {room}";

        return new LessonCardVm
        {
            Lesson = lesson,
            TimeText = lesson.TimeText,
            StartText = $"{lesson.Start:hh\\:mm}",
            EndText = $"{lesson.End:hh\\:mm}",
            Subject = lesson.Subject,
            Place = lesson.PlaceText == "—" ? "" : lesson.PlaceText,
            Meta = meta,
            Detail = detail,
            ShowOnline = lesson.PlaceText == "Онлайн" || MeetingLinks.IsCallUrl(lesson.MeetingUrl),
            Notes = OneLine(lesson.Notes),
            Badge =
                MeetingLinks.IsCallUrl(lesson.MeetingUrl) && lesson.PlaceText != "Онлайн"
                    ? "онлайн"
                    : "",
            Links = CardLinks(lesson),
            IsOnline = lesson.HasLink,
            IsDimmed = !AcademicCalendar.AppliesThisWeek(lesson, columnDate, semesterStart),
            Accent = LessonAccent.For(lesson),
            HomeworkLinks = HomeworkLines(homework, lesson, columnDate, lessons),
            HasElectiveChoice = options is { Count: > 1 },
            ElectiveChoices = options is { Count: > 1 }
                ? options
                    .Select(option => new ElectiveChoiceVm
                    {
                        LessonId = option.Id,
                        Subject = option.Subject,
                        IsCurrent = ReferenceEquals(option, lesson),
                    })
                    .ToList()
                : [],
        };
    }

    private static string OneLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<CardLinkVm> CardLinks(Lesson lesson)
    {
        var links = new List<CardLinkVm>();
        Add(lesson.MeetingUrl, "Ссылка на занятие");
        Add(lesson.LmsUrl, "Ссылка на LMS");
        return links;

        void Add(string? url, string label)
        {
            var line = (url ?? "").Trim();
            if (
                line.Length == 0
                || links.Any(item =>
                    string.Equals(item.Url, line, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return;
            }

            links.Add(new CardLinkVm { Label = label, Url = line });
        }
    }

    private static IReadOnlyList<HomeworkLinkVm> HomeworkLines(
        IReadOnlyList<Homework>? homework,
        Lesson lesson,
        DateTime columnDate,
        IReadOnlyList<Lesson> lessons
    )
    {
        if (homework is null || lesson.Id <= 0)
        {
            return [];
        }

        var week = AcademicCalendar.StartOfWeek(columnDate);
        return homework
            .Select(item => HomeworkLine(item, lesson, columnDate, week, lessons))
            .Where(item => item is not null)
            .Cast<HomeworkLinkVm>()
            .OrderBy(item => item.Homework.IsDone)
            .ThenBy(item => item.Homework.Deadline)
            .ThenBy(item => item.Homework.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HomeworkLinkVm? HomeworkLine(
        Homework item,
        Lesson lesson,
        DateTime columnDate,
        DateTime week,
        IReadOnlyList<Lesson> lessons
    )
    {
        var title = item.Title.Trim();
        if (item.LessonId == lesson.Id && item.Deadline.Date == columnDate.Date)
        {
            return new HomeworkLinkVm { Homework = item, Label = $"ДЗ · {title}" };
        }

        if (
            item.IsDone
            || item.Deadline.Date <= columnDate.Date
            || AcademicCalendar.StartOfWeek(item.Deadline) != week
        )
        {
            return null;
        }

        var owner = lessons.FirstOrDefault(candidate => candidate.Id == item.LessonId);
        if (owner is null || !LessonSeries.SameName(owner, lesson))
        {
            return null;
        }

        var day = AcademicCalendar.DayShort(item.Deadline.DayOfWeek).ToLowerInvariant();
        return new HomeworkLinkVm { Homework = item, Label = $"ДЗ · {title} · до {day}" };
    }

    private static (string Text, Lesson? Current, Lesson? Open) BuildNextLesson(
        IReadOnlyList<Lesson> lessons,
        DateTime now,
        DateTime semesterStart
    )
    {
        var today = now.Date;
        if (today.DayOfWeek != DayOfWeek.Sunday)
        {
            var current = lessons
                .Where(lesson => AcademicCalendar.AppliesOnDate(lesson, today, semesterStart))
                .Where(lesson => today + lesson.Start <= now && now < today + lesson.End)
                .OrderByDescending(lesson => lesson.Start)
                .FirstOrDefault();
            if (current is not null)
            {
                return (
                    $"Сейчас: {current.Subject} · до {current.End:hh\\:mm} · {current.PlaceText}",
                    current,
                    current
                );
            }
        }

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

            var when =
                i == 0 ? "сегодня"
                : i == 1 ? "завтра"
                : AcademicCalendar.DayName(upcoming.DayOfWeek).ToLowerInvariant();
            return (
                $"Ближайшая: {upcoming.Subject} · {when} {upcoming.TimeText} · {upcoming.PlaceText}",
                null,
                upcoming
            );
        }

        return (NoUpcomingText, null, null);
    }
}
