using UniSchedule.Models;

namespace UniSchedule.Services;

public static class LessonForm
{
    public const string SubjectError = "Укажите название предмета.";
    public const string TimeError = "Время укажите в формате ЧЧ:ММ, конец позже начала.";

    public static string DeleteConfirmText(string? subject, int homeworkCount)
    {
        var named = !string.IsNullOrWhiteSpace(subject);
        var lesson = named ? $"«{subject!.Trim()}» будет удалена." : "Пара будет удалена.";
        if (homeworkCount <= 0)
        {
            return lesson;
        }

        return named
            ? $"«{subject!.Trim()}» будет удалена вместе с домашками ({homeworkCount})."
            : $"Пара будет удалена вместе с домашками ({homeworkCount}).";
    }

    public static bool TryValidate(
        string? subject,
        string? startText,
        string? endText,
        out TimeSpan start,
        out TimeSpan end,
        out string? error)
    {
        start = default;
        end = default;
        if (string.IsNullOrWhiteSpace(subject))
        {
            error = SubjectError;
            return false;
        }

        if (!TimeSpan.TryParse((startText ?? "").Trim(), out start) ||
            !TimeSpan.TryParse((endText ?? "").Trim(), out end) ||
            end <= start)
        {
            error = TimeError;
            return false;
        }

        error = null;
        return true;
    }

    public readonly record struct Fields(
        DayOfWeek Day,
        string Start,
        string End,
        string Subject,
        string LessonType,
        string Teacher,
        string Room,
        string MeetingUrl,
        string LmsUrl,
        WeekParity Parity,
        string WeekFrom,
        string WeekTo,
        string Notes);

    public static bool HasEdits(Fields baseline, Fields current) => Normalize(baseline) != Normalize(current);

    private static Fields Normalize(Fields fields) => fields with
    {
        Start = fields.Start.Trim(),
        End = fields.End.Trim(),
        Subject = fields.Subject.Trim(),
        LessonType = fields.LessonType.Trim(),
        Teacher = fields.Teacher.Trim(),
        Room = fields.Room.Trim(),
        MeetingUrl = fields.MeetingUrl.Trim(),
        LmsUrl = fields.LmsUrl.Trim(),
        WeekFrom = fields.WeekFrom.Trim(),
        WeekTo = fields.WeekTo.Trim(),
        Notes = fields.Notes.Trim()
    };
}
