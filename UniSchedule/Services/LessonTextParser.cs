using System.Text.RegularExpressions;
using UniSchedule.Models;

namespace UniSchedule.Services;

public static class LessonTextParser
{
    private static readonly Regex TeacherRegex = new(
        @"[А-ЯЁ][а-яё]+(?:-[А-ЯЁ][а-яё]+)?\s+[А-ЯЁ]\.\s*[А-ЯЁ]\.?",
        RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s\)\],]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TimeRegex = new(
        @"(\d{1,2})[.:](\d{2})\s*[-–—]\s*(\d{1,2})[.:](\d{2})",
        RegexOptions.Compiled);

    private static readonly Regex WeekRangeRegex = new(
        @"(\d+)\s*[-–]\s*(\d+)\s*нед",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WeekFromRegex = new(
        @"с(?:о)?\s+(\d+)\s+недел",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WeekSingleRegex = new(
        @"(?:^|[^\d])(\d{1,2})\s*нед",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RoomAudRegex = new(
        @"ауд\.?\s*([А-Яа-яA-Za-z0-9.\-]+(?:\s+гл\.?\s*здание)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RoomInRegex = new(
        @"\bв\s+(\d{3,4}[а-яА-Я]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<Lesson> ParseAll(string raw, DayOfWeek day, TimeSpan start, TimeSpan end, string groupCode)
    {
        var chunks = SplitElectives(raw);
        if (chunks.Count <= 1)
        {
            return [Parse(raw, day, start, end, groupCode)];
        }

        return chunks
            .Select(chunk => Parse(chunk, day, start, end, groupCode))
            .Where(lesson => !string.IsNullOrWhiteSpace(lesson.Subject))
            .ToList();
    }

    public static Lesson Parse(string raw, DayOfWeek day, TimeSpan start, TimeSpan end, string groupCode)
    {
        var text = Normalize(raw);
        var lesson = new Lesson
        {
            GroupCode = groupCode,
            DayOfWeek = day,
            Start = start,
            End = end,
            RawText = raw.Trim(),
            Source = LessonCodes.Imported
        };

        foreach (var url in UrlRegex.Matches(text).Select(m => m.Value.TrimEnd('.', ',', ';')))
        {
            if (MeetingLinks.IsCallUrl(url) && string.IsNullOrWhiteSpace(lesson.MeetingUrl))
            {
                lesson.MeetingUrl = url;
            }
            else if (MeetingLinks.IsLmsUrl(url) && string.IsNullOrWhiteSpace(lesson.LmsUrl))
            {
                lesson.LmsUrl = url;
            }
        }

        if (Regex.IsMatch(text, @"нечетн\.?\s*нед", RegexOptions.IgnoreCase))
        {
            lesson.Parity = WeekParity.Odd;
        }
        else if (Regex.IsMatch(text, @"четн\.?\s*нед", RegexOptions.IgnoreCase))
        {
            lesson.Parity = WeekParity.Even;
        }

        if (WeekRangeRegex.Match(text) is { Success: true } range)
        {
            lesson.WeekFrom = int.Parse(range.Groups[1].Value);
            lesson.WeekTo = int.Parse(range.Groups[2].Value);
        }
        else if (WeekFromRegex.Match(text) is { Success: true } fromWeek)
        {
            lesson.WeekFrom = int.Parse(fromWeek.Groups[1].Value);
        }
        else if (WeekSingleRegex.Match(text) is { Success: true } single
                 && int.TryParse(single.Groups[1].Value, out var week)
                 && week is >= 1 and <= 20)
        {
            lesson.WeekFrom = week;
            lesson.WeekTo = week;
        }

        if (Regex.IsMatch(text, @"\bлаб", RegexOptions.IgnoreCase))
        {
            lesson.LessonType = LessonCodes.Lab;
        }
        else if (Regex.IsMatch(text, @"\bпракт|\bпрак\.?", RegexOptions.IgnoreCase))
        {
            lesson.LessonType = LessonCodes.Practice;
        }
        else if (Regex.IsMatch(text, @"\bлекц|\bлек\.?", RegexOptions.IgnoreCase))
        {
            lesson.LessonType = LessonCodes.Lecture;
        }

        if (TeacherRegex.Match(text) is { Success: true } teacher)
        {
            lesson.Teacher = teacher.Value.Trim();
        }

        lesson.Room = ExtractRoom(text);

        if (TimeRegex.Match(text) is { Success: true } altTime
            && TryTime(altTime, out var altStart, out var altEnd)
            && (altStart != start || altEnd != end))
        {
            lesson.Start = altStart;
            lesson.End = altEnd;
        }

        lesson.Subject = ExtractSubject(text, lesson.Teacher);
        lesson.Notes = Append(lesson.Notes, ExtractNotes(text));
        return lesson;
    }

    public static bool TryParseTime(string? value, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = Regex.Replace(value, @"\s+", "");
        var match = TimeRegex.Match(normalized);
        return match.Success && TryTime(match, out start, out end);
    }

    public static DayOfWeek? ParseDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = Regex.Replace(value, @"[\s*]+", "").ToUpperInvariant();
        if (compact.Contains("ПОНЕДЕЛЬНИК")) return DayOfWeek.Monday;
        if (compact.Contains("ВТОРНИК")) return DayOfWeek.Tuesday;
        if (compact.Contains("СРЕДА")) return DayOfWeek.Wednesday;
        if (compact.Contains("ЧЕТВЕРГ")) return DayOfWeek.Thursday;
        if (compact.Contains("ПЯТНИЦА")) return DayOfWeek.Friday;
        if (compact.Contains("СУББОТА")) return DayOfWeek.Saturday;
        return null;
    }

    private static bool TryTime(Match match, out TimeSpan start, out TimeSpan end)
    {
        start = new TimeSpan(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), 0);
        end = new TimeSpan(int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value), 0);
        return end > start;
    }

    private static List<string> SplitElectives(string raw)
    {
        if (!Regex.IsMatch(raw, @"по выбору", RegexOptions.IgnoreCase))
        {
            return [raw];
        }

        var text = Regex.Replace(
            raw,
            @"дисциплин[аы]?\s+по выбору\s*:?\s*",
            "",
            RegexOptions.IgnoreCase);
        text = text.Trim();
        var chunks = new List<string>();

        while (true)
        {
            var teacher = TeacherRegex.Match(text);
            if (!teacher.Success)
            {
                break;
            }

            var subject = text[..teacher.Index];
            var after = text[(teacher.Index + teacher.Length)..];
            var tail = Regex.Match(
                after,
                @"^[\s,.;:/–-]*(?:\((?:вебинар[^)]*|онлайн|цор)[^)]*\)|вебинары|вебинар|онлайн|цор|ауд\.?[^\n,]*|в\s+\d{3,4}[а-яА-Я]?|https?://\S+)*",
                RegexOptions.IgnoreCase);
            var tailText = tail.Success ? after[..tail.Length] : "";
            var chunk = $"{subject.Trim()} {teacher.Value} {tailText.Trim()}".Trim();
            if (!string.IsNullOrWhiteSpace(subject) && chunk.Length > 6)
            {
                chunks.Add(chunk);
            }

            text = after[tailText.Length..].Trim();
        }

        return chunks.Count >= 2 ? chunks : [raw];
    }

    private static string ExtractSubject(string text, string teacher)
    {
        var cleaned = UrlRegex.Replace(text, "");
        cleaned = Regex.Replace(cleaned, @"дисциплин[аы]?\s+по выбору\s*:?\s*", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        var colon = cleaned.IndexOf(':');
        if (colon is > 4 and < 80 && !cleaned[..colon].Contains("по выбору", StringComparison.OrdinalIgnoreCase))
        {
            return TrimSubject(cleaned[..colon]);
        }

        if (!string.IsNullOrEmpty(teacher))
        {
            var idx = cleaned.IndexOf(teacher, StringComparison.Ordinal);
            if (idx > 0)
            {
                return TrimSubject(cleaned[..idx]);
            }
        }

        var comma = cleaned.IndexOf(',');
        if (comma is > 4 and < 90)
        {
            return TrimSubject(cleaned[..comma]);
        }

        return TrimSubject(cleaned.Length > 90 ? cleaned[..90] : cleaned);
    }

    private static string TrimSubject(string value)
    {
        var subject = value.Trim(' ', ',', '.', '/', '-', ':', ';');
        subject = Regex.Replace(subject, @"\b(лекции|лекция|лек\.|практ\.|прак\.|лаб\.)\b", "", RegexOptions.IgnoreCase);
        subject = Regex.Replace(subject, @"\s+", " ").Trim(' ', ',', '.');
        return string.IsNullOrWhiteSpace(subject) ? value.Trim() : subject;
    }

    private static string ExtractRoom(string text)
    {
        if (Regex.IsMatch(text, @"КЗВК", RegexOptions.IgnoreCase))
        {
            return "КЗВК, гл. здание";
        }

        if (Regex.IsMatch(text, @"УНИКС", RegexOptions.IgnoreCase))
        {
            return "УНИКС";
        }

        if (Regex.IsMatch(text, @"Сим\.?\s*центр", RegexOptions.IgnoreCase))
        {
            return "Сим. центр";
        }

        if (RoomAudRegex.Match(text) is { Success: true } aud)
        {
            return aud.Groups[1].Value.Trim();
        }

        if (RoomInRegex.Match(text) is { Success: true } inRoom)
        {
            return inRoom.Groups[1].Value.Trim();
        }

        if (Regex.Match(text, @"(\d{3,4})(?:\s|$)") is { Success: true } digits
            && !text.Contains("нед", StringComparison.OrdinalIgnoreCase))
        {
            return digits.Groups[1].Value;
        }

        return "";
    }

    private static string ExtractNotes(string text)
    {
        var parts = new List<string>();
        if (Regex.IsMatch(text, @"не будет", RegexOptions.IgnoreCase))
        {
            parts.Add("не будет");
        }

        if (Regex.IsMatch(text, @"перенос", RegexOptions.IgnoreCase))
        {
            parts.Add("перенос");
        }

        var dates = Regex.Matches(text, @"\d{1,2}\.\d{2}");
        if (dates.Count > 0)
        {
            parts.Add(string.Join(", ", dates.Select(m => m.Value).Distinct().Take(4)));
        }

        return string.Join("; ", parts);
    }

    private static string Normalize(string raw) =>
        raw.Replace('\u00A0', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();

    private static string Append(string current, string extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
        {
            return current;
        }

        return string.IsNullOrWhiteSpace(current) ? extra : $"{current}; {extra}";
    }
}
