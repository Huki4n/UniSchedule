using System.Globalization;
using Microsoft.Data.Sqlite;
using UniSchedule.Models;

namespace UniSchedule.Data;

public sealed partial class AppDatabase
{
    public List<Homework> GetHomework(string groupCode)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            SELECT h.Id, h.LessonId, h.Title, h.Description, h.Deadline, h.IsDone, h.Url, h.ExtraUrl
            FROM Homework h
            INNER JOIN Lessons l ON l.Id = h.LessonId
            WHERE l.GroupCode = $group
            ORDER BY h.Deadline, h.Title
            """;
        cmd.Parameters.AddWithValue("$group", groupCode);
        return ReadHomework(cmd);
    }

    public int CountHomework(long lessonId)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Homework WHERE LessonId=$id";
        cmd.Parameters.AddWithValue("$id", lessonId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public long UpsertHomework(Homework homework)
    {
        using var db = Open();
        WriteHomework(db, homework);
        return homework.Id;
    }

    public long SaveHomeworkWithComments(
        Homework homework,
        IReadOnlyList<long> removedCommentIds,
        IReadOnlyList<HomeworkComment> addedComments)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        WriteHomework(db, homework);
        foreach (var id in removedCommentIds)
        {
            DeleteHomeworkComment(db, id);
        }

        foreach (var comment in addedComments)
        {
            AddHomeworkComment(db, homework.Id, comment.Body, comment.CreatedAt);
        }

        tx.Commit();
        return homework.Id;
    }

    public void DeleteHomework(long id)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        DeleteCommentsForHomework(db, id);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM Homework WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public List<HomeworkComment> GetHomeworkComments(long homeworkId)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            SELECT Id, HomeworkId, Body, CreatedAt
            FROM HomeworkComment
            WHERE HomeworkId=$id
            ORDER BY CreatedAt, Id
            """;
        cmd.Parameters.AddWithValue("$id", homeworkId);
        var list = new List<HomeworkComment>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new HomeworkComment
            {
                Id = reader.GetInt64(0),
                HomeworkId = reader.GetInt64(1),
                Body = reader.GetString(2),
                CreatedAt = DateTime.ParseExact(reader.GetString(3), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            });
        }

        return list;
    }

    public void AddHomeworkComment(long homeworkId, string body, DateTime createdAt)
    {
        using var db = Open();
        AddHomeworkComment(db, homeworkId, body, createdAt);
    }

    public void DeleteHomeworkComment(long id)
    {
        using var db = Open();
        DeleteHomeworkComment(db, id);
    }

    private static void WriteHomework(SqliteConnection db, Homework homework)
    {
        using var cmd = db.CreateCommand();
        if (homework.Id > 0)
        {
            cmd.CommandText =
                """
                UPDATE Homework SET
                    LessonId=$lesson, Title=$title, Description=$description,
                    Deadline=$deadline, IsDone=$done, Url=$url, ExtraUrl=$extra
                WHERE Id=$id
                """;
            cmd.Parameters.AddWithValue("$id", homework.Id);
        }
        else
        {
            cmd.CommandText =
                """
                INSERT INTO Homework (LessonId, Title, Description, Deadline, IsDone, Url, ExtraUrl)
                VALUES ($lesson, $title, $description, $deadline, $done, $url, $extra);
                SELECT last_insert_rowid();
                """;
        }

        BindHomework(cmd, homework);
        var result = cmd.ExecuteScalar();
        if (homework.Id <= 0 && result is not null)
        {
            homework.Id = Convert.ToInt64(result);
        }
    }

    private static void AddHomeworkComment(SqliteConnection db, long homeworkId, string body, DateTime createdAt)
    {
        var text = body.Trim();
        if (homeworkId <= 0 || text.Length == 0)
        {
            return;
        }

        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO HomeworkComment (HomeworkId, Body, CreatedAt)
            VALUES ($homework, $body, $at)
            """;
        cmd.Parameters.AddWithValue("$homework", homeworkId);
        cmd.Parameters.AddWithValue("$body", text);
        cmd.Parameters.AddWithValue("$at", createdAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    private static void DeleteHomeworkComment(SqliteConnection db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM HomeworkComment WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void DeleteCommentsForHomework(SqliteConnection db, long homeworkId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM HomeworkComment WHERE HomeworkId=$id";
        cmd.Parameters.AddWithValue("$id", homeworkId);
        cmd.ExecuteNonQuery();
    }

    private static void DeleteCommentsForLesson(SqliteConnection db, long lessonId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            """
            DELETE FROM HomeworkComment
            WHERE HomeworkId IN (SELECT Id FROM Homework WHERE LessonId=$id)
            """;
        cmd.Parameters.AddWithValue("$id", lessonId);
        cmd.ExecuteNonQuery();
    }

    private static List<(long HomeworkId, long LessonId)> ReadHomeworkLinks(SqliteConnection db, IReadOnlyCollection<long> lessonIds)
    {
        var links = new List<(long, long)>();
        if (lessonIds.Count == 0)
        {
            return links;
        }

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Id, LessonId FROM Homework";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var lessonId = reader.GetInt64(1);
            if (lessonIds.Contains(lessonId))
            {
                links.Add((reader.GetInt64(0), lessonId));
            }
        }

        return links;
    }

    private static List<Homework> ReadHomework(SqliteCommand cmd)
    {
        var list = new List<Homework>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Homework
            {
                Id = reader.GetInt64(0),
                LessonId = reader.GetInt64(1),
                Title = reader.GetString(2),
                Description = reader.GetString(3),
                Deadline = DateTime.ParseExact(reader.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                IsDone = reader.GetInt32(5) != 0,
                Url = reader.GetString(6),
                ExtraUrl = reader.GetString(7)
            });
        }

        return list;
    }

    private static void BindHomework(SqliteCommand cmd, Homework homework)
    {
        cmd.Parameters.AddWithValue("$lesson", homework.LessonId);
        cmd.Parameters.AddWithValue("$title", homework.Title.Trim());
        cmd.Parameters.AddWithValue("$description", homework.Description.Trim());
        cmd.Parameters.AddWithValue("$deadline", homework.Deadline.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$done", homework.IsDone ? 1 : 0);
        cmd.Parameters.AddWithValue("$url", homework.Url.Trim());
        cmd.Parameters.AddWithValue("$extra", homework.ExtraUrl.Trim());
    }
}
