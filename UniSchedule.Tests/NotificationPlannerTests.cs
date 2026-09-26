using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class NotificationPlannerTests
{
    private static readonly DateTime SemesterStart = new(2026, 9, 1);

    [Fact]
    public void Collect_ReturnsOffsetInsideTwoMinuteWindow()
    {
        var lesson = DueLesson(new TimeSpan(10, 0, 0));
        var now = new DateTime(2026, 9, 4, 9, 0, 30);
        var settings = Settings(60);

        var due = NotificationPlanner.Collect([lesson], settings, now, (_, _, _) => false);

        var item = Assert.Single(due);
        Assert.Equal(60, item.OffsetMinutes);
        Assert.Same(lesson, item.Lesson);
    }

    [Fact]
    public void Collect_SkipsSentSundayDisabledAndZeroOffsets()
    {
        var lesson = DueLesson(new TimeSpan(10, 0, 0));
        var now = new DateTime(2026, 9, 4, 9, 0, 0);
        var settings = Settings(60);

        Assert.Empty(NotificationPlanner.Collect([lesson], settings, now, (_, _, _) => true));

        var sunday = new DateTime(2026, 9, 6, 9, 0, 0);
        Assert.Empty(NotificationPlanner.Collect([lesson], settings, sunday, (_, _, _) => false));

        settings.NotificationsEnabled = false;
        Assert.Empty(NotificationPlanner.Collect([lesson], settings, now, (_, _, _) => false));

        settings.NotificationsEnabled = true;
        settings.ReminderMinutes = [];
        Assert.Empty(settings.ReminderOffsets);
        Assert.Empty(NotificationPlanner.Collect([lesson], settings, now, (_, _, _) => false));
    }

    [Fact]
    public void Collect_IgnoresUnsavedLessonsAndClosedWindow()
    {
        var saved = DueLesson(new TimeSpan(10, 0, 0));
        var unsaved = DueLesson(new TimeSpan(10, 0, 0));
        unsaved.Id = 0;
        var inside = new DateTime(2026, 9, 4, 9, 0, 0);
        var outside = new DateTime(2026, 9, 4, 9, 3, 0);

        var due = NotificationPlanner.Collect([unsaved, saved], Settings(60), inside, (_, _, _) => false);
        Assert.Equal(saved.Id, Assert.Single(due).Lesson.Id);
        Assert.Empty(NotificationPlanner.Collect([saved], Settings(60), outside, (_, _, _) => false));
    }

    [Fact]
    public void ReminderOffsets_DeduplicatesAndSortsDescending()
    {
        var settings = new AppSettings { ReminderMinutes = [15, 60, 15] };
        Assert.Equal([60, 15], settings.ReminderOffsets);

        settings.ReminderMinutes = [15, 15];
        Assert.Equal([15], settings.ReminderOffsets);
    }

    private static AppSettings Settings(int first) => new()
    {
        SelectedGroup = "11-321",
        SemesterStart = SemesterStart,
        ReminderMinutes = [first],
        NotificationsEnabled = true
    };

    private static Lesson DueLesson(TimeSpan start) => new()
    {
        Id = 7,
        GroupCode = "11-321",
        DayOfWeek = DayOfWeek.Friday,
        Start = start,
        End = start.Add(TimeSpan.FromMinutes(90)),
        Subject = "Базы данных"
    };
}
