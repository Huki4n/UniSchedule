using System.Text.RegularExpressions;
using UniSchedule.Models;

namespace UniSchedule.Services;

public static class HomeworkForm
{
    public const string LessonError = "Выберите пару.";
    public const string TitleError = "Укажите название задания.";
    public const string DeadlineError = "Укажите дедлайн.";
    public const string NoLessonsMessage = "Сначала добавьте пару, к ней можно привязать домашку.";
    public const string MissingLessonMessage = "Выбранная пара удалена. Выберите другую.";

    public static bool LessonExists(IReadOnlyList<Lesson> lessons, long lessonId) =>
        lessons.Any(lesson => lesson.Id == lessonId);

    public static string SlotLabel(Lesson lesson) =>
        $"{AcademicCalendar.DayName(lesson.DayOfWeek)} · {lesson.Start:hh\\:mm}";

    public static bool MatchesSlot(Lesson lesson, Lesson card) =>
        lesson.DayOfWeek == card.DayOfWeek
        && lesson.Start == card.Start
        && string.Equals(SubjectTitle(lesson.Subject), SubjectTitle(card.Subject), StringComparison.OrdinalIgnoreCase);

    public static string SubjectTitle(string? subject)
    {
        var text = (subject ?? "").Trim();
        var comma = text.IndexOf(',');
        if (comma > 0 && TailIsWeekOrType(text[(comma + 1)..]))
        {
            text = text[..comma];
        }

        text = WeekPhrase.Replace(text, " ");
        text = TypeWord.Replace(text, " ");
        text = Regex.Replace(text, @"\s+", " ").Trim(' ', ',', '.', ';');
        return text.Length == 0 ? (subject ?? "").Trim() : text;
    }

    public static IReadOnlyList<IReadOnlyList<Lesson>> GroupBySubject(IReadOnlyList<Lesson> lessons)
    {
        return lessons
            .OrderBy(lesson => DayOrder(lesson.DayOfWeek))
            .ThenBy(lesson => lesson.Start)
            .ThenBy(lesson => lesson.WeekFrom ?? 0)
            .GroupBy(lesson => SubjectTitle(lesson.Subject), StringComparer.OrdinalIgnoreCase)
            .Select(group => (IReadOnlyList<Lesson>)group.ToList())
            .ToList();
    }

    public static IReadOnlyList<Lesson> DistinctSlots(IReadOnlyList<Lesson> lessons, long preferredId)
    {
        return lessons
            .GroupBy(lesson => (lesson.DayOfWeek, lesson.Start))
            .Select(group => group.FirstOrDefault(lesson => lesson.Id == preferredId) ?? group.First())
            .ToList();
    }

    private static readonly Regex WeekPhrase = new(
        @"\d+\s*[-–—]\s*\d+\s*нед\.?|с\s+\d+\s*нед\.?|\d+\s*нед\.?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeWord = new(
        @"\b(лекц(ия|ии)?|лек|практ(ика)?|прак|лаб(ораторная)?)\.?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool TailIsWeekOrType(string tail)
    {
        var cleaned = WeekPhrase.Replace(tail, "");
        cleaned = TypeWord.Replace(cleaned, "");
        cleaned = Regex.Replace(cleaned, @"[\s,.;:/–—-]+", "");
        return cleaned.Length == 0;
    }

    private static int DayOrder(DayOfWeek day) => ((int)day - (int)DayOfWeek.Monday + 7) % 7;

    public static bool TryValidate(long? lessonId, string? title, DateTime? deadline, out string? error)
    {
        if (lessonId is null or <= 0)
        {
            error = LessonError;
            return false;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            error = TitleError;
            return false;
        }

        if (deadline is null)
        {
            error = DeadlineError;
            return false;
        }

        error = null;
        return true;
    }

    public readonly record struct Fields(
        long LessonId,
        string Title,
        string Description,
        DateTime Deadline,
        bool IsDone,
        string Url,
        string ExtraUrl,
        string PendingComment,
        bool CommentsChanged);

    public static bool HasEdits(Fields baseline, Fields current)
    {
        var left = Normalize(baseline);
        var right = Normalize(current);
        return left != right;
    }

    private static Fields Normalize(Fields fields) => fields with
    {
        Title = fields.Title.Trim(),
        Description = fields.Description.Trim(),
        Deadline = fields.Deadline.Date,
        Url = fields.Url.Trim(),
        ExtraUrl = fields.ExtraUrl.Trim(),
        PendingComment = fields.PendingComment.Trim()
    };
}
