using UniSchedule.Models;

namespace UniSchedule.Services;

public static class SettingsForm
{
    public const string ReminderError = "Минуты напоминания должны быть целым числом больше нуля.";

    public static bool TryParseReminder(string? text, out int minutes)
    {
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
