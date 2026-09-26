using UniSchedule.Models;

namespace UniSchedule.Services;

public static class SettingsForm
{
    public const string ReminderError = "Минуты напоминания должны быть целым числом больше нуля.";
    public const string HomeworkReminderError =
        "Минуты напоминания о сдаче должны быть целым числом больше нуля или словом «месяц».";

    public static bool TryParseReminder(string? text, out int minutes)
    {
        if (!int.TryParse(text, out minutes) || minutes < 1)
        {
            minutes = 0;
            return false;
        }

        return true;
    }

    public static bool TryParseHomeworkReminder(string? text, out int minutes)
    {
        if (text?.Trim().Equals("месяц", StringComparison.OrdinalIgnoreCase) == true)
        {
            minutes = AppSettings.HomeworkMonthOffset;
            return true;
        }

        if (!int.TryParse(text, out minutes) || minutes < 1)
        {
            minutes = 0;
            return false;
        }

        return true;
    }

    public static string NormalizeGroup(string? text) =>
        string.IsNullOrWhiteSpace(text) ? AppSettings.DefaultGroupCode : text.Trim();

    public static DateTime NormalizeSemesterStart(DateTime? selected) =>
        selected?.Date ?? AppSettings.DefaultSemesterStart;
}
