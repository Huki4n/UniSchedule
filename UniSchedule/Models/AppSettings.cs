namespace UniSchedule.Models;

public sealed class AppSettings
{
    public const string DefaultGroupCode = "11-321";
    public static readonly DateTime DefaultSemesterStart = new(2026, 9, 1);

    private int[] _reminderMinutes = [60, 15];

    public string SelectedGroup { get; set; } = DefaultGroupCode;
    public DateTime SemesterStart { get; set; } = DefaultSemesterStart;

    public int[] ReminderMinutes
    {
        get => _reminderMinutes;
        set => _reminderMinutes = NormalizeReminders(value);
    }

    public int[] ReminderOffsets => _reminderMinutes;

    public bool NotificationsEnabled { get; set; } = true;
    public bool Autostart { get; set; }
    public bool MinimizeToTray { get; set; } = true;

    public static int[] NormalizeReminders(IEnumerable<int>? minutes) =>
        (minutes ?? [])
            .Where(value => value > 0)
            .Distinct()
            .OrderByDescending(value => value)
            .ToArray();

    public static int[] ParseReminderList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var values = new List<int>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out var minutes))
            {
                values.Add(minutes);
            }
        }

        return NormalizeReminders(values);
    }

    public static string FormatReminderList(IEnumerable<int>? minutes) =>
        string.Join(",", NormalizeReminders(minutes));

    public AppSettings Clone() => new()
    {
        SelectedGroup = SelectedGroup,
        SemesterStart = SemesterStart,
        ReminderMinutes = ReminderMinutes.ToArray(),
        NotificationsEnabled = NotificationsEnabled,
        Autostart = Autostart,
        MinimizeToTray = MinimizeToTray
    };
}
