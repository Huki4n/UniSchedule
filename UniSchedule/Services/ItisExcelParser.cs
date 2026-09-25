using System.Text.RegularExpressions;
using ClosedXML.Excel;
using UniSchedule.Models;

namespace UniSchedule.Services;

public sealed class ImportResult
{
    public List<Lesson> Lessons { get; } = [];
    public List<string> Groups { get; } = [];
    public int LinkMatches { get; set; }

    public string FormatStoredMessage(int storedCount, string selectedGroup)
    {
        var hasGroup = Groups.Any(g =>
            string.Equals(g, selectedGroup, StringComparison.OrdinalIgnoreCase));
        var message = $"Импортировано пар: {storedCount} из {Groups.Count} групп.";
        if (!hasGroup)
        {
            message +=
                $"{Environment.NewLine}Группы {selectedGroup} в файле нет. " +
                "Она останется пустой — добавьте пары вручную или выберите другую группу.";
        }

        return message;
    }
}

public static class ItisExcelParser
{
    public static ImportResult Parse(string path)
    {
        using var workbook = new XLWorkbook(path);
        var schedule = FindSheet(workbook, "Расписание") ?? workbook.Worksheets.First();
        var linksSheet = FindSheet(workbook, "Ссылки");

        var mergeMap = BuildMergeMap(schedule);
        var groups = ReadGroups(schedule, mergeMap);
        var links = linksSheet is null ? [] : ReadLinks(linksSheet);
        var result = new ImportResult();
        result.Groups.AddRange(groups.Select(g => g.Code));
        foreach (var group in groups)
        {
            DayOfWeek? day = null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lastRow = Math.Max(schedule.LastRowUsed()?.RowNumber() ?? 45, 45);

            for (var row = 3; row <= lastRow; row++)
            {
                var dayText = GetText(schedule, mergeMap, row, 1);
                if (LessonTextParser.ParseDay(dayText) is { } parsedDay)
                {
                    day = parsedDay;
                }

                if (day is null)
                {
                    continue;
                }

                var timeText = GetText(schedule, mergeMap, row, 2);
                if (!LessonTextParser.TryParseTime(timeText, out var start, out var end))
                {
                    continue;
                }

                var cellText = GetText(schedule, mergeMap, row, group.Column);
                if (string.IsNullOrWhiteSpace(cellText))
                {
                    continue;
                }

                foreach (var lesson in LessonTextParser.ParseAll(cellText, day.Value, start, end, group.Code))
                {
                    var key = $"{day}|{start}|{end}|{NormalizeKey(lesson.Subject)}|{NormalizeKey(lesson.Teacher)}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    AttachLinks(lesson, group.Code, links);
                    MeetingLinks.ApplyOnlineNote(lesson);
                    result.Lessons.Add(lesson);
                    if (MeetingLinks.IsCallUrl(lesson.MeetingUrl))
                    {
                        result.LinkMatches++;
                    }
                }
            }
        }

        return result;
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string token) =>
        workbook.Worksheets.FirstOrDefault(s =>
            s.Name.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<(int Row, int Col), (int Row, int Col)> BuildMergeMap(IXLWorksheet sheet)
    {
        var map = new Dictionary<(int, int), (int, int)>();
        foreach (var range in sheet.MergedRanges)
        {
            var origin = (range.RangeAddress.FirstAddress.RowNumber, range.RangeAddress.FirstAddress.ColumnNumber);
            for (var row = range.RangeAddress.FirstAddress.RowNumber; row <= range.RangeAddress.LastAddress.RowNumber; row++)
            {
                for (var col = range.RangeAddress.FirstAddress.ColumnNumber; col <= range.RangeAddress.LastAddress.ColumnNumber; col++)
                {
                    map[(row, col)] = origin;
                }
            }
        }

        return map;
    }

    private static List<(string Code, int Column)> ReadGroups(
        IXLWorksheet sheet,
        Dictionary<(int Row, int Col), (int Row, int Col)> mergeMap)
    {
        var groups = new List<(string, int)>();
        var lastCol = Math.Max(sheet.LastColumnUsed()?.ColumnNumber() ?? 68, 3);
        for (var col = 3; col <= lastCol; col++)
        {
            var raw = GetText(sheet, mergeMap, 2, col);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var code = Regex.Replace(raw, @"\s+", " ").Trim();
            if (code.Length > 0)
            {
                groups.Add((code, col));
            }
        }

        return groups;
    }

    private static string GetText(
        IXLWorksheet sheet,
        Dictionary<(int Row, int Col), (int Row, int Col)> mergeMap,
        int row,
        int col)
    {
        var origin = mergeMap.GetValueOrDefault((row, col), (row, col));
        return ReadCell(sheet.Cell(origin.Item1, origin.Item2));
    }

    private static string ReadCell(IXLCell cell)
    {
        if (cell.TryGetValue(out string text) && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        if (!cell.IsEmpty())
        {
            var raw = cell.Value.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }
        }

        var rich = cell.GetRichText().Text;
        return string.IsNullOrWhiteSpace(rich) ? "" : rich.Trim();
    }

    private sealed record OnlineLink(string Subject, string Teacher, string Groups, string MeetingUrl, string LmsUrl);

    private static List<OnlineLink> ReadLinks(IXLWorksheet sheet)
    {
        var links = new List<OnlineLink>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var row = 2; row <= lastRow; row++)
        {
            var subject = sheet.Cell(row, 2).GetString().Trim();
            var teacher = sheet.Cell(row, 3).GetString().Trim();
            var groups = sheet.Cell(row, 4).GetString().Trim();
            var meeting = sheet.Cell(row, 5).GetString().Trim();
            var lms = sheet.Cell(row, 6).GetString().Trim();
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(groups))
            {
                continue;
            }

            if (subject.Equals("Дисциплина", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            links.Add(new OnlineLink(subject, teacher, groups, meeting, lms));
        }

        return links;
    }

    private static void AttachLinks(Lesson lesson, string groupCode, List<OnlineLink> links)
    {
        OnlineLink? best = null;
        var bestScore = 0;
        foreach (var link in links)
        {
            if (!GroupMatches(link.Groups, groupCode))
            {
                continue;
            }

            var score = SubjectScore(lesson.Subject, link.Subject);
            if (!string.IsNullOrWhiteSpace(link.Teacher) &&
                !string.IsNullOrWhiteSpace(lesson.Teacher) &&
                NormalizeKey(link.Teacher) == NormalizeKey(lesson.Teacher))
            {
                score += 3;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = link;
            }
        }

        if (best is null || bestScore < 2)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(lesson.MeetingUrl) && MeetingLinks.IsCallUrl(best.MeetingUrl))
        {
            lesson.MeetingUrl = best.MeetingUrl;
        }

        if (string.IsNullOrWhiteSpace(lesson.LmsUrl) && !string.IsNullOrWhiteSpace(best.LmsUrl))
        {
            lesson.LmsUrl = best.LmsUrl;
        }
    }

    private static bool GroupMatches(string field, string group)
    {
        var target = NormalizeGroup(group);
        foreach (var rawPart in field.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = Regex.Replace(rawPart, @"курс(ы)? по выбору", "", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (NormalizeGroup(part) == target)
            {
                return true;
            }

            var range = Regex.Match(part, @"^(\d+(?:\.\d+)?)-(\d+)-(\d+(?:\.\d+)?)-(\d+)");
            if (!range.Success)
            {
                continue;
            }

            var prefix = range.Groups[1].Value;
            if (!prefix.Equals(range.Groups[3].Value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(range.Groups[2].Value, out var from) ||
                !int.TryParse(range.Groups[4].Value, out var to))
            {
                continue;
            }

            var numberMatch = Regex.Match(target, @"(\d+)$");
            if (numberMatch.Success &&
                int.TryParse(numberMatch.Value, out var number) &&
                number >= from && number <= to &&
                target.StartsWith(NormalizeGroup(prefix + "-"), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int SubjectScore(string lessonSubject, string linkSubject)
    {
        var a = NormalizeKey(lessonSubject);
        var b = NormalizeKey(linkSubject);
        if (a.Length < 4 || b.Length < 4)
        {
            return 0;
        }

        if (a == b)
        {
            return 6;
        }

        if (a.Contains(b) || b.Contains(a))
        {
            return 5;
        }

        var left = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var right = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = left.Intersect(right).Count(t => t.Length > 3);
        return overlap >= 2 ? 3 : overlap == 1 ? 2 : 0;
    }

    private static string NormalizeGroup(string value)
    {
        var compact = Regex.Replace(value, @"\s+", "");
        compact = compact.Replace("..", ".").Replace(".-", "-");
        compact = Regex.Replace(compact, @"\.-", "-");
        return compact.Trim().ToLowerInvariant();
    }

    private static string NormalizeKey(string value)
    {
        var text = value.ToLowerInvariant().Replace('ё', 'е');
        text = Regex.Replace(text, @"[^a-zа-я0-9\s]", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
