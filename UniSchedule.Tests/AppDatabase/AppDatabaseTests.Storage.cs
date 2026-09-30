using Microsoft.Data.Sqlite;
using UniSchedule.Data;
using UniSchedule.Models;

namespace UniSchedule.Tests;

public sealed partial class AppDatabaseTests
{
    [Fact]
    public void NotificationLog_IsIdempotentPerDayAndOffset()
    {
        var date = new DateTime(2026, 9, 4);
        Assert.False(_database.WasNotificationSent(3, date, 15));
        _database.MarkNotificationSent(3, date, 15);
        _database.MarkNotificationSent(3, date, 15);
        Assert.True(_database.WasNotificationSent(3, date, 15));
        Assert.False(_database.WasNotificationSent(3, date, 60));
    }

    [Fact]
    public void HomeworkNotificationLog_IsIdempotentPerDayAndOffset()
    {
        var date = new DateTime(2026, 9, 4);
        Assert.False(_database.WasHomeworkNotificationSent(4, date, 0));
        _database.MarkHomeworkNotificationSent(4, date, 0);
        _database.MarkHomeworkNotificationSent(4, date, 0);
        Assert.True(_database.WasHomeworkNotificationSent(4, date, 0));
        Assert.False(_database.WasHomeworkNotificationSent(4, date, 1));
    }

    [Fact]
    public void HomeworkNotificationLog_ReplacesDayOffsetColumn()
    {
        var path = Path.Combine(_directory, "schedule.db");
        using (
            var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path }.ToString()
            )
        )
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                DROP TABLE HomeworkNotificationLog;
                CREATE TABLE HomeworkNotificationLog (
                    HomeworkId INTEGER NOT NULL,
                    FireDate TEXT NOT NULL,
                    OffsetDays INTEGER NOT NULL,
                    PRIMARY KEY (HomeworkId, FireDate, OffsetDays)
                );
                """;
            cmd.ExecuteNonQuery();
        }

        var reopened = new AppDatabase(path);
        var date = new DateTime(2026, 9, 4);
        reopened.MarkHomeworkNotificationSent(4, date, 1);
        Assert.True(reopened.WasHomeworkNotificationSent(4, date, 1));
        Assert.False(
            reopened.WasHomeworkNotificationSent(4, date, AppSettings.HomeworkMonthOffset)
        );
    }

    [Fact]
    public void DefaultPath_IsUnderLocalAppData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniSchedule",
            "schedule.db"
        );
        Assert.Equal(expected, AppDatabase.DefaultDatabasePath());
    }

    [Fact]
    public void ImportLegacyDatabase_MovesExeFileAndSidecars()
    {
        var legacyDir = Path.Combine(_directory, "exe");
        var profileDir = Path.Combine(_directory, "profile");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(profileDir);
        var legacy = Path.Combine(legacyDir, "schedule.db");
        var destination = Path.Combine(profileDir, "schedule.db");
        File.WriteAllText(legacy, "live");
        File.WriteAllText(legacy + "-wal", "wal");
        File.WriteAllText(destination, "stale");
        File.WriteAllText(destination + "-shm", "old-shm");

        AppDatabase.ImportLegacyDatabase(legacy, destination);

        Assert.Equal("live", File.ReadAllText(destination));
        Assert.Equal("wal", File.ReadAllText(destination + "-wal"));
        Assert.False(File.Exists(destination + "-shm"));
        Assert.False(File.Exists(legacy));
        Assert.False(File.Exists(legacy + "-wal"));
    }

    [Fact]
    public void ImportLegacyDatabase_LeavesProfileWhenExeCopyIsAbsent()
    {
        var destination = Path.Combine(_directory, "kept.db");
        File.WriteAllText(destination, "profile");

        AppDatabase.ImportLegacyDatabase(Path.Combine(_directory, "missing.db"), destination);

        Assert.Equal("profile", File.ReadAllText(destination));
    }

    [Fact]
    public void SubjectRollback_KeepsFirstNameForTheCalendarDay()
    {
        var lessonId = _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Сети",
                DayOfWeek = DayOfWeek.Monday,
            }
        );
        var today = new DateTime(2026, 9, 25);
        var yesterday = today.AddDays(-1);

        _database.RememberSubjectRollback(lessonId, "Сети", today);
        _database.RememberSubjectRollback(lessonId, "Промежуточное", today);
        Assert.Equal("Сети", _database.GetSubjectRollback(lessonId, today));

        _database.RememberSubjectRollback(lessonId, "Вчера", yesterday);
        Assert.Equal("Вчера", _database.GetSubjectRollback(lessonId, yesterday));
        Assert.Null(_database.GetSubjectRollback(lessonId, today));

        _database.RememberSubjectRollback(lessonId, "Сети", today);
        _database.ForgetSubjectRollback(lessonId);
        Assert.Null(_database.GetSubjectRollback(lessonId, today));

        _database.RememberSubjectRollback(lessonId, "Сети", today);
        _database.DeleteLesson(lessonId);
        Assert.Null(_database.GetSubjectRollback(lessonId, today));
    }

    [Fact]
    public void HomeworkComment_RoundTrip_AndLeavesWithHomework()
    {
        var lessonId = _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Сети",
                DayOfWeek = DayOfWeek.Monday,
            }
        );
        var homeworkId = _database.UpsertHomework(
            new Homework
            {
                LessonId = lessonId,
                Title = "ЛР",
                Deadline = new DateTime(2026, 9, 25),
            }
        );

        _database.AddHomeworkComment(
            homeworkId,
            "  первый  ",
            new DateTime(2026, 9, 25, 13, 40, 55)
        );
        _database.AddHomeworkComment(homeworkId, "   ", new DateTime(2026, 9, 25, 14, 0, 0));
        var comment = Assert.Single(_database.GetHomeworkComments(homeworkId));
        Assert.Equal("первый", comment.Body);
        Assert.Equal(new DateTime(2026, 9, 25, 13, 40, 0), comment.CreatedAt);

        _database.DeleteHomeworkComment(comment.Id);
        Assert.Empty(_database.GetHomeworkComments(homeworkId));

        _database.AddHomeworkComment(homeworkId, "ещё", new DateTime(2026, 9, 25, 15, 0, 0));
        _database.DeleteLesson(lessonId);
        Assert.Empty(_database.GetHomeworkComments(homeworkId));
    }

    [Fact]
    public void ClearStoredData_RemovesLessonsAndHomework_KeepsSettings()
    {
        _database.SaveSettings(
            new AppSettings { SelectedGroup = "11-405", SemesterStart = new DateTime(2026, 9, 1) }
        );
        var day = new DateTime(2026, 9, 25);
        var lessonId = _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-405",
                Subject = "Сети",
                DayOfWeek = DayOfWeek.Monday,
                Source = LessonCodes.Manual,
            }
        );
        var homeworkId = _database.UpsertHomework(
            new Homework
            {
                LessonId = lessonId,
                Title = "ЛР",
                Deadline = day,
            }
        );
        _database.AddHomeworkComment(homeworkId, "сдать", day);
        _database.RememberSubjectRollback(lessonId, "Старое", day);
        _database.MarkNotificationSent(lessonId, day, 15);
        _database.MarkHomeworkNotificationSent(homeworkId, day, 1);

        _database.ClearStoredData();

        Assert.Empty(_database.GetAllLessons());
        Assert.Empty(_database.GetHomework("11-405"));
        Assert.Empty(_database.GetHomeworkComments(homeworkId));
        Assert.Null(_database.GetSubjectRollback(lessonId, day));
        Assert.False(_database.WasNotificationSent(lessonId, day, 15));
        Assert.False(_database.WasHomeworkNotificationSent(homeworkId, day, 1));
        var settings = _database.GetSettings();
        Assert.Equal("11-405", settings.SelectedGroup);
        Assert.Equal(new DateTime(2026, 9, 1), settings.SemesterStart);
    }

    [Fact]
    public void UpsertLessons_WritesTheWholeListTogether()
    {
        var first = new Lesson
        {
            GroupCode = "11-321",
            Subject = "Сети",
            DayOfWeek = DayOfWeek.Monday,
            Start = new TimeSpan(8, 30, 0),
            End = new TimeSpan(10, 0, 0),
        };
        var second = new Lesson
        {
            GroupCode = "11-321",
            Subject = "Базы",
            DayOfWeek = DayOfWeek.Tuesday,
            Start = new TimeSpan(10, 10, 0),
            End = new TimeSpan(11, 40, 0),
        };

        _database.UpsertLessons([first, second]);
        first.Subject = "Сети 2";
        second.Subject = "Базы 2";
        _database.UpsertLessons([first, second]);

        var loaded = _database.GetLessons("11-321");
        Assert.Equal(["Сети 2", "Базы 2"], loaded.Select(lesson => lesson.Subject));
        Assert.Equal(first.Id, loaded[0].Id);
        Assert.Equal(second.Id, loaded[1].Id);
    }

    [Fact]
    public void SaveHomeworkWithComments_AppliesUpsertAndCommentEditsTogether()
    {
        var lessonId = _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Сети",
                DayOfWeek = DayOfWeek.Monday,
            }
        );
        var homework = new Homework
        {
            LessonId = lessonId,
            Title = "ЛР",
            Deadline = new DateTime(2026, 9, 25),
        };
        _database.UpsertHomework(homework);
        _database.AddHomeworkComment(homework.Id, "старый", new DateTime(2026, 9, 25, 10, 0, 0));
        var keep = Assert.Single(_database.GetHomeworkComments(homework.Id));
        _database.AddHomeworkComment(homework.Id, "удалить", new DateTime(2026, 9, 25, 11, 0, 0));
        var remove = _database
            .GetHomeworkComments(homework.Id)
            .Single(comment => comment.Body == "удалить");

        homework.Title = "ЛР 2";
        _database.SaveHomeworkWithComments(
            homework,
            [remove.Id],
            [
                new HomeworkComment
                {
                    Body = "новый",
                    CreatedAt = new DateTime(2026, 9, 25, 12, 30, 55),
                },
            ]
        );

        var loaded = Assert.Single(_database.GetHomework("11-321"));
        Assert.Equal("ЛР 2", loaded.Title);
        var comments = _database.GetHomeworkComments(homework.Id);
        Assert.Equal(2, comments.Count);
        Assert.Contains(comments, comment => comment.Id == keep.Id && comment.Body == "старый");
        var added = Assert.Single(comments, comment => comment.Body == "новый");
        Assert.Equal(new DateTime(2026, 9, 25, 12, 30, 0), added.CreatedAt);
        Assert.DoesNotContain(comments, comment => comment.Id == remove.Id);
    }
}
