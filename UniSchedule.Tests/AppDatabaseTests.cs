using Microsoft.Data.Sqlite;
using UniSchedule.Data;
using UniSchedule.Models;

namespace UniSchedule.Tests;

public sealed class AppDatabaseTests : IDisposable
{
    private readonly string _directory;
    private readonly AppDatabase _database;

    public AppDatabaseTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "unischedule-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _database = new AppDatabase(Path.Combine(_directory, "schedule.db"));
    }

    [Fact]
    public void Settings_RoundTrip_KeepsStoredDateFormat()
    {
        var settings = new AppSettings
        {
            SelectedGroup = "11-405",
            SemesterStart = new DateTime(2026, 9, 1),
            ReminderMinutes = [10, 45, 10, 5],
            NotificationsEnabled = false,
            Autostart = true,
            MinimizeToTray = false
        };

        _database.SaveSettings(settings);
        var loaded = _database.GetSettings();

        Assert.Equal("11-405", loaded.SelectedGroup);
        Assert.Equal(new DateTime(2026, 9, 1), loaded.SemesterStart);
        Assert.Equal([45, 10, 5], loaded.ReminderMinutes);
        Assert.False(loaded.NotificationsEnabled);
        Assert.True(loaded.Autostart);
        Assert.False(loaded.MinimizeToTray);
    }

    [Fact]
    public void Settings_MissingReminderList_UsesLegacyMinutes()
    {
        var path = Path.Combine(_directory, "schedule.db");
        WriteSetting(path, "FirstReminderMinutes", "45");
        WriteSetting(path, "SecondReminderMinutes", "0");

        Assert.Equal([45], _database.GetSettings().ReminderMinutes);

        WriteSetting(path, "ReminderMinutes", "");
        Assert.Empty(_database.GetSettings().ReminderMinutes);
    }

    private static void WriteSetting(string path, string key, string value)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO Settings(Key, Value) VALUES ($k, $v)
            ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Lessons_RoundTrip_DropsSecondsAndKeepsNullWeeks()
    {
        var lesson = new Lesson
        {
            GroupCode = "11-321",
            DayOfWeek = DayOfWeek.Wednesday,
            Start = new TimeSpan(8, 30, 45),
            End = new TimeSpan(10, 0, 15),
            Subject = "Алгоритмы",
            LessonType = LessonCodes.Practice,
            Teacher = "Иванов И.И.",
            Room = "1301",
            MeetingUrl = "https://telemost.yandex.ru/j/1",
            LmsUrl = "https://edu.kpfu.ru/course/1",
            Parity = WeekParity.Even,
            WeekFrom = 2,
            WeekTo = null,
            Notes = "перенос",
            RawText = "исходный текст",
            Source = LessonCodes.Manual
        };

        var id = _database.UpsertLesson(lesson);
        var loaded = Assert.Single(_database.GetLessons("11-321"));

        Assert.Equal(id, loaded.Id);
        Assert.Equal(new TimeSpan(8, 30, 0), loaded.Start);
        Assert.Equal(new TimeSpan(10, 0, 0), loaded.End);
        Assert.Equal(LessonCodes.Practice, loaded.LessonType);
        Assert.Equal(WeekParity.Even, loaded.Parity);
        Assert.Equal(2, loaded.WeekFrom);
        Assert.Null(loaded.WeekTo);
        Assert.Equal("исходный текст", loaded.RawText);
        Assert.Equal(LessonCodes.Manual, loaded.Source);

        loaded.Subject = "Алгоритмы и структуры";
        _database.UpsertLesson(loaded);
        Assert.Equal("Алгоритмы и структуры", Assert.Single(_database.GetLessons("11-321")).Subject);

        _database.DeleteLesson(id);
        Assert.Empty(_database.GetLessons("11-321"));
    }

    [Fact]
    public void ReplaceImported_RemovesOnlyImportedRows()
    {
        _database.UpsertLesson(new Lesson
        {
            GroupCode = "11-321",
            Subject = "Ручная",
            Source = LessonCodes.Manual,
            DayOfWeek = DayOfWeek.Monday
        });

        var count = _database.ReplaceImported(
        [
            new Lesson
            {
                GroupCode = "11-405",
                Subject = "Импорт",
                DayOfWeek = DayOfWeek.Tuesday,
                Source = LessonCodes.Manual
            }
        ]);

        Assert.Equal(1, count);
        Assert.Equal(["11-321", "11-405"], _database.GetGroups());
        Assert.Equal(LessonCodes.Manual, Assert.Single(_database.GetLessons("11-321")).Source);
        Assert.Equal(LessonCodes.Imported, Assert.Single(_database.GetLessons("11-405")).Source);

        _database.ReplaceImported([]);
        Assert.Single(_database.GetAllLessons());
        Assert.Equal("Ручная", _database.GetAllLessons()[0].Subject);
    }

    [Fact]
    public void Homework_RoundTrip_AndDeleteLessonRemovesIt()
    {
        var lessonId = _database.UpsertLesson(new Lesson
        {
            GroupCode = "11-321",
            Subject = "Сети",
            DayOfWeek = DayOfWeek.Friday,
            Start = new TimeSpan(10, 10, 0)
        });
        var id = _database.UpsertHomework(new Homework
        {
            LessonId = lessonId,
            Title = "  ЛР 1  ",
            Description = "  отчёт  ",
            Url = " https://edu.kpfu.ru/1 ",
            ExtraUrl = " https://disk.yandex.ru/1 ",
            Deadline = new DateTime(2026, 9, 15, 18, 30, 0),
            IsDone = true
        });

        var loaded = Assert.Single(_database.GetHomework("11-321"));
        Assert.Equal(id, loaded.Id);
        Assert.Equal(lessonId, loaded.LessonId);
        Assert.Equal("ЛР 1", loaded.Title);
        Assert.Equal("отчёт", loaded.Description);
        Assert.Equal("https://edu.kpfu.ru/1", loaded.Url);
        Assert.Equal("https://disk.yandex.ru/1", loaded.ExtraUrl);
        Assert.Equal(new DateTime(2026, 9, 15), loaded.Deadline);
        Assert.True(loaded.IsDone);
        Assert.Equal(1, _database.CountHomework(lessonId));

        loaded.Title = "ЛР 2";
        loaded.IsDone = false;
        _database.UpsertHomework(loaded);
        Assert.Equal("ЛР 2", Assert.Single(_database.GetHomework("11-321")).Title);
        Assert.False(_database.GetHomework("11-321")[0].IsDone);

        _database.DeleteLesson(lessonId);
        Assert.Empty(_database.GetHomework("11-321"));
        Assert.Equal(0, _database.CountHomework(lessonId));
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
            Source = LessonCodes.Imported
        };
        var manual = new Lesson
        {
            GroupCode = "11-321",
            Subject = "Ручная",
            DayOfWeek = DayOfWeek.Tuesday,
            Start = new TimeSpan(10, 0, 0),
            Source = LessonCodes.Manual
        };
        _database.UpsertLesson(imported);
        _database.UpsertLesson(manual);
        _database.UpsertHomework(new Homework
        {
            LessonId = imported.Id,
            Title = "Импорт",
            Deadline = new DateTime(2026, 9, 15)
        });
        _database.UpsertHomework(new Homework
        {
            LessonId = manual.Id,
            Title = "Своя",
            Deadline = new DateTime(2026, 9, 16)
        });
        var day = new DateTime(2026, 9, 25);
        _database.RememberSubjectRollback(imported.Id, "Старые базы", day);
        _database.RememberSubjectRollback(manual.Id, "Старая ручная", day);

        _database.ReplaceImported(
        [
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Базы",
                DayOfWeek = DayOfWeek.Monday,
                Start = new TimeSpan(8, 30, 45),
                Source = LessonCodes.Manual
            }
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

        _database.ReplaceImported([]);
        Assert.Equal(["Своя"], _database.GetHomework("11-321").Select(item => item.Title).ToArray());
        Assert.Equal("Ручная", Assert.Single(_database.GetLessons("11-321")).Subject);
    }

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
    public void DefaultPath_IsUnderLocalAppData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniSchedule",
            "schedule.db");
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
        var lessonId = _database.UpsertLesson(new Lesson
        {
            GroupCode = "11-321",
            Subject = "Сети",
            DayOfWeek = DayOfWeek.Monday
        });
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
        var lessonId = _database.UpsertLesson(new Lesson
        {
            GroupCode = "11-321",
            Subject = "Сети",
            DayOfWeek = DayOfWeek.Monday
        });
        var homeworkId = _database.UpsertHomework(new Homework
        {
            LessonId = lessonId,
            Title = "ЛР",
            Deadline = new DateTime(2026, 9, 25)
        });

        _database.AddHomeworkComment(homeworkId, "  первый  ", new DateTime(2026, 9, 25, 13, 40, 55));
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
        _database.SaveSettings(new AppSettings
        {
            SelectedGroup = "11-405",
            SemesterStart = new DateTime(2026, 9, 1)
        });
        var day = new DateTime(2026, 9, 25);
        var lessonId = _database.UpsertLesson(new Lesson
        {
            GroupCode = "11-405",
            Subject = "Сети",
            DayOfWeek = DayOfWeek.Monday,
            Source = LessonCodes.Manual
        });
        var homeworkId = _database.UpsertHomework(new Homework
        {
            LessonId = lessonId,
            Title = "ЛР",
            Deadline = day
        });
        _database.AddHomeworkComment(homeworkId, "сдать", day);
        _database.RememberSubjectRollback(lessonId, "Старое", day);
        _database.MarkNotificationSent(lessonId, day, 15);

        _database.ClearStoredData();

        Assert.Empty(_database.GetAllLessons());
        Assert.Empty(_database.GetHomework("11-405"));
        Assert.Empty(_database.GetHomeworkComments(homeworkId));
        Assert.Null(_database.GetSubjectRollback(lessonId, day));
        Assert.False(_database.WasNotificationSent(lessonId, day, 15));
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
            End = new TimeSpan(10, 0, 0)
        };
        var second = new Lesson
        {
            GroupCode = "11-321",
            Subject = "Базы",
            DayOfWeek = DayOfWeek.Tuesday,
            Start = new TimeSpan(10, 10, 0),
            End = new TimeSpan(11, 40, 0)
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

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
