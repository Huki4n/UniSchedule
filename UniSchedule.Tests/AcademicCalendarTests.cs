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
    public void Labels_UseRussianNames()
    {
        Assert.Equal("Неделя 1 · нечётная", AcademicCalendar.WeekLabel(SemesterStart, SemesterStart));
        Assert.Equal("Понедельник", AcademicCalendar.DayName(DayOfWeek.Monday));
        Assert.Equal("Лекция", AcademicCalendar.TypeLabel(LessonCodes.Lecture));
        Assert.Equal("", AcademicCalendar.TypeLabel("unknown"));
    }
}
