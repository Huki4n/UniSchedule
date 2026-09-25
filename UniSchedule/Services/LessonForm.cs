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
}
