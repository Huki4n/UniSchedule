using UniSchedule.Models;

namespace UniSchedule.Services;

public enum LessonEditScope
{
    OnlyThis,
    ThisAndFollowing,
    All
}

public static class LessonSeries
{
    public static bool SameName(Lesson left, Lesson right) =>
        string.Equals(left.GroupCode, right.GroupCode, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Subject.Trim(), right.Subject.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool HasOthers(IReadOnlyList<Lesson> lessons, Lesson current) =>
        lessons.Any(item => item.Id != current.Id && SameName(item, current));

    public static bool IsAfter(Lesson current, Lesson other)
    {
        var currentDay = DayOrder(current.DayOfWeek);
        var otherDay = DayOrder(other.DayOfWeek);
        if (otherDay != currentDay)
        {
            return otherDay > currentDay;
        }

        return other.Start > current.Start;
    }

    public static IReadOnlyList<Lesson> Select(IReadOnlyList<Lesson> lessons, Lesson original, LessonEditScope scope)
    {
        if (scope == LessonEditScope.OnlyThis)
        {
            return [];
        }

        return lessons
            .Where(item => item.Id != original.Id && SameName(item, original))
            .Where(item => scope == LessonEditScope.All || IsAfter(original, item))
            .ToList();
    }

    public static void CopyShared(Lesson from, Lesson to)
    {
        to.Subject = from.Subject;
        to.LessonType = from.LessonType;
        to.Teacher = from.Teacher;
        to.Room = from.Room;
        to.MeetingUrl = from.MeetingUrl;
        to.LmsUrl = from.LmsUrl;
        to.Notes = from.Notes;
        to.Parity = from.Parity;
        to.WeekFrom = from.WeekFrom;
        to.WeekTo = from.WeekTo;
    }

    private static int DayOrder(DayOfWeek day) => ((int)day - (int)DayOfWeek.Monday + 7) % 7;
}
