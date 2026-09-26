using Microsoft.Data.Sqlite;

namespace UniSchedule.Data;

public sealed partial class AppDatabase
{
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

    public bool WasHomeworkNotificationSent(long homeworkId, DateTime date, int offsetMinutes)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            SELECT 1 FROM HomeworkNotificationLog
            WHERE HomeworkId=$id AND FireDate=$date AND OffsetMinutes=$off
            """;
        cmd.Parameters.AddWithValue("$id", homeworkId);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$off", offsetMinutes);
        return cmd.ExecuteScalar() is not null;
    }

    public void MarkHomeworkNotificationSent(long homeworkId, DateTime date, int offsetMinutes)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            INSERT OR IGNORE INTO HomeworkNotificationLog (HomeworkId, FireDate, OffsetMinutes)
            VALUES ($id, $date, $off)
            """;
        cmd.Parameters.AddWithValue("$id", homeworkId);
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$off", offsetMinutes);
        cmd.ExecuteNonQuery();
    }
}
