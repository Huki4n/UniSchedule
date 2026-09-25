using System.Globalization;
using Microsoft.Data.Sqlite;
using UniSchedule.Models;

namespace UniSchedule.Data;

public sealed class AppDatabase
{
    private readonly string _connectionString;

    public AppDatabase()
        : this(DefaultDatabasePath(), migrateLegacy: true)
    {
    }

    public AppDatabase(string databasePath)
        : this(databasePath, migrateLegacy: false)
    {
    }

    private AppDatabase(string databasePath, bool migrateLegacy)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (migrateLegacy)
        {
            ImportLegacyDatabase(LegacyDatabasePath(), databasePath);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();
        Initialize();
    }

    public static string DefaultDatabasePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniSchedule",
            "schedule.db");

    public static string LegacyDatabasePath() =>
        Path.Combine(AppContext.BaseDirectory, "schedule.db");

    internal static void ImportLegacyDatabase(string legacyPath, string destinationPath)
    {
        if (!File.Exists(legacyPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(legacyPath, destinationPath, overwrite: true);
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var from = legacyPath + suffix;
            var to = destinationPath + suffix;
            if (File.Exists(from))
            {
                File.Copy(from, to, overwrite: true);
            }
            else if (File.Exists(to))
            {
                File.Delete(to);
            }
        }

        File.Delete(legacyPath);
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var side = legacyPath + suffix;
            if (File.Exists(side))
            {
                File.Delete(side);
            }
        }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        // Column names, Settings keys and Source codes are a persisted contract.
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Lessons (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupCode TEXT NOT NULL,
                DayOfWeek INTEGER NOT NULL,
                Start TEXT NOT NULL,
                End TEXT NOT NULL,
                Subject TEXT NOT NULL,
                LessonType TEXT NOT NULL DEFAULT '',
                Teacher TEXT NOT NULL DEFAULT '',
                Room TEXT NOT NULL DEFAULT '',
                MeetingUrl TEXT NOT NULL DEFAULT '',
                LmsUrl TEXT NOT NULL DEFAULT '',
                Parity INTEGER NOT NULL DEFAULT 0,
                WeekFrom INTEGER,
                WeekTo INTEGER,
                Notes TEXT NOT NULL DEFAULT '',
                RawText TEXT NOT NULL DEFAULT '',
                Source TEXT NOT NULL DEFAULT 'manual'
            );
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS NotificationLog (
                LessonId INTEGER NOT NULL,
                FireDate TEXT NOT NULL,
                OffsetMinutes INTEGER NOT NULL,
                PRIMARY KEY (LessonId, FireDate, OffsetMinutes)
            );
            CREATE INDEX IF NOT EXISTS IX_Lessons_Group ON Lessons(GroupCode);
            """;
        cmd.ExecuteNonQuery();
    }

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

        if (map.TryGetValue("FirstReminderMinutes", out var first) && int.TryParse(first, out var firstMin))
        {
            settings.FirstReminderMinutes = firstMin;
        }

        if (map.TryGetValue("SecondReminderMinutes", out var second) && int.TryParse(second, out var secondMin))
        {
            settings.SecondReminderMinutes = secondMin;
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
        Set(db, "FirstReminderMinutes", settings.FirstReminderMinutes.ToString());
        Set(db, "SecondReminderMinutes", settings.SecondReminderMinutes.ToString());
        Set(db, "NotificationsEnabled", settings.NotificationsEnabled ? "1" : "0");
        Set(db, "Autostart", settings.Autostart ? "1" : "0");
        Set(db, "MinimizeToTray", settings.MinimizeToTray ? "1" : "0");
        tx.Commit();
    }

    public List<string> GetGroups()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT GroupCode FROM Lessons ORDER BY GroupCode";
        var groups = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            groups.Add(reader.GetString(0));
        }

        return groups;
    }

    public List<Lesson> GetLessons(string groupCode)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            SELECT Id, GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                   MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source
            FROM Lessons
            WHERE GroupCode = $group
            ORDER BY DayOfWeek, Start
            """;
        cmd.Parameters.AddWithValue("$group", groupCode);
        return ReadLessons(cmd);
    }

    public List<Lesson> GetAllLessons()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            SELECT Id, GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                   MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source
            FROM Lessons
            ORDER BY GroupCode, DayOfWeek, Start
            """;
        return ReadLessons(cmd);
    }

    public long UpsertLesson(Lesson lesson)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        if (lesson.Id > 0)
        {
            cmd.CommandText =
                """
                UPDATE Lessons SET
                    GroupCode=$group, DayOfWeek=$day, Start=$start, End=$end, Subject=$subject,
                    LessonType=$type, Teacher=$teacher, Room=$room, MeetingUrl=$meet, LmsUrl=$lms,
                    Parity=$parity, WeekFrom=$from, WeekTo=$to, Notes=$notes, RawText=$raw, Source=$source
                WHERE Id=$id
                """;
            cmd.Parameters.AddWithValue("$id", lesson.Id);
        }
        else
        {
            cmd.CommandText =
                """
                INSERT INTO Lessons (
                    GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                    MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source)
                VALUES (
                    $group, $day, $start, $end, $subject, $type, $teacher, $room,
                    $meet, $lms, $parity, $from, $to, $notes, $raw, $source);
                SELECT last_insert_rowid();
                """;
        }

        BindLesson(cmd, lesson);
        var result = cmd.ExecuteScalar();
        if (lesson.Id <= 0 && result is not null)
        {
            lesson.Id = Convert.ToInt64(result);
        }

        return lesson.Id;
    }

    public void DeleteLesson(long id)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM Lessons WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public int ReplaceImported(IReadOnlyList<Lesson> lessons)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using (var clear = db.CreateCommand())
        {
            clear.CommandText = "DELETE FROM Lessons WHERE Source='imported'";
            clear.ExecuteNonQuery();
        }

        foreach (var lesson in lessons)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO Lessons (
                    GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                    MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source)
                VALUES (
                    $group, $day, $start, $end, $subject, $type, $teacher, $room,
                    $meet, $lms, $parity, $from, $to, $notes, $raw, $source)
                """;
            lesson.Source = LessonCodes.Imported;
            BindLesson(cmd, lesson);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return lessons.Count;
    }

    public bool WasNotificationSent(long lessonId, DateTime date, int offsetMinutes)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            SELECT 1 FROM NotificationLog
            WHERE LessonId=$id AND FireDate=$date AND OffsetMinutes=$off
            """;
        cmd.Parameters.AddWithValue("$id", lessonId);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$off", offsetMinutes);
        return cmd.ExecuteScalar() is not null;
    }

    public void MarkNotificationSent(long lessonId, DateTime date, int offsetMinutes)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            INSERT OR IGNORE INTO NotificationLog (LessonId, FireDate, OffsetMinutes)
            VALUES ($id, $date, $off)
            """;
        cmd.Parameters.AddWithValue("$id", lessonId);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$off", offsetMinutes);
        cmd.ExecuteNonQuery();
    }

    private static List<Lesson> ReadLessons(SqliteCommand cmd)
    {
        var list = new List<Lesson>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Lesson
            {
                Id = reader.GetInt64(0),
                GroupCode = reader.GetString(1),
                DayOfWeek = (DayOfWeek)reader.GetInt32(2),
                Start = TimeSpan.Parse(reader.GetString(3)),
                End = TimeSpan.Parse(reader.GetString(4)),
                Subject = reader.GetString(5),
                LessonType = reader.GetString(6),
                Teacher = reader.GetString(7),
                Room = reader.GetString(8),
                MeetingUrl = reader.GetString(9),
                LmsUrl = reader.GetString(10),
                Parity = (WeekParity)reader.GetInt32(11),
                WeekFrom = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                WeekTo = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                Notes = reader.GetString(14),
                RawText = reader.GetString(15),
                Source = reader.GetString(16)
            });
        }

        return list;
    }

    private static void BindLesson(SqliteCommand cmd, Lesson lesson)
    {
        cmd.Parameters.AddWithValue("$group", lesson.GroupCode);
        cmd.Parameters.AddWithValue("$day", (int)lesson.DayOfWeek);
        cmd.Parameters.AddWithValue("$start", lesson.Start.ToString(@"hh\:mm"));
        cmd.Parameters.AddWithValue("$end", lesson.End.ToString(@"hh\:mm"));
        cmd.Parameters.AddWithValue("$subject", lesson.Subject);
        cmd.Parameters.AddWithValue("$type", lesson.LessonType);
        cmd.Parameters.AddWithValue("$teacher", lesson.Teacher);
        cmd.Parameters.AddWithValue("$room", lesson.Room);
        cmd.Parameters.AddWithValue("$meet", lesson.MeetingUrl);
        cmd.Parameters.AddWithValue("$lms", lesson.LmsUrl);
        cmd.Parameters.AddWithValue("$parity", (int)lesson.Parity);
        cmd.Parameters.AddWithValue("$from", lesson.WeekFrom is int from ? from : DBNull.Value);
        cmd.Parameters.AddWithValue("$to", lesson.WeekTo is int to ? to : DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", lesson.Notes);
        cmd.Parameters.AddWithValue("$raw", lesson.RawText);
        cmd.Parameters.AddWithValue("$source", lesson.Source);
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
