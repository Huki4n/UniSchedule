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
        Assert.Equal("Неделя 1 · нечётная", snapshot.WeekLabel);
        Assert.True(snapshot.Days.Single(day => day.Day == DayOfWeek.Friday).IsToday);
    }

    [Fact]
    public void Build_NextLesson_IsLaterToday_ThenSkipsStartedSlot()
    {
        var lessons = new List<Lesson>
        {
            LessonAt(DayOfWeek.Friday, 8, 30, "Уже началась"),
            LessonAt(DayOfWeek.Friday, 10, 10, "Следующая")
        };

        var snapshot = ScheduleComposer.Build(lessons, null, FridayMorning, SemesterStart);

        Assert.Contains("Следующая", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Contains("сегодня", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Equal(
            ["Уже началась", "Следующая"],
            snapshot.Days.Single(day => day.Day == DayOfWeek.Friday).Lessons.Select(card => card.Subject).ToArray());
    }

    [Fact]
    public void Build_NextLesson_UsesTomorrowAfterThursday()
    {
        var snapshot = ScheduleComposer.Build(
            [LessonAt(DayOfWeek.Friday, 8, 30, "Сети")],
            "",
            new DateTime(2026, 9, 3, 20, 0, 0),
            SemesterStart);

        Assert.Contains("завтра", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Contains("Сети", snapshot.NextLessonText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_Search_FiltersCardsAndNextLesson()
    {
        var lessons = new List<Lesson>
        {
            LessonAt(DayOfWeek.Friday, 10, 10, "Базы данных", "Иванов И.И."),
            LessonAt(DayOfWeek.Monday, 8, 30, "Сети", "Петров П.П.")
        };

        var snapshot = ScheduleComposer.Build(lessons, "петров", FridayMorning, SemesterStart);

        Assert.Contains("Сети", snapshot.NextLessonText, StringComparison.Ordinal);
        Assert.Single(snapshot.Days.SelectMany(day => day.Lessons));
    }

    [Fact]
    public void Build_SearchMiss_UsesEmptyText()
    {
        var snapshot = ScheduleComposer.Build(
            [LessonAt(DayOfWeek.Friday, 10, 10, "Базы данных")],
            "нет такого",
            FridayMorning,
            SemesterStart);

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

        var card = ScheduleComposer.Build([lesson], "", FridayMorning, SemesterStart)
            .Days.Single(day => day.Day == DayOfWeek.Friday)
            .Lessons.Single();

        Assert.Equal("#FB923C", card.Accent);
        Assert.Equal("онлайн", card.Badge);
        Assert.True(card.IsOnline);
        Assert.True(card.IsDimmed);
        Assert.Contains("чётная", card.Meta, StringComparison.Ordinal);
        Assert.Contains("3–3 нед.", card.Meta, StringComparison.Ordinal);
    }

    private static Lesson LessonAt(DayOfWeek day, int hour, int minute, string subject, string teacher = "") =>
        new()
        {
            DayOfWeek = day,
            Start = new TimeSpan(hour, minute, 0),
            End = new TimeSpan(hour + 1, minute, 0),
            Subject = subject,
            Teacher = teacher,
            GroupCode = "11-321"
        };
}
