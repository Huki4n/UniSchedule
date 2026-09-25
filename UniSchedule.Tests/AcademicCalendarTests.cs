using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class AcademicCalendarTests
{
    private static readonly DateTime SemesterStart = new(2026, 9, 1);

    [Fact]
    public void WeekNumber_StartsAtOne_AndAdvancesEverySevenDays()
    {
        Assert.Equal(1, AcademicCalendar.GetWeekNumber(SemesterStart, SemesterStart));
        Assert.Equal(1, AcademicCalendar.GetWeekNumber(new DateTime(2026, 9, 7), SemesterStart));
        Assert.Equal(2, AcademicCalendar.GetWeekNumber(new DateTime(2026, 9, 8), SemesterStart));
        Assert.Equal(1, AcademicCalendar.GetWeekNumber(SemesterStart.AddDays(-3), SemesterStart));
    }

    [Fact]
    public void Parity_OddWeeksAreOdd()
    {
        Assert.Equal(WeekParity.Odd, AcademicCalendar.GetParity(1));
        Assert.Equal(WeekParity.Even, AcademicCalendar.GetParity(2));
    }

    [Fact]
    public void AppliesThisWeek_RespectsRangeAndParity()
    {
        var lesson = new Lesson
        {
            DayOfWeek = DayOfWeek.Tuesday,
            Parity = WeekParity.Odd,
            WeekFrom = 1,
            WeekTo = 1
        };

        Assert.True(AcademicCalendar.AppliesThisWeek(lesson, SemesterStart, SemesterStart));
        Assert.False(AcademicCalendar.AppliesThisWeek(lesson, new DateTime(2026, 9, 8), SemesterStart));
        Assert.False(AcademicCalendar.AppliesOnDate(lesson, new DateTime(2026, 9, 2), SemesterStart));
    }

    [Fact]
    public void StartOfWeek_LandsOnMonday_IncludingSunday()
    {
        Assert.Equal(new DateTime(2026, 8, 31), AcademicCalendar.StartOfWeek(new DateTime(2026, 9, 4)));
        Assert.Equal(new DateTime(2026, 8, 31), AcademicCalendar.StartOfWeek(new DateTime(2026, 9, 6)));
        Assert.Equal(new DateTime(2026, 9, 7), AcademicCalendar.StartOfWeek(new DateTime(2026, 9, 7)));
        Assert.Equal("Пятница, 4 сент.", AcademicCalendar.ColumnTitle(new DateTime(2026, 9, 4)));
        Assert.Equal("Сентябрь 2026", AcademicCalendar.MonthTitle(new DateTime(2026, 9, 4)));
    }

    [Fact]
    public void Labels_UseRussianNames()
    {
        Assert.Equal("Неделя 1 · нечётная", AcademicCalendar.WeekLabel(SemesterStart, SemesterStart));
        Assert.Equal("Понедельник", AcademicCalendar.DayName(DayOfWeek.Monday));
        Assert.Equal("Лекция", AcademicCalendar.TypeLabel(LessonCodes.Lecture));
        Assert.Equal("Зачет", AcademicCalendar.TypeLabel(LessonCodes.Credit));
        Assert.Equal("Экзамен", AcademicCalendar.TypeLabel(LessonCodes.Exam));
        Assert.Equal("", AcademicCalendar.TypeLabel("unknown"));
    }
}
