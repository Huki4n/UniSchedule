using Microsoft.Data.Sqlite;
using UniSchedule.Models;

namespace UniSchedule.Data;

public sealed partial class AppDatabase
{
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
        cmd.CommandText = """
            SELECT Id, GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                   MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source, ElectiveKey
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
        cmd.CommandText = """
            SELECT Id, GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                   MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source, ElectiveKey
            FROM Lessons
            ORDER BY GroupCode, DayOfWeek, Start
            """;
        return ReadLessons(cmd);
    }

    public long UpsertLesson(Lesson lesson)
    {
        using var db = Open();
        WriteLesson(db, lesson);
        return lesson.Id;
    }

    public void UpsertLessons(IReadOnlyList<Lesson> lessons)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        foreach (var lesson in lessons)
        {
            WriteLesson(db, lesson);
        }

        tx.Commit();
    }

    private static void WriteLesson(SqliteConnection db, Lesson lesson)
    {
        using var cmd = db.CreateCommand();
        if (lesson.Id > 0)
        {
            cmd.CommandText = """
                UPDATE Lessons SET
                    GroupCode=$group, DayOfWeek=$day, Start=$start, End=$end, Subject=$subject,
                    LessonType=$type, Teacher=$teacher, Room=$room, MeetingUrl=$meet, LmsUrl=$lms,
                    Parity=$parity, WeekFrom=$from, WeekTo=$to, Notes=$notes, RawText=$raw, Source=$source,
                    ElectiveKey=$elective
                WHERE Id=$id
                """;
            cmd.Parameters.AddWithValue("$id", lesson.Id);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO Lessons (
                    GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                    MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source, ElectiveKey)
                VALUES (
                    $group, $day, $start, $end, $subject, $type, $teacher, $room,
                    $meet, $lms, $parity, $from, $to, $notes, $raw, $source, $elective);
                SELECT last_insert_rowid();
                """;
        }

        BindLesson(cmd, lesson);
        var result = cmd.ExecuteScalar();
        if (lesson.Id <= 0 && result is not null)
        {
            lesson.Id = Convert.ToInt64(result);
        }
    }

    public void DeleteLesson(long id)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        DeleteCommentsForLesson(db, id);
        using (var homework = db.CreateCommand())
        {
            homework.CommandText = "DELETE FROM Homework WHERE LessonId=$id";
            homework.Parameters.AddWithValue("$id", id);
            homework.ExecuteNonQuery();
        }

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Lessons WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        using (var rollback = db.CreateCommand())
        {
            rollback.CommandText = "DELETE FROM SubjectRollback WHERE LessonId=$id";
            rollback.Parameters.AddWithValue("$id", id);
            rollback.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public int ReplaceImported(IReadOnlyList<Lesson> lessons, IReadOnlyList<string>? groups = null)
    {
        var replaceGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (groups is not null)
        {
            foreach (var group in groups)
            {
                AddGroup(replaceGroups, group);
            }
        }

        foreach (var lesson in lessons)
        {
            AddGroup(replaceGroups, lesson.GroupCode);
        }

        using var db = Open();
        using var tx = db.BeginTransaction();
        var oldKeys = ReadImportedKeys(db, replaceGroups);
        var linked = ReadHomeworkLinks(db, oldKeys.Keys);

        foreach (var group in replaceGroups)
        {
            using var clear = db.CreateCommand();
            clear.CommandText =
                "DELETE FROM Lessons WHERE Source='imported' AND GroupCode = $group COLLATE NOCASE";
            clear.Parameters.AddWithValue("$group", group);
            clear.ExecuteNonQuery();
        }

        var newKeys = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var lesson in lessons)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Lessons (
                    GroupCode, DayOfWeek, Start, End, Subject, LessonType, Teacher, Room,
                    MeetingUrl, LmsUrl, Parity, WeekFrom, WeekTo, Notes, RawText, Source, ElectiveKey)
                VALUES (
                    $group, $day, $start, $end, $subject, $type, $teacher, $room,
                    $meet, $lms, $parity, $from, $to, $notes, $raw, $source, $elective);
                SELECT last_insert_rowid();
                """;
            lesson.Source = LessonCodes.Imported;
            BindLesson(cmd, lesson);
            var id = Convert.ToInt64(cmd.ExecuteScalar());
            lesson.Id = id;
            newKeys.TryAdd(LessonMatchKey.Of(lesson), id);
        }

        foreach (var (homeworkId, lessonId) in linked)
        {
            using var cmd = db.CreateCommand();
            if (
                oldKeys.TryGetValue(lessonId, out var key)
                && newKeys.TryGetValue(key, out var newId)
            )
            {
                cmd.CommandText = "UPDATE Homework SET LessonId=$lesson WHERE Id=$id";
                cmd.Parameters.AddWithValue("$lesson", newId);
            }
            else
            {
                DeleteCommentsForHomework(db, homeworkId);
                cmd.CommandText = "DELETE FROM Homework WHERE Id=$id";
            }

            cmd.Parameters.AddWithValue("$id", homeworkId);
            cmd.ExecuteNonQuery();
        }

        using (var dropRollback = db.CreateCommand())
        {
            dropRollback.CommandText = """
                DELETE FROM SubjectRollback
                WHERE LessonId NOT IN (SELECT Id FROM Lessons)
                """;
            dropRollback.ExecuteNonQuery();
        }

        tx.Commit();
        return lessons.Count;
    }

    private static List<Lesson> ReadLessons(SqliteCommand cmd)
    {
        var list = new List<Lesson>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(
                new Lesson
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
                    Source = reader.GetString(16),
                    ElectiveKey = reader.GetString(17),
                }
            );
        }

        return list;
    }

    private static void AddGroup(HashSet<string> groups, string? group)
    {
        var code = (group ?? "").Trim();
        if (code.Length > 0)
        {
            groups.Add(code);
        }
    }

    private static Dictionary<long, string> ReadImportedKeys(
        SqliteConnection db,
        IReadOnlySet<string> groups
    )
    {
        var keys = new Dictionary<long, string>();
        if (groups.Count == 0)
        {
            return keys;
        }

        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT Id, GroupCode, DayOfWeek, Start, Subject
            FROM Lessons
            WHERE Source='imported'
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var group = reader.GetString(1);
            if (!groups.Contains(group.Trim()))
            {
                continue;
            }

            var lesson = new Lesson
            {
                GroupCode = group,
                DayOfWeek = (DayOfWeek)reader.GetInt32(2),
                Start = TimeSpan.Parse(reader.GetString(3)),
                Subject = reader.GetString(4),
            };
            keys[reader.GetInt64(0)] = LessonMatchKey.Of(lesson);
        }

        return keys;
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
        cmd.Parameters.AddWithValue("$elective", lesson.ElectiveKey);
    }

    public List<ElectivePick> GetElectivePicks()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT GroupCode, DayOfWeek, Start, Subject FROM ElectivePick";
        var list = new List<ElectivePick>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(
                new ElectivePick
                {
                    GroupCode = reader.GetString(0),
                    DayOfWeek = (DayOfWeek)reader.GetInt32(1),
                    Start = reader.GetString(2),
                    Subject = reader.GetString(3),
                }
            );
        }

        return list;
    }

    public void SetElectivePick(string group, DayOfWeek day, TimeSpan start, string subject)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ElectivePick (GroupCode, DayOfWeek, Start, Subject)
            VALUES ($group, $day, $start, $subject)
            ON CONFLICT(GroupCode, DayOfWeek, Start) DO UPDATE SET Subject=$subject
            """;
        cmd.Parameters.AddWithValue("$group", group);
        cmd.Parameters.AddWithValue("$day", (int)day);
        cmd.Parameters.AddWithValue("$start", start.ToString(@"hh\:mm"));
        cmd.Parameters.AddWithValue("$subject", subject.Trim());
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<string>? GetElectiveSubjects(string group)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "SELECT Subject FROM ElectiveSubject WHERE GroupCode=$group ORDER BY Subject";
        cmd.Parameters.AddWithValue("$group", group);
        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(reader.GetString(0));
        }

        return list.Count == 0 ? null : list.Where(subject => subject.Length > 0).ToList();
    }

    public void SetElectiveSubjects(string group, IReadOnlyList<string> subjects)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using (var clear = db.CreateCommand())
        {
            clear.CommandText = "DELETE FROM ElectiveSubject WHERE GroupCode=$group";
            clear.Parameters.AddWithValue("$group", group);
            clear.ExecuteNonQuery();
        }

        var names = subjects
            .Select(subject => subject.Trim())
            .Where(subject => subject.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
        {
            names.Add("");
        }

        foreach (var subject in names)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "INSERT INTO ElectiveSubject (GroupCode, Subject) VALUES ($group, $subject)";
            cmd.Parameters.AddWithValue("$group", group);
            cmd.Parameters.AddWithValue("$subject", subject);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
