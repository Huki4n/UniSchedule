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
    public void CollectHomework_FiresOneMinuteBeforeMidnightAndAMonthEarlier()
    {
        var homework = DueHomework(new DateTime(2026, 9, 6));
        var settings = Settings(60);
        settings.HomeworkReminderMinutes = [AppSettings.HomeworkMonthOffset, 1];
        var minute = new DateTime(2026, 9, 6, 23, 59, 0);
        var month = new DateTime(2026, 8, 7, 0, 0, 30);

        var atMinute = Assert.Single(NotificationPlanner.CollectHomework([homework], settings, minute, (_, _, _) => false));
        Assert.Equal(1, atMinute.OffsetMinutes);
        Assert.Equal(minute.Date, atMinute.FireDate);

        var atMonth = Assert.Single(NotificationPlanner.CollectHomework([homework], settings, month, (_, _, _) => false));
        Assert.Equal(AppSettings.HomeworkMonthOffset, atMonth.OffsetMinutes);
        Assert.Equal(month.Date, atMonth.FireDate);
        Assert.Equal("через 1 мин.", NotificationPlanner.HomeworkDueText(1));
        Assert.Equal("через месяц", NotificationPlanner.HomeworkDueText(AppSettings.HomeworkMonthOffset));
        Assert.Equal("через 7 дней", NotificationPlanner.HomeworkDueText(AppSettings.HomeworkPresetWeek));
        Assert.Equal("через 5 дней", NotificationPlanner.HomeworkDueText(AppSettings.HomeworkPresetFiveDays));
        Assert.Equal("через 3 дня", NotificationPlanner.HomeworkDueText(AppSettings.HomeworkPresetThreeDays));
        Assert.Equal("через 1 день", NotificationPlanner.HomeworkDueText(AppSettings.HomeworkPresetDay));
        Assert.Equal("через 12 часов", NotificationPlanner.HomeworkDueText(AppSettings.HomeworkPresetTwelveHours));
        Assert.Equal("через 4 часа", NotificationPlanner.HomeworkDueText(AppSettings.HomeworkPresetFourHours));
    }

    [Fact]
    public void CollectHomework_SkipsDoneSentAndClosedWindow()
    {
        var homework = DueHomework(new DateTime(2026, 9, 4));
        var settings = Settings(60);
        settings.HomeworkReminderMinutes = [1];
        var now = new DateTime(2026, 9, 4, 23, 59, 0);

        var due = Assert.Single(NotificationPlanner.CollectHomework([homework], settings, now, (_, _, _) => false));
        Assert.Equal(1, due.OffsetMinutes);

        Assert.Empty(NotificationPlanner.CollectHomework([homework], settings, now, (_, _, _) => true));

        homework.IsDone = true;
        Assert.Empty(NotificationPlanner.CollectHomework([homework], settings, now, (_, _, _) => false));

        homework.IsDone = false;
        homework.Id = 0;
        Assert.Empty(NotificationPlanner.CollectHomework([homework], settings, now, (_, _, _) => false));

        homework.Id = 8;
        Assert.Empty(NotificationPlanner.CollectHomework([homework], settings, now.AddMinutes(3), (_, _, _) => false));

        settings.NotificationsEnabled = false;
        Assert.Empty(NotificationPlanner.CollectHomework([homework], settings, now, (_, _, _) => false));

        settings.NotificationsEnabled = true;
        settings.HomeworkReminderMinutes = [];
        Assert.Empty(NotificationPlanner.CollectHomework([homework], settings, now, (_, _, _) => false));
    }

    [Fact]
    public void ReminderOffsets_DeduplicatesAndSortsDescending()
    {
        var settings = new AppSettings { ReminderMinutes = [15, 60, 15] };
        Assert.Equal([60, 15], settings.ReminderOffsets);

        settings.ReminderMinutes = [15, 15];
        Assert.Equal([15], settings.ReminderOffsets);
        Assert.Equal(AppSettings.HomeworkReminderPresets, new AppSettings().HomeworkReminderMinutes);
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

    private static Homework DueHomework(DateTime deadline) => new()
    {
        Id = 8,
        LessonId = 7,
        Title = "ЛР",
        Deadline = deadline
    };
}
