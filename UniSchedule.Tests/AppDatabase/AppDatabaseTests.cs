using Microsoft.Data.Sqlite;
using UniSchedule.Data;
using UniSchedule.Models;

namespace UniSchedule.Tests;

public sealed partial class AppDatabaseTests : IDisposable
{
    private readonly string _directory;
    private readonly AppDatabase _database;

    public AppDatabaseTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "unischedule-tests",
            Guid.NewGuid().ToString("N")
        );
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
            HomeworkReminderMinutes = [15, AppSettings.HomeworkMonthOffset, 15],
            NotificationsEnabled = false,
            Autostart = true,
            MinimizeToTray = false,
        };

        _database.SaveSettings(settings);
        var loaded = _database.GetSettings();

        Assert.Equal("11-405", loaded.SelectedGroup);
        Assert.Equal(new DateTime(2026, 9, 1), loaded.SemesterStart);
        Assert.Equal([45, 10, 5], loaded.ReminderMinutes);
        Assert.Equal([AppSettings.HomeworkMonthOffset, 15], loaded.HomeworkReminderMinutes);
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

    [Fact]
    public void Settings_MissingHomeworkReminderMinutes_DefaultsToPresets()
    {
        Assert.Equal(
            AppSettings.HomeworkReminderPresets,
            _database.GetSettings().HomeworkReminderMinutes
        );

        var path = Path.Combine(_directory, "schedule.db");
        WriteSetting(path, "HomeworkReminderDays", "3");
        Assert.Equal(
            AppSettings.HomeworkReminderPresets,
            _database.GetSettings().HomeworkReminderMinutes
        );

        WriteSetting(path, "HomeworkReminderMinutes", "");
        Assert.Empty(_database.GetSettings().HomeworkReminderMinutes);

        WriteSetting(path, "HomeworkReminderMinutes", "месяц,1,0");
        Assert.Equal(
            AppSettings.HomeworkReminderPresets,
            _database.GetSettings().HomeworkReminderMinutes
        );

        WriteSetting(path, "HomeworkReminderMinutes", "месяц,15");
        Assert.Equal(
            [AppSettings.HomeworkMonthOffset, 15],
            _database.GetSettings().HomeworkReminderMinutes
        );

        _database.SaveSettings(new AppSettings());
        Assert.Null(ReadSetting(path, "HomeworkReminderDays"));
        Assert.Equal(
            AppSettings.FormatHomeworkReminderList(AppSettings.HomeworkReminderPresets),
            ReadSetting(path, "HomeworkReminderMinutes")
        );
    }

    private static void WriteSetting(string path, string key, string value)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path }.ToString()
        );
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Settings(Key, Value) VALUES ($k, $v)
            ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    private static string? ReadSetting(string path, string key)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path }.ToString()
        );
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException) { }
    }
}
