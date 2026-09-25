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
    public void LessonForm_DeleteConfirm_MentionsHomeworkWhenPresent()
    {
        Assert.Equal("«Сети» будет удалена.", LessonForm.DeleteConfirmText("Сети", 0));
        Assert.Equal("Пара будет удалена.", LessonForm.DeleteConfirmText("  ", 0));
        Assert.Equal("«Сети» будет удалена вместе с домашками (2).", LessonForm.DeleteConfirmText(" Сети ", 2));
    }

    [Fact]
    public void HomeworkForm_RejectsMissingLessonTitleAndDeadline()
    {
        Assert.False(HomeworkForm.TryValidate(null, "ЛР", new DateTime(2026, 9, 15), out var lessonError));
        Assert.Equal(HomeworkForm.LessonError, lessonError);
        Assert.False(HomeworkForm.TryValidate(0, "ЛР", new DateTime(2026, 9, 15), out _));

        Assert.False(HomeworkForm.TryValidate(4, "  ", new DateTime(2026, 9, 15), out var titleError));
        Assert.Equal(HomeworkForm.TitleError, titleError);

        Assert.False(HomeworkForm.TryValidate(4, "ЛР", null, out var deadlineError));
        Assert.Equal(HomeworkForm.DeadlineError, deadlineError);
    }

    [Fact]
    public void HomeworkForm_AcceptsLessonTitleAndDeadline()
    {
        Assert.True(HomeworkForm.TryValidate(4, " ЛР 1 ", new DateTime(2026, 9, 15), out var error));
        Assert.Null(error);
        Assert.Equal(
            "Пятница · 10:10",
            HomeworkForm.SlotLabel(new Lesson
            {
                Subject = "Сети",
                DayOfWeek = DayOfWeek.Friday,
                Start = new TimeSpan(10, 10, 0)
            }));
    }

    [Fact]
    public void HomeworkForm_GroupsSameSubjectAndKeepsWeekOrder()
    {
        var groups = HomeworkForm.GroupBySubject(
        [
            new Lesson { Id = 1, Subject = "DevOps", DayOfWeek = DayOfWeek.Wednesday, Start = new TimeSpan(15, 50, 0) },
            new Lesson { Id = 2, Subject = "  devops ", DayOfWeek = DayOfWeek.Wednesday, Start = new TimeSpan(13, 50, 0) },
            new Lesson { Id = 3, Subject = "Сети", DayOfWeek = DayOfWeek.Monday, Start = new TimeSpan(8, 30, 0) }
        ]);

        Assert.Equal(["Сети", "devops"], groups.Select(group => HomeworkForm.SubjectTitle(group[0].Subject)).ToArray());
        Assert.Equal([2L, 1L], groups[1].Select(lesson => lesson.Id).ToArray());
        Assert.Equal("Среда · 13:50", HomeworkForm.SlotLabel(groups[1][0]));
    }

    [Fact]
    public void HomeworkForm_MergesWeekVariantsOfTheSameSubject()
    {
        Assert.Equal("Психология", HomeworkForm.SubjectTitle("Психология, с 10 нед.прак"));
        Assert.Equal("Психология", HomeworkForm.SubjectTitle("Психология, 1-10 нед. лек"));
        Assert.Equal("Конфликтология", HomeworkForm.SubjectTitle("Конфликтология, 1-9 нед.лек., с 10 нед.прак"));

        var groups = HomeworkForm.GroupBySubject(
        [
            new Lesson { Id = 1, Subject = "Психология, 1-10 нед. лек", DayOfWeek = DayOfWeek.Monday, Start = new TimeSpan(8, 30, 0), WeekFrom = 1, WeekTo = 10 },
            new Lesson { Id = 2, Subject = "Психология, 11-16 нед. лек", DayOfWeek = DayOfWeek.Monday, Start = new TimeSpan(8, 30, 0), WeekFrom = 11, WeekTo = 16 },
            new Lesson { Id = 3, Subject = "Психология, с 10 нед.прак", DayOfWeek = DayOfWeek.Wednesday, Start = new TimeSpan(10, 0, 0), WeekFrom = 10 },
            new Lesson { Id = 4, Subject = "Конфликтология, 1-9 нед.лек., с 10 нед.прак", DayOfWeek = DayOfWeek.Tuesday, Start = new TimeSpan(8, 30, 0) }
        ]);

        Assert.Equal(["Психология", "Конфликтология"], groups.Select(group => HomeworkForm.SubjectTitle(group[0].Subject)).ToArray());
        Assert.Equal([1L, 3L], HomeworkForm.DistinctSlots(groups[0], preferredId: 0).Select(lesson => lesson.Id).ToArray());
        Assert.Equal(2L, HomeworkForm.DistinctSlots(groups[0], preferredId: 2).First().Id);
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
        Assert.False(SettingsForm.TryParseReminder("-1", out _));
        Assert.False(SettingsForm.TryParseReminder("0", out _));
        Assert.False(SettingsForm.TryParseReminder("час", out _));
        Assert.True(SettingsForm.TryParseReminder("15", out var minutes));
        Assert.Equal(15, minutes);
        Assert.Equal([60, 15], AppSettings.NormalizeReminders([15, 0, 60, 15]));
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
            ReminderMinutes = [5, 30],
            NotificationsEnabled = false,
            Autostart = true,
            MinimizeToTray = false
        };

        var clone = settings.Clone();
        clone.SelectedGroup = "11-999";

        Assert.Equal("11-405", settings.SelectedGroup);
        Assert.Equal(new DateTime(2026, 2, 1), clone.SemesterStart);
        Assert.Equal([30, 5], clone.ReminderMinutes);
        clone.ReminderMinutes = [1];
        Assert.Equal([30, 5], settings.ReminderMinutes);
        Assert.False(clone.NotificationsEnabled);
        Assert.True(clone.Autostart);
        Assert.False(clone.MinimizeToTray);
    }
}
