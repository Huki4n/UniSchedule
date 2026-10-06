using UniSchedule.Models;
using UniSchedule.ViewModels;

namespace UniSchedule.Tests;

public class HomeworkCalendarTests
{
    private static readonly DateTime Today = new(2026, 9, 4);
    private static readonly Lesson Networks = new()
    {
        Id = 5,
        Subject = "Сети",
        LessonType = LessonCodes.Practice,
        DayOfWeek = DayOfWeek.Tuesday,
    };

    [Fact]
    public void Build_PlacesHomeworkOnDeadline_AndHidesOutsideDays()
    {
        var homework = new Homework
        {
            Id = 1,
            LessonId = 5,
            Title = "ЛР 1",
            Description = "отчёт",
            Deadline = new DateTime(2026, 9, 15),
        };

        var month = HomeworkCalendar.Build(
            [homework],
            [Networks],
            new DateTime(2026, 9, 1),
            Today,
            "",
            null
        );

        Assert.Equal("Сентябрь 2026", month.MonthLabel);
        Assert.Equal(5, month.Weeks.Count);
        Assert.All(month.Weeks, week => Assert.Equal(7, week.Days.Count));
        var days = month.Weeks.SelectMany(week => week.Days).ToList();
        var cell = days.Single(day => day.Date == new DateTime(2026, 9, 15));
        Assert.True(cell.IsCurrentMonth);
        Assert.Equal("15", cell.DayNumber);
        var card = Assert.Single(cell.Items);
        Assert.Equal("ЛР 1", card.Title);
        Assert.Equal("Сети", card.Subject);
        Assert.Equal("#34D399", card.Accent);
        Assert.False(card.IsDone);
        Assert.False(card.IsOverdue);
        Assert.Empty(month.Overdue);

        var leading = days.Single(day => day.Date == new DateTime(2026, 8, 31));
        Assert.False(leading.IsCurrentMonth);
        Assert.Equal("", leading.DayNumber);
        Assert.Empty(leading.Items);
        Assert.Equal(
            Enumerable.Range(1, 30).Select(day => day.ToString()).ToArray(),
            days.Where(day => day.IsCurrentMonth).Select(day => day.DayNumber).ToArray()
        );
        Assert.All(days.Where(day => !day.IsCurrentMonth), day => Assert.Equal("", day.DayNumber));
        Assert.DoesNotContain(days, day => day.Date == new DateTime(2026, 10, 5));
    }

    [Fact]
    public void Build_SearchAndLessonFilter_HideOtherCards()
    {
        var homework = new Homework
        {
            LessonId = 5,
            Title = "ЛР 1",
            Description = "отчёт",
            Deadline = new DateTime(2026, 9, 15),
        };
        var other = new Homework
        {
            LessonId = 9,
            Title = "Чужое",
            Deadline = new DateTime(2026, 9, 15),
        };
        var lessons = new List<Lesson>
        {
            Networks,
            new() { Id = 9, Subject = "Базы" },
        };

        Assert.NotEmpty(
            Cards(
                HomeworkCalendar.Build(
                    [homework],
                    [Networks],
                    new DateTime(2026, 9, 1),
                    Today,
                    "отчёт",
                    null
                )
            )
        );
        Assert.NotEmpty(
            Cards(
                HomeworkCalendar.Build(
                    [homework],
                    [Networks],
                    new DateTime(2026, 9, 1),
                    Today,
                    "сети",
                    null
                )
            )
        );
        Assert.Empty(
            Cards(
                HomeworkCalendar.Build(
                    [homework],
                    [Networks],
                    new DateTime(2026, 9, 1),
                    Today,
                    "нет",
                    null
                )
            )
        );
        Assert.DoesNotContain(
            Cards(
                HomeworkCalendar.Build(
                    [homework, other],
                    lessons,
                    new DateTime(2026, 9, 1),
                    Today,
                    "",
                    9
                )
            ),
            card => card.Title == "ЛР 1"
        );
    }

    [Fact]
    public void Build_DoneCard_StaysOnTheDay()
    {
        var homework = new Homework
        {
            LessonId = 5,
            Title = "ЛР 1",
            Deadline = new DateTime(2026, 9, 15),
            IsDone = true,
        };

        var month = HomeworkCalendar.Build(
            [homework],
            [Networks],
            new DateTime(2026, 9, 1),
            Today,
            "",
            null
        );
        var card = Assert.Single(Cards(month));
        Assert.True(card.IsDone);
        Assert.False(card.IsOverdue);
        Assert.Empty(month.Overdue);
    }

    [Fact]
    public void Build_Overdue_ListsPastUndone_AndSkipsTodayAndDone()
    {
        var past = new Homework
        {
            LessonId = 5,
            Title = "Старая",
            Deadline = new DateTime(2026, 8, 31),
        };
        var yesterday = new Homework
        {
            LessonId = 5,
            Title = "Вчера",
            Deadline = new DateTime(2026, 9, 3),
        };
        var today = new Homework
        {
            LessonId = 5,
            Title = "Сегодня",
            Deadline = Today,
        };
        var done = new Homework
        {
            LessonId = 5,
            Title = "Сдана",
            Deadline = new DateTime(2026, 9, 1),
            IsDone = true,
        };

        var month = HomeworkCalendar.Build(
            [past, yesterday, today, done],
            [Networks],
            new DateTime(2026, 9, 1),
            Today,
            "",
            null
        );

        Assert.Equal(["Старая", "Вчера"], month.Overdue.Select(card => card.Title).ToArray());
        Assert.Equal("31.08.2026", month.Overdue[0].DeadlineText);
        Assert.All(month.Overdue, card => Assert.True(card.IsOverdue));
        Assert.Equal(
            HomeworkCalendar.OverdueLabel(2),
            HomeworkCalendar.OverdueLabel(month.Overdue.Count)
        );
        Assert.DoesNotContain(Cards(month), card => card.Title == "Старая");
        var yesterdayCard = Cards(month).Single(card => card.Title == "Вчера");
        Assert.True(yesterdayCard.IsOverdue);
        Assert.False(Cards(month).Single(card => card.Title == "Сегодня").IsOverdue);
        Assert.False(Cards(month).Single(card => card.Title == "Сдана").IsOverdue);

        var filtered = HomeworkCalendar.Build(
            [past, yesterday],
            [Networks],
            new DateTime(2026, 9, 1),
            Today,
            "вчера",
            null
        );
        Assert.Equal(["Вчера"], filtered.Overdue.Select(card => card.Title).ToArray());
    }

    [Fact]
    public void Build_CardShowsOneLineDescriptionAndLinks()
    {
        var homework = new Homework
        {
            LessonId = 5,
            Title = "ЛР 1",
            Description = "  первая\nстрока  ",
            Url = " https://edu.kpfu.ru/1 ",
            ExtraUrl = "  ",
            Deadline = new DateTime(2026, 9, 15),
        };

        var card = Assert.Single(
            Cards(
                HomeworkCalendar.Build(
                    [homework],
                    [Networks],
                    new DateTime(2026, 9, 1),
                    Today,
                    "",
                    null
                )
            )
        );
        Assert.Equal("первая строка", card.Description);
        Assert.Equal(["https://edu.kpfu.ru/1"], card.Links);
    }

    [Fact]
    public void NearestDeadline_PrefersUpcoming_ThenLatestPast()
    {
        var items = new List<Homework>
        {
            new() { Deadline = new DateTime(2026, 9, 1) },
            new() { Deadline = new DateTime(2026, 9, 20) },
            new() { Deadline = new DateTime(2026, 10, 2) },
        };

        Assert.Equal(new DateTime(2026, 9, 20), HomeworkCalendar.NearestDeadline(items, Today));
        Assert.Equal(
            new DateTime(2026, 10, 2),
            HomeworkCalendar.NearestDeadline(items, new DateTime(2026, 11, 1))
        );
    }

    private static List<HomeworkCardVm> Cards(MonthCalendarSnapshot month) =>
        month.Weeks.SelectMany(week => week.Days).SelectMany(day => day.Items).ToList();
}
