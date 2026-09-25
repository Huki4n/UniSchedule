using System.Globalization;
using Microsoft.Data.Sqlite;

namespace UniSchedule.Data;

public sealed partial class AppDatabase
{
    public string? GetSubjectRollback(long lessonId, DateTime day)
    {
        using var db = Open();
        PurgeSubjectRollbacks(db, day);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT OriginalSubject FROM SubjectRollback WHERE LessonId=$id AND ChangedOn=$day";
        cmd.Parameters.AddWithValue("$id", lessonId);
        cmd.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return cmd.ExecuteScalar() as string;
    }

    public void RememberSubjectRollback(long lessonId, string originalSubject, DateTime day)
    {
        using var db = Open();
        PurgeSubjectRollbacks(db, day);
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO SubjectRollback (LessonId, OriginalSubject, ChangedOn)
            VALUES ($id, $subject, $day)
            ON CONFLICT(LessonId) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("$id", lessonId);
        cmd.Parameters.AddWithValue("$subject", originalSubject.Trim());
        cmd.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    public void ForgetSubjectRollback(long lessonId)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM SubjectRollback WHERE LessonId=$id";
        cmd.Parameters.AddWithValue("$id", lessonId);
        cmd.ExecuteNonQuery();
    }

    private static void PurgeSubjectRollbacks(SqliteConnection db, DateTime day)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM SubjectRollback WHERE ChangedOn<>$day";
        cmd.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }
}
