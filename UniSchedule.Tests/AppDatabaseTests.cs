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
            FirstReminderMinutes = 45,
            SecondReminderMinutes = 10,
            NotificationsEnabled = false,
            Autostart = true,
            MinimizeToTray = false
        };

        _database.SaveSettings(settings);
        var loaded = _database.GetSettings();

        Assert.Equal("11-405", loaded.SelectedGroup);
        Assert.Equal(new DateTime(2026, 9, 1), loaded.SemesterStart);
        Assert.Equal(45, loaded.FirstReminderMinutes);
        Assert.Equal(10, loaded.SecondReminderMinutes);
        Assert.False(loaded.NotificationsEnabled);
        Assert.True(loaded.Autostart);
        Assert.False(loaded.MinimizeToTray);
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
