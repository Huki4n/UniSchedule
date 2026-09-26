using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class LessonSeriesTests
{
    [Fact]
    public void Select_FollowingKeepsLaterSlots_AllCopiesSharedFields()
    {
        var monday = Lesson("Сети", DayOfWeek.Monday, 8, 30);
        monday.Id = 1;
        var laterMonday = Lesson("Сети", DayOfWeek.Monday, 10, 10);
        laterMonday.Id = 2;
        var friday = Lesson("сети", DayOfWeek.Friday, 8, 30);
        friday.Id = 3;
        var other = Lesson("Базы", DayOfWeek.Tuesday, 8, 30);
        other.Id = 4;
        var lessons = new List<Lesson> { monday, laterMonday, friday, other };

        Assert.True(LessonSeries.HasOthers(lessons, monday));
        Assert.Equal([2, 3], LessonSeries.Select(lessons, monday, LessonEditScope.All).Select(item => item.Id).ToArray());
        Assert.Equal([2, 3], LessonSeries.Select(lessons, monday, LessonEditScope.ThisAndFollowing).Select(item => item.Id).ToArray());
        Assert.Equal([3], LessonSeries.Select(lessons, laterMonday, LessonEditScope.ThisAndFollowing).Select(item => item.Id).ToArray());
        Assert.Empty(LessonSeries.Select(lessons, monday, LessonEditScope.OnlyThis));
        // After a sibling is deleted from the board, Select must not keep its id.
        Assert.Equal(
            [3],
            LessonSeries.Select([monday, friday, other], monday, LessonEditScope.All).Select(item => item.Id).ToArray());

        monday.Teacher = "Иванов";
        monday.Subject = "Сети и связь";
        monday.DayOfWeek = DayOfWeek.Wednesday;
        LessonSeries.CopyShared(monday, friday);

        Assert.Equal("Сети и связь", friday.Subject);
        Assert.Equal("Иванов", friday.Teacher);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);
        Assert.Equal("Базы", other.Subject);
    }

    private static Lesson Lesson(string subject, DayOfWeek day, int hour, int minute) => new()
    {
        GroupCode = "11-321",
        Subject = subject,
        DayOfWeek = day,
        Start = new TimeSpan(hour, minute, 0),
        End = new TimeSpan(hour + 1, minute, 0)
    };
}