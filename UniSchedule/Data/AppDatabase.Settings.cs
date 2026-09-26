using System.Globalization;
using Microsoft.Data.Sqlite;
using UniSchedule.Models;

namespace UniSchedule.Data;

public sealed partial class AppDatabase
{
    public AppSettings GetSettings()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            map[reader.GetString(0)] = reader.GetString(1);
        }

        var settings = new AppSettings();
        if (map.TryGetValue("SelectedGroup", out var group) && !string.IsNullOrWhiteSpace(group))
        {
            settings.SelectedGroup = group.Trim();
        }

        if (map.TryGetValue("SemesterStart", out var start) && TryReadDate(start, out var date))
        {
            settings.SemesterStart = date.Date;
        }

        if (map.ContainsKey("ReminderMinutes"))
        {
            settings.ReminderMinutes = AppSettings.ParseReminderList(map["ReminderMinutes"]);
        }
        else
        {
            var first = 60;
            var second = 15;
            if (map.TryGetValue("FirstReminderMinutes", out var firstText) && int.TryParse(firstText, out var firstMin))
            {
                first = firstMin;
            }

            if (map.TryGetValue("SecondReminderMinutes", out var secondText) && int.TryParse(secondText, out var secondMin))
            {
                second = secondMin;
            }

            settings.ReminderMinutes = AppSettings.NormalizeReminders([first, second]);
        }

        if (map.ContainsKey("HomeworkReminderMinutes"))
        {
            var parsed = AppSettings.ParseHomeworkReminderList(map["HomeworkReminderMinutes"]);
            settings.HomeworkReminderMinutes = parsed is [AppSettings.HomeworkMonthOffset, 1]
                ? AppSettings.HomeworkReminderPresets.ToArray()
                : parsed;
        }

        settings.NotificationsEnabled = GetBool(map, "NotificationsEnabled", true);
        settings.Autostart = GetBool(map, "Autostart", false);
        settings.MinimizeToTray = GetBool(map, "MinimizeToTray", true);
        return settings;
    }

    public void SaveSettings(AppSettings settings)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        Set(db, "SelectedGroup", settings.SelectedGroup);
        Set(db, "SemesterStart", settings.SemesterStart.ToString("yyyy-MM-dd"));
        Set(db, "ReminderMinutes", AppSettings.FormatReminderList(settings.ReminderMinutes));
        Set(db, "HomeworkReminderMinutes", AppSettings.FormatHomeworkReminderList(settings.HomeworkReminderMinutes));
        using var dropDays = db.CreateCommand();
        dropDays.CommandText = "DELETE FROM Settings WHERE Key = 'HomeworkReminderDays'";
        dropDays.ExecuteNonQuery();
        Set(db, "NotificationsEnabled", settings.NotificationsEnabled ? "1" : "0");
        Set(db, "Autostart", settings.Autostart ? "1" : "0");
        Set(db, "MinimizeToTray", settings.MinimizeToTray ? "1" : "0");
        tx.Commit();
    }

    private static void Set(SqliteConnection db, string key, string value)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO Settings(Key, Value) VALUES ($k, $v)
            ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    private static bool TryReadDate(string text, out DateTime date)
    {
        if (DateTime.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            return true;
        }

        return DateTime.TryParse(text, out date);
    }

    private static bool GetBool(Dictionary<string, string> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return value is "1" or "true" or "True";
    }
}
