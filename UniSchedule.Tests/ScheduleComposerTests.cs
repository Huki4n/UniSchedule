using UniSchedule.Models;
using UniSchedule.ViewModels;

namespace UniSchedule.Tests;

public class ScheduleComposerTests
{
    private static readonly DateTime SemesterStart = new(2026, 9, 1);
    private static readonly DateTime FridayMorning = new(2026, 9, 4, 9, 0, 0);

    [Fact]
    public void Build_EmptySchedule_UsesEmptyText()
    {
        var snapshot = ScheduleComposer.Build([], "", FridayMorning, SemesterStart);

        Assert.Equal(ScheduleComposer.EmptyScheduleText, snapshot.NextLessonText);
        Assert.Equal(6, snapshot.Days.Count);
        Assert.All(snapshot.Days, day => Assert.True(day.IsEmpty));
        Assert.True(Assert.Single(snapshot.Rows).Cells.All(cell => cell.IsHeader));
        Assert.Null(snapshot.CurrentLesson);
        Assert.Null(snapshot.OpenLesson);
        Assert.Equal("Неделя 1 · нечётная", snapshot.WeekLabel);
        Assert.True(snapshot.Days.Single(day => day.Day == DayOfWeek.Friday).IsToday);
    }

    [Fact]
    public void Build_NextLesson_ShowsLessonInProgress()
    {
        var current = LessonAt(DayOfWeek.Friday, 8, 30, "Уже началась");
        current.Room = "1301";
        var snapshot = ScheduleComposer.Build(
            [current, LessonAt(DayOfWeek.Friday, 10, 10, "Следующая")],
            null,
            FridayMorning,
            SemesterStart
        );

        Assert.Equal("Сейчас: Уже началась · до 09:30 · 1301", snapshot.NextLessonText);
        Assert.Same(current, snapshot.CurrentLesson);
        Assert.Same(current, snapshot.OpenLesson);
        var friday = snapshot.Days.Single(day => day.Day == DayOfWeek.Friday);
        Assert.Equal(
            ["Уже началась", "Следующая"],
            friday.Lessons.Select(card => card.Subject).ToArray()
        );
        Assert.True(friday.Lessons.Single(card => card.Subject == "Уже началась").IsCurrent);
        Assert.False(friday.Lessons.Single(card => card.Subject == "Следующая").IsCurrent);
        Assert.True(snapshot.Days.Single(day => day.Day == DayOfWeek.Monday).IsEmpty);
    }

    [Fact]
    public void Build_NextLesson_PrefersLaterOverlappingStart()
    {
        var early = LessonAt(DayOfWeek.Friday, 8, 30, "Первая");
        early.End = new TimeSpan(10, 0, 0);
        var later = LessonAt(DayOfWeek.Friday, 9, 0, "Вторая");
        later.End = new TimeSpan(10, 30, 0);

        var snapshot = ScheduleComposer.Build(
            [early, later],
            "",
            new DateTime(2026, 9, 4, 9, 15, 0),
            SemesterStart
        );

        Assert.Equal("Сейчас: Вторая · до 10:30 · —", snapshot.NextLessonText);
    }

    [Fact]
    public void Build_NextLesson_SkipsLessonThatAlreadyEnded()
    {
        var ended = LessonAt(DayOfWeek.Friday, 8, 30, "Уже кончилась");
        ended.End = new TimeSpan(9, 0, 0);
        var snapshot = ScheduleComposer.Build(
            [ended, LessonAt(DayOfWeek.Friday, 10, 10, "Следующая")],
            null,
            FridayMorning,
            SemesterStart
        );

        Assert.Contains("Ближайшая", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Contains("Следующая", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Contains("сегодня", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Null(snapshot.CurrentLesson);
        Assert.Equal("Следующая", snapshot.OpenLesson?.Subject);
    }

    [Fact]
    public void Build_NextLesson_IgnoresCurrentLessonOnAnotherWeek()
    {
        var other = LessonAt(DayOfWeek.Friday, 8, 30, "Чужая неделя");
        other.Parity = WeekParity.Even;
        var snapshot = ScheduleComposer.Build(
            [other, LessonAt(DayOfWeek.Friday, 10, 10, "Следующая")],
            "",
            FridayMorning,
            SemesterStart
        );

        Assert.Contains("Следующая", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.DoesNotContain("Сейчас", snapshot.NextLessonText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NextLesson_UsesTomorrowAfterThursday()
    {
        var snapshot = ScheduleComposer.Build(
            [LessonAt(DayOfWeek.Friday, 8, 30, "Сети")],
            "",
            new DateTime(2026, 9, 3, 20, 0, 0),
            SemesterStart
        );

        Assert.Contains("завтра", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Contains("Сети", snapshot.NextLessonText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_Search_FiltersCardsAndNextLesson()
    {
        var lessons = new List<Lesson>
        {
            LessonAt(DayOfWeek.Friday, 10, 10, "Базы данных", "Иванов И.И."),
            LessonAt(DayOfWeek.Monday, 8, 30, "Сети", "Петров П.П."),
        };

        var snapshot = ScheduleComposer.Build(lessons, "петров", FridayMorning, SemesterStart);

        Assert.Contains("Сети", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Single(snapshot.Days.SelectMany(day => day.Lessons));
    }

    [Fact]
    public void Build_Search_MatchesRoomAndNotes()
    {
        var byRoom = LessonAt(DayOfWeek.Friday, 10, 10, "Базы");
        byRoom.Room = "1301";
        var byNotes = LessonAt(DayOfWeek.Monday, 8, 30, "Сети");
        byNotes.Notes = "перенос на пятницу";

        var room = ScheduleComposer.Build([byRoom, byNotes], "1301", FridayMorning, SemesterStart);
        Assert.Equal(
            ["Базы"],
            room.Days.SelectMany(day => day.Lessons).Select(card => card.Subject).ToArray()
        );

        var notes = ScheduleComposer.Build(
            [byRoom, byNotes],
            "перенос",
            FridayMorning,
            SemesterStart
        );
        Assert.Equal(
            ["Сети"],
            notes.Days.SelectMany(day => day.Lessons).Select(card => card.Subject).ToArray()
        );
    }

    [Fact]
    public void Build_Overlap_MarksCrossingLessonsOnTheSameDay()
    {
        var early = LessonAt(DayOfWeek.Friday, 8, 30, "Первая");
        early.End = new TimeSpan(10, 0, 0);
        var crossing = LessonAt(DayOfWeek.Friday, 9, 0, "Вторая");
        crossing.End = new TimeSpan(10, 30, 0);
        var next = LessonAt(DayOfWeek.Friday, 10, 30, "Третья");
        next.End = new TimeSpan(12, 0, 0);
        var otherWeek = LessonAt(DayOfWeek.Friday, 9, 0, "Чужая");
        otherWeek.End = new TimeSpan(10, 30, 0);
        otherWeek.Parity = WeekParity.Even;

        var cards = ScheduleComposer
            .Build([early, crossing, next, otherWeek], "", FridayMorning, SemesterStart)
            .Days.Single(day => day.Day == DayOfWeek.Friday)
            .Lessons;

        Assert.True(cards.Single(card => card.Subject == "Первая").IsOverlap);
        Assert.True(cards.Single(card => card.Subject == "Вторая").IsOverlap);
        Assert.False(cards.Single(card => card.Subject == "Третья").IsOverlap);
        Assert.False(cards.Single(card => card.Subject == "Чужая").IsOverlap);
    }

    [Fact]
    public void Build_Elective_ShowsChosenOption_AndSearchCanRevealTheOther()
    {
        var first = LessonAt(
            DayOfWeek.Friday,
            8,
            30,
            "Кроссплатформенная разработка",
            "Евстратова Д.Д."
        );
        var second = LessonAt(DayOfWeek.Friday, 8, 30, "Основы Linux", "Резников Я.Я.");
        first.ElectiveKey = "11-321|5|08:30";
        second.ElectiveKey = first.ElectiveKey;
        first.Id = 1;
        second.Id = 2;
        var picks = new List<ElectivePick>
        {
            new()
            {
                GroupCode = "11-321",
                DayOfWeek = DayOfWeek.Friday,
                Start = "08:30",
                Subject = "Основы Linux",
            },
        };

        var chosen = ScheduleComposer
            .Build([first, second], "", FridayMorning, SemesterStart, picks: picks)
            .Days.Single(day => day.Day == DayOfWeek.Friday)
            .Lessons;
        var card = Assert.Single(chosen);
        Assert.Equal("Основы Linux", card.Subject);
        Assert.False(card.IsOverlap);
        Assert.True(card.HasElectiveChoice);
        Assert.Equal(
            ["Кроссплатформенная разработка", "Основы Linux"],
            card.ElectiveChoices.Select(item => item.Subject).ToArray()
        );
        Assert.True(card.ElectiveChoices.Single(item => item.Subject == "Основы Linux").IsCurrent);

        var searched = ScheduleComposer
            .Build([first, second], "кросс", FridayMorning, SemesterStart, picks: picks)
            .Days.Single(day => day.Day == DayOfWeek.Friday)
            .Lessons;
        Assert.Equal("Кроссплатформенная разработка", Assert.Single(searched).Subject);
    }

    [Fact]
    public void Build_ElectiveSubjects_ShowsCheckedSubjectsOnEverySlot()
    {
        var fridayLinux = LessonAt(DayOfWeek.Friday, 8, 30, "Основы Linux");
        var fridayCross = LessonAt(DayOfWeek.Friday, 8, 30, "Кроссплатформенная разработка");
        fridayLinux.ElectiveKey = "11-321|5|08:30";
        fridayCross.ElectiveKey = fridayLinux.ElectiveKey;
        var thursdayLinux = LessonAt(DayOfWeek.Thursday, 12, 0, "Основы Linux");
        var thursdayCross = LessonAt(DayOfWeek.Thursday, 12, 0, "Кроссплатформенная разработка");
        thursdayLinux.ElectiveKey = "11-321|4|12:00";
        thursdayCross.ElectiveKey = thursdayLinux.ElectiveKey;

        var both = ScheduleComposer.Build(
            [fridayLinux, fridayCross, thursdayLinux, thursdayCross],
            "",
            FridayMorning,
            SemesterStart,
            electiveSubjects: ["Основы Linux", "Кроссплатформенная разработка"]
        );
        var friday = both.Days.Single(day => day.Day == DayOfWeek.Friday).Lessons;
        Assert.Equal(2, friday.Count);
        Assert.All(friday, card => Assert.True(card.IsOverlap));
        Assert.Equal(2, both.Days.Single(day => day.Day == DayOfWeek.Thursday).Lessons.Count);

        var none = ScheduleComposer.Build(
            [fridayLinux, fridayCross],
            "",
            FridayMorning,
            SemesterStart,
            electiveSubjects: []
        );
        Assert.Empty(none.Days.Single(day => day.Day == DayOfWeek.Friday).Lessons);

        var revealed = ScheduleComposer.Build(
            [fridayLinux, fridayCross],
            "кросс",
            FridayMorning,
            SemesterStart,
            electiveSubjects: ["Основы Linux"]
        );
        Assert.Equal(
            "Кроссплатформенная разработка",
            Assert.Single(revealed.Days.Single(day => day.Day == DayOfWeek.Friday).Lessons).Subject
        );
    }

    [Fact]
    public void Build_SearchMiss_UsesEmptyText()
    {
        var snapshot = ScheduleComposer.Build(
            [LessonAt(DayOfWeek.Friday, 10, 10, "Базы данных")],
            "нет такого",
            FridayMorning,
            SemesterStart
        );

        Assert.Equal(ScheduleComposer.EmptyScheduleText, snapshot.NextLessonText);
    }

    [Fact]
    public void Build_Card_MarksOnlineBadge_AndDimsOtherParity()
    {
        var lesson = LessonAt(DayOfWeek.Friday, 10, 10, "Сети");
        lesson.LessonType = LessonCodes.Lab;
        lesson.Parity = WeekParity.Even;
        lesson.MeetingUrl = "https://telemost.yandex.ru/j/1";
        lesson.WeekFrom = 3;

        var card = ScheduleComposer
            .Build([lesson], "", FridayMorning, SemesterStart)
            .Days.Single(day => day.Day == DayOfWeek.Friday)
            .Lessons.Single();

        Assert.Equal("#FB923C", card.Accent);
        Assert.Equal("Онлайн", card.Place);
        Assert.Equal("", card.Badge);
        Assert.True(card.ShowOnline);
        Assert.Equal("10:10", card.StartText);
        Assert.Equal("11:10", card.EndText);
        Assert.DoesNotContain("Онлайн", card.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Лабораторная", card.Meta, StringComparison.Ordinal);
        Assert.Equal(["Ссылка на занятие"], card.Links.Select(link => link.Label).ToArray());

        lesson.Room = "1301";
        lesson.Notes = "вебинары\nещё";
        lesson.LmsUrl = lesson.MeetingUrl;
        var withRoom = ScheduleComposer
            .Build([lesson], "", FridayMorning, SemesterStart)
            .Days.Single(day => day.Day == DayOfWeek.Friday)
            .Lessons.Single();
        Assert.Equal("1301", withRoom.Place);
        Assert.Equal("онлайн", withRoom.Badge);
        Assert.True(withRoom.ShowOnline);
        Assert.Contains("1301", withRoom.Detail, StringComparison.Ordinal);
        Assert.Equal(["Ссылка на занятие"], withRoom.Links.Select(link => link.Label).ToArray());
        Assert.Equal("вебинары ещё", withRoom.Notes);
        Assert.Equal("", card.Notes);
        Assert.True(card.IsOnline);
        Assert.True(card.IsDimmed);
        Assert.Contains("чётная", card.Meta, StringComparison.Ordinal);
        Assert.Contains("3–3 нед.", card.Meta, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ViewWeek_DoesNotMoveNextLesson_AndUsesColumnDate()
    {
        var now = new DateTime(2026, 9, 4, 9, 0, 0);
        var view = new DateTime(2026, 9, 11);
        var even = LessonAt(DayOfWeek.Friday, 12, 0, "Чётная");
        even.Parity = WeekParity.Even;
        var snapshot = ScheduleComposer.Build(
            [LessonAt(DayOfWeek.Friday, 10, 10, "Сети"), even],
            "",
            now,
            SemesterStart,
            view
        );

        Assert.Contains("Сети", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Contains("сегодня", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Equal("Неделя 2 · чётная", snapshot.WeekLabel);
        var friday = snapshot.Days.Single(day => day.Day == DayOfWeek.Friday);
        Assert.False(friday.IsToday);
        Assert.Equal(view, friday.Date);
        Assert.Contains("11 сент.", friday.Title, StringComparison.Ordinal);
        Assert.False(friday.Lessons.Single(card => card.Subject == "Чётная").IsDimmed);
        Assert.Equal(
            new DateTime(2026, 9, 7),
            snapshot.Days.Single(day => day.Day == DayOfWeek.Monday).Date
        );
    }

    [Fact]
    public void Build_ShowsHomeworkOnDeadline_AndEarlierSameSubject()
    {
        var lesson = LessonAt(DayOfWeek.Friday, 10, 10, "Сети");
        lesson.Id = 7;
        var earlier = LessonAt(DayOfWeek.Monday, 8, 30, "Сети");
        earlier.Id = 9;
        var otherSubject = LessonAt(DayOfWeek.Monday, 12, 0, "Математика");
        otherSubject.Id = 10;
        var due = new Homework
        {
            LessonId = 7,
            Title = "ЛР",
            Deadline = new DateTime(2026, 9, 4),
        };
        var later = new Homework
        {
            LessonId = 7,
            Title = "Позже",
            Deadline = new DateTime(2026, 9, 11),
        };
        var other = new Homework
        {
            LessonId = 8,
            Title = "Чужая",
            Deadline = new DateTime(2026, 9, 4),
        };
        var done = new Homework
        {
            LessonId = 7,
            Title = "Сдано",
            Deadline = new DateTime(2026, 9, 4),
            IsDone = true,
        };

        var today = ScheduleComposer.Build(
            [lesson, earlier, otherSubject],
            "",
            FridayMorning,
            SemesterStart,
            homework: [due, later, other, done]
        );
        var card = today.Days.Single(day => day.Day == DayOfWeek.Friday).Lessons.Single();
        Assert.Equal(
            ["ДЗ · ЛР", "ДЗ · Сдано"],
            card.HomeworkLinks.Select(link => link.Label).ToArray()
        );
        Assert.Equal(
            ["ДЗ · ЛР · до пт"],
            today
                .Days.Single(day => day.Day == DayOfWeek.Monday)
                .Lessons.Single(item => item.Subject == "Сети")
                .HomeworkLinks.Select(link => link.Label)
                .ToArray()
        );
        Assert.Empty(
            today
                .Days.Single(day => day.Day == DayOfWeek.Monday)
                .Lessons.Single(item => item.Subject == "Математика")
                .HomeworkLinks
        );

        var nextWeek = ScheduleComposer.Build(
            [lesson],
            "",
            FridayMorning,
            SemesterStart,
            new DateTime(2026, 9, 11),
            [due, later]
        );
        Assert.Equal(
            ["ДЗ · Позже"],
            nextWeek
                .Days.Single(day => day.Day == DayOfWeek.Friday)
                .Lessons.Single()
                .HomeworkLinks.Select(link => link.Label)
                .ToArray()
        );
    }

    [Fact]
    public void Build_AlignsSameStartAcrossDays()
    {
        var snapshot = ScheduleComposer.Build(
            [
                LessonAt(DayOfWeek.Monday, 15, 20, "Понедельник"),
                LessonAt(DayOfWeek.Tuesday, 13, 50, "Раньше"),
                LessonAt(DayOfWeek.Tuesday, 15, 20, "Вторник"),
            ],
            "",
            FridayMorning,
            SemesterStart
        );

        Assert.Equal(3, snapshot.Rows.Count);
        var early = snapshot.Rows[1];
        Assert.Empty(early.Cells.Single(cell => cell.Day.Day == DayOfWeek.Monday).Cards);
        Assert.Equal(
            "Раньше",
            early.Cells.Single(cell => cell.Day.Day == DayOfWeek.Tuesday).Cards.Single().Subject
        );
        var same = snapshot.Rows[2];
        Assert.Equal(
            "Понедельник",
            same.Cells.Single(cell => cell.Day.Day == DayOfWeek.Monday).Cards.Single().Subject
        );
        Assert.Equal(
            "Вторник",
            same.Cells.Single(cell => cell.Day.Day == DayOfWeek.Tuesday).Cards.Single().Subject
        );
    }

    private static Lesson LessonAt(
        DayOfWeek day,
        int hour,
        int minute,
        string subject,
        string teacher = ""
    ) =>
        new()
        {
            DayOfWeek = day,
            Start = new TimeSpan(hour, minute, 0),
            End = new TimeSpan(hour + 1, minute, 0),
            Subject = subject,
            Teacher = teacher,
            GroupCode = "11-321",
        };
}
