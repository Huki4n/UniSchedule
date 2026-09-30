using UniSchedule.Models;

namespace UniSchedule.Tests;

public sealed partial class AppDatabaseTests
{
    [Fact]
    public void SetHomeworkDone_UpdatesOnlyTheFlag()
    {
        var lessonId = _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Сети",
                DayOfWeek = DayOfWeek.Friday,
                Start = new TimeSpan(10, 10, 0),
            }
        );
        var id = _database.UpsertHomework(
            new Homework
            {
                LessonId = lessonId,
                Title = "ЛР 1",
                Description = "отчёт",
                Deadline = new DateTime(2026, 9, 15),
            }
        );

        _database.SetHomeworkDone(id, true);
        var done = Assert.Single(_database.GetHomework("11-321"));
        Assert.True(done.IsDone);
        Assert.Equal("ЛР 1", done.Title);
        Assert.Equal("отчёт", done.Description);

        _database.SetHomeworkDone(id, false);
        Assert.False(Assert.Single(_database.GetHomework("11-321")).IsDone);
        _database.SetHomeworkDone(0, true);
        Assert.False(Assert.Single(_database.GetHomework("11-321")).IsDone);
    }

    [Fact]
    public void ReplaceImported_RebindsHomeworkAndDropsMissingLessons()
    {
        var imported = new Lesson
        {
            GroupCode = "11-321",
            Subject = "Базы",
            DayOfWeek = DayOfWeek.Monday,
            Start = new TimeSpan(8, 30, 0),
            Source = LessonCodes.Imported,
        };
        var manual = new Lesson
        {
            GroupCode = "11-321",
            Subject = "Ручная",
            DayOfWeek = DayOfWeek.Tuesday,
            Start = new TimeSpan(10, 0, 0),
            Source = LessonCodes.Manual,
        };
        _database.UpsertLesson(imported);
        _database.UpsertLesson(manual);
        _database.UpsertHomework(
            new Homework
            {
                LessonId = imported.Id,
                Title = "Импорт",
                Deadline = new DateTime(2026, 9, 15),
            }
        );
        _database.UpsertHomework(
            new Homework
            {
                LessonId = manual.Id,
                Title = "Своя",
                Deadline = new DateTime(2026, 9, 16),
            }
        );
        var day = new DateTime(2026, 9, 25);
        _database.RememberSubjectRollback(imported.Id, "Старые базы", day);
        _database.RememberSubjectRollback(manual.Id, "Старая ручная", day);

        _database.ReplaceImported([
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Базы",
                DayOfWeek = DayOfWeek.Monday,
                Start = new TimeSpan(8, 30, 45),
                Source = LessonCodes.Manual,
            },
        ]);

        var lessons = _database.GetLessons("11-321");
        var bases = Assert.Single(lessons, lesson => lesson.Subject == "Базы");
        Assert.NotEqual(imported.Id, bases.Id);
        var homework = _database.GetHomework("11-321");
        Assert.Equal(bases.Id, Assert.Single(homework, item => item.Title == "Импорт").LessonId);
        Assert.Equal(manual.Id, Assert.Single(homework, item => item.Title == "Своя").LessonId);
        Assert.Null(_database.GetSubjectRollback(imported.Id, day));
        Assert.Null(_database.GetSubjectRollback(bases.Id, day));
        Assert.Equal("Старая ручная", _database.GetSubjectRollback(manual.Id, day));

        _database.ReplaceImported([], ["11-321"]);
        Assert.Equal(
            ["Своя"],
            _database.GetHomework("11-321").Select(item => item.Title).ToArray()
        );
        Assert.Equal("Ручная", Assert.Single(_database.GetLessons("11-321")).Subject);
    }
}
