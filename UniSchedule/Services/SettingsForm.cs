using UniSchedule.Models;

namespace UniSchedule.Services;

public static class SettingsForm
{
    public const string ReminderError = "Минуты напоминаний должны быть целыми числами.";

    public static bool TryParseReminders(string? firstText, string? secondText, out int first, out int second)
    {
        if (!int.TryParse(firstText, out first) || first < 0 ||
            !int.TryParse(secondText, out second) || second < 0)
        {
            first = 0;
            second = 0;
            return false;
        }

        return true;
    }

    public static string NormalizeGroup(string? text) =>
        string.IsNullOrWhiteSpace(text) ? AppSettings.DefaultGroupCode : text.Trim();

    public static DateTime NormalizeSemesterStart(DateTime? selected) =>
        selected?.Date ?? AppSettings.DefaultSemesterStart;
}
