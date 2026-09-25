using UniSchedule.Models;

namespace UniSchedule.Services;

public static class AcademicCalendar
{
    public static int GetWeekNumber(DateTime date, DateTime semesterStart)
    {
        var days = (date.Date - semesterStart.Date).Days;
        return days < 0 ? 1 : days / 7 + 1;
    }

    public static WeekParity GetParity(int weekNumber) =>
        weekNumber % 2 == 1 ? WeekParity.Odd : WeekParity.Even;

    public static bool AppliesThisWeek(Lesson lesson, DateTime date, DateTime semesterStart)
    {
        var week = GetWeekNumber(date, semesterStart);
        if (lesson.WeekFrom is int from && week < from)
        {
            return false;
        }

        if (lesson.WeekTo is int to && week > to)
        {
            return false;
        }

        return lesson.Parity switch
        {
            WeekParity.Odd => week % 2 == 1,
            WeekParity.Even => week % 2 == 0,
            _ => true
        };
    }

    public static bool AppliesOnDate(Lesson lesson, DateTime date, DateTime semesterStart) =>
        lesson.DayOfWeek == date.DayOfWeek && AppliesThisWeek(lesson, date, semesterStart);

    public static string WeekLabel(DateTime date, DateTime semesterStart)
    {
        var week = GetWeekNumber(date, semesterStart);
        var parity = GetParity(week) == WeekParity.Odd ? "нечётная" : "чётная";
        return $"Неделя {week} · {parity}";
    }

    public static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Понедельник",
        DayOfWeek.Tuesday => "Вторник",
        DayOfWeek.Wednesday => "Среда",
        DayOfWeek.Thursday => "Четверг",
        DayOfWeek.Friday => "Пятница",
        DayOfWeek.Saturday => "Суббота",
        _ => "Воскресенье"
    };

    public static string DayShort(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Пн",
        DayOfWeek.Tuesday => "Вт",
        DayOfWeek.Wednesday => "Ср",
        DayOfWeek.Thursday => "Чт",
        DayOfWeek.Friday => "Пт",
        DayOfWeek.Saturday => "Сб",
        _ => "Вс"
    };

    public static string TypeLabel(string type) => type switch
    {
        LessonCodes.Lecture => "Лекция",
        LessonCodes.Practice => "Практика",
        LessonCodes.Lab => "Лабораторная",
        _ => ""
    };

    public static string ParityLabel(WeekParity parity) => parity switch
    {
        WeekParity.Odd => "нечётная",
        WeekParity.Even => "чётная",
        _ => "все недели"
    };
}
