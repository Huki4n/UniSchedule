using UniSchedule.Models;

namespace UniSchedule.Services;

public sealed class ElectiveRow
{
    public required Lesson Lesson { get; init; }
    public IReadOnlyList<Lesson> Options { get; init; } = [];
}

public static class ElectiveChoice
{
    public static string SlotKey(string group, DayOfWeek day, TimeSpan start) =>
        $"{group.Trim()}|{(int)day}|{start:hh\\:mm}";

    public static IReadOnlyList<Lesson> Visible(
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<ElectivePick>? picks,
        IReadOnlyList<string>? subjects = null
    ) => Display(lessons, picks, null, subjects).Select(row => row.Lesson).ToList();

    public static IReadOnlyList<ElectiveRow> Display(
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<ElectivePick>? picks,
        string? search,
        IReadOnlyList<string>? subjects = null
    )
    {
        var query = (search ?? "").Trim();
        var rows = new List<ElectiveRow>();
        foreach (var bucket in Buckets(lessons))
        {
            if (bucket.Count == 1 && string.IsNullOrWhiteSpace(bucket[0].ElectiveKey))
            {
                if (Matches(bucket[0], query))
                {
                    rows.Add(new ElectiveRow { Lesson = bucket[0] });
                }

                continue;
            }

            if (subjects is not null)
            {
                AddSelected(rows, bucket, subjects, query);
                continue;
            }

            var chosen = Chosen(bucket, picks);
            var shown =
                query.Length == 0 ? chosen
                : Matches(chosen, query) ? chosen
                : bucket.FirstOrDefault(lesson => Matches(lesson, query));
            if (shown is null)
            {
                continue;
            }

            rows.Add(new ElectiveRow { Lesson = shown, Options = bucket.Count > 1 ? bucket : [] });
        }

        return rows;
    }

    public static bool Matches(Lesson lesson, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        return lesson.Subject.Contains(query, StringComparison.OrdinalIgnoreCase)
            || lesson.Teacher.Contains(query, StringComparison.OrdinalIgnoreCase)
            || lesson.Room.Contains(query, StringComparison.OrdinalIgnoreCase)
            || lesson.Notes.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string>? RenameSelected(
        IReadOnlyList<string>? selected,
        IEnumerable<(string Previous, string Next)> renames,
        IReadOnlyList<Lesson> groupLessons
    )
    {
        if (selected is null)
        {
            return null;
        }

        var result = selected.ToList();
        var changed = false;
        foreach (var (previous, next) in renames)
        {
            var oldName = previous.Trim();
            var newName = next.Trim();
            if (
                oldName.Length == 0
                || newName.Length == 0
                || string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            var index = result.FindIndex(subject =>
                string.Equals(subject, oldName, StringComparison.OrdinalIgnoreCase)
            );
            if (index < 0)
            {
                continue;
            }

            var stillUsed = groupLessons.Any(lesson =>
                !string.IsNullOrWhiteSpace(lesson.ElectiveKey)
                && string.Equals(lesson.Subject.Trim(), oldName, StringComparison.OrdinalIgnoreCase)
            );
            var hasNew = result.Any(subject =>
                string.Equals(subject, newName, StringComparison.OrdinalIgnoreCase)
            );
            if (stillUsed)
            {
                if (!hasNew)
                {
                    result.Add(newName);
                    changed = true;
                }

                continue;
            }

            if (hasNew)
            {
                result.RemoveAt(index);
            }
            else
            {
                result[index] = newName;
            }

            changed = true;
        }

        return changed ? result : selected;
    }

    private static void AddSelected(
        List<ElectiveRow> rows,
        List<Lesson> bucket,
        IReadOnlyList<string> subjects,
        string query
    )
    {
        var picked = bucket
            .Where(lesson =>
                subjects.Any(subject =>
                    string.Equals(subject, lesson.Subject, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();
        var shown =
            query.Length == 0 ? picked : picked.Where(lesson => Matches(lesson, query)).ToList();
        if (shown.Count == 0 && query.Length > 0)
        {
            var reveal = bucket.FirstOrDefault(lesson => Matches(lesson, query));
            if (reveal is not null)
            {
                shown = [reveal];
            }
        }

        var options = bucket.Count > 1 ? bucket : [];
        foreach (var lesson in shown)
        {
            rows.Add(new ElectiveRow { Lesson = lesson, Options = options });
        }
    }

    private static Lesson Chosen(IReadOnlyList<Lesson> options, IReadOnlyList<ElectivePick>? picks)
    {
        var sample = options[0];
        var start = sample.Start.ToString(@"hh\:mm");
        var pick = picks?.FirstOrDefault(item =>
            item.DayOfWeek == sample.DayOfWeek
            && string.Equals(item.GroupCode, sample.GroupCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Start, start, StringComparison.Ordinal)
        );
        if (pick is not null)
        {
            var match = options.FirstOrDefault(lesson =>
                string.Equals(lesson.Subject, pick.Subject, StringComparison.OrdinalIgnoreCase)
            );
            if (match is not null)
            {
                return match;
            }
        }

        return sample;
    }

    private static IEnumerable<List<Lesson>> Buckets(IReadOnlyList<Lesson> lessons)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<Lesson>>(StringComparer.Ordinal);
        for (var i = 0; i < lessons.Count; i++)
        {
            var lesson = lessons[i];
            var key = string.IsNullOrWhiteSpace(lesson.ElectiveKey) ? $"#{i}" : lesson.ElectiveKey;
            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
                order.Add(key);
            }

            list.Add(lesson);
        }

        return order.Select(key => map[key]);
    }
}
