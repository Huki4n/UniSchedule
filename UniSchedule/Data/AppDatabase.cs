using Microsoft.Data.Sqlite;

namespace UniSchedule.Data;

public sealed partial class AppDatabase
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
            CREATE TABLE IF NOT EXISTS Homework (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LessonId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                Deadline TEXT NOT NULL,
                IsDone INTEGER NOT NULL DEFAULT 0,
                Url TEXT NOT NULL DEFAULT '',
                ExtraUrl TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS IX_Homework_Lesson ON Homework(LessonId);
            CREATE INDEX IF NOT EXISTS IX_Homework_Deadline ON Homework(Deadline);
            CREATE TABLE IF NOT EXISTS SubjectRollback (
                LessonId INTEGER PRIMARY KEY,
                OriginalSubject TEXT NOT NULL,
                ChangedOn TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS HomeworkComment (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                HomeworkId INTEGER NOT NULL,
                Body TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_HomeworkComment_Homework ON HomeworkComment(HomeworkId);
            """;
        cmd.ExecuteNonQuery();
        EnsureHomeworkNotificationLog(db);
        EnsureColumn(db, "Homework", "Url", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(db, "Homework", "ExtraUrl", "TEXT NOT NULL DEFAULT ''");
    }

    public void ClearStoredData()
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        foreach (var table in new[] { "HomeworkComment", "Homework", "SubjectRollback", "NotificationLog", "HomeworkNotificationLog", "Lessons" })
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = $"DELETE FROM {table}";
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void EnsureHomeworkNotificationLog(SqliteConnection db)
    {
        if (HasColumn(db, "HomeworkNotificationLog", "OffsetDays"))
        {
            using var drop = db.CreateCommand();
            drop.CommandText = "DROP TABLE HomeworkNotificationLog";
            drop.ExecuteNonQuery();
        }

        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS HomeworkNotificationLog (
                HomeworkId INTEGER NOT NULL,
                FireDate TEXT NOT NULL,
                OffsetMinutes INTEGER NOT NULL,
                PRIMARY KEY (HomeworkId, FireDate, OffsetMinutes)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection db, string table, string column, string definition)
    {
        if (HasColumn(db, table, column))
        {
            return;
        }

        using var alter = db.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        alter.ExecuteNonQuery();
    }

    private static bool HasColumn(SqliteConnection db, string table, string column)
    {
        using var info = db.CreateCommand();
        info.CommandText = $"PRAGMA table_info({table})";
        using var reader = info.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
