using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class FormRulesTests
{
    [Fact]
    public void LessonForm_RejectsEmptySubjectAndInvertedTime()
    {
        Assert.False(LessonForm.TryValidate("  ", "08:30", "10:00", out _, out _, out var subjectError));
        Assert.Equal(LessonForm.SubjectError, subjectError);

        Assert.False(LessonForm.TryValidate("Сети", "10:00", "08:30", out _, out _, out var timeError));
        Assert.Equal(LessonForm.TimeError, timeError);
    }

    [Fact]
    public void LessonForm_AcceptsClockTime()
    {
        Assert.True(LessonForm.TryValidate("Сети", "8:30", "10:00", out var start, out var end, out var error));
        Assert.Null(error);
        Assert.Equal(new TimeSpan(8, 30, 0), start);
        Assert.Equal(new TimeSpan(10, 0, 0), end);
    }

    [Fact]
    public void SettingsForm_NormalizesEmptyFields_AndRejectsNegativeReminders()
    {
        Assert.False(SettingsForm.TryParseReminders("-1", "15", out _, out _));
        Assert.False(SettingsForm.TryParseReminders("час", "15", out _, out _));
        Assert.True(SettingsForm.TryParseReminders("0", "15", out var first, out var second));
        Assert.Equal(0, first);
        Assert.Equal(15, second);
        Assert.Equal(AppSettings.DefaultGroupCode, SettingsForm.NormalizeGroup("  "));
        Assert.Equal("11-405", SettingsForm.NormalizeGroup(" 11-405 "));
        Assert.Equal(AppSettings.DefaultSemesterStart, SettingsForm.NormalizeSemesterStart(null));
    }

    [Fact]
    public void SettingsClone_CopiesFieldsWithoutSharingLaterEdits()
    {
        var settings = new AppSettings
        {
            SelectedGroup = "11-405",
            SemesterStart = new DateTime(2026, 2, 1),
            FirstReminderMinutes = 30,
            SecondReminderMinutes = 5,
            NotificationsEnabled = false,
            Autostart = true,
            MinimizeToTray = false
        };

        var clone = settings.Clone();
        clone.SelectedGroup = "11-999";

        Assert.Equal("11-405", settings.SelectedGroup);
        Assert.Equal(new DateTime(2026, 2, 1), clone.SemesterStart);
        Assert.Equal(30, clone.FirstReminderMinutes);
        Assert.Equal(5, clone.SecondReminderMinutes);
        Assert.False(clone.NotificationsEnabled);
        Assert.True(clone.Autostart);
        Assert.False(clone.MinimizeToTray);
    }
}
