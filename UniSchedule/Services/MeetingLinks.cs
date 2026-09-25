using System.Text.RegularExpressions;
using UniSchedule.Models;

namespace UniSchedule.Services;

public static class MeetingLinks
{
    private static readonly Regex CallHostRegex = new(
        @"telemost|mts-link|mtslink|meet\.google|ktalk|teleboss|zoom\.|t\.me/|clck\.ru",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsCallUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && CallHostRegex.IsMatch(url);

    public static bool IsLmsUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        url.Contains("edu.kpfu", StringComparison.OrdinalIgnoreCase);

    public static void ApplyOnlineNote(Lesson lesson)
    {
        var parts = lesson.Notes
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.Equals("онлайн", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (IsCallUrl(lesson.MeetingUrl))
        {
            parts.Insert(0, "онлайн");
        }

        lesson.Notes = string.Join("; ", parts);
    }
}
