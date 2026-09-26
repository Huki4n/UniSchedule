namespace UniSchedule.Models;

public sealed class AppSettings
{
    public const string DefaultGroupCode = "11-321";
    public const int HomeworkMonthOffset = -1;
    public const int HomeworkPresetWeek = 7 * 24 * 60;
    public const int HomeworkPresetFiveDays = 5 * 24 * 60;
    public const int HomeworkPresetThreeDays = 3 * 24 * 60;
    public const int HomeworkPresetDay = 24 * 60;
    public const int HomeworkPresetTwelveHours = 12 * 60;
    public const int HomeworkPresetFourHours = 4 * 60;
    public static readonly int[] HomeworkReminderPresets =
    [
        HomeworkPresetWeek,
        HomeworkPresetFiveDays,
        HomeworkPresetThreeDays,
        HomeworkPresetDay,
        HomeworkPresetTwelveHours,
        HomeworkPresetFourHours
    ];
    public static readonly DateTime DefaultSemesterStart = new(2026, 9, 1);

    private int[] _reminderMinutes = [60, 15];
    private int[] _homeworkReminderMinutes = [.. HomeworkReminderPresets];

    public string SelectedGroup { get; set; } = DefaultGroupCode;
    public DateTime SemesterStart { get; set; } = DefaultSemesterStart;

    public int[] ReminderMinutes
    {
        get => _reminderMinutes;
        set => _reminderMinutes = NormalizeReminders(value);
    }

    public int[] ReminderOffsets => _reminderMinutes;

    public int[] HomeworkReminderMinutes
    {
        get => _homeworkReminderMinutes;
        set => _homeworkReminderMinutes = NormalizeHomeworkReminders(value);
    }

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

    public static int[] NormalizeHomeworkReminders(IEnumerable<int>? minutes) =>
        (minutes ?? [])
            .Where(value => value > 0 || value == HomeworkMonthOffset)
            .Distinct()
            .OrderByDescending(value => value == HomeworkMonthOffset ? int.MaxValue : value)
            .ToArray();

    public static int[] ParseHomeworkReminderList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var values = new List<int>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("месяц", StringComparison.OrdinalIgnoreCase))
            {
                values.Add(HomeworkMonthOffset);
            }
            else if (int.TryParse(part, out var minutes))
            {
                values.Add(minutes);
            }
        }

        return NormalizeHomeworkReminders(values);
    }

    public static string FormatHomeworkReminderList(IEnumerable<int>? minutes) =>
        string.Join(",", NormalizeHomeworkReminders(minutes).Select(value =>
            value == HomeworkMonthOffset ? "месяц" : value.ToString()));

    public static string HomeworkReminderSpan(int minutes) => minutes switch
    {
        HomeworkMonthOffset => "месяц",
        HomeworkPresetWeek => "7 дней",
        HomeworkPresetFiveDays => "5 дней",
        HomeworkPresetThreeDays => "3 дня",
        HomeworkPresetDay => "1 день",
        HomeworkPresetTwelveHours => "12 часов",
        HomeworkPresetFourHours => "4 часа",
        _ => $"{minutes} мин."
    };

    public AppSettings Clone() => new()
    {
        SelectedGroup = SelectedGroup,
        SemesterStart = SemesterStart,
        ReminderMinutes = ReminderMinutes.ToArray(),
        HomeworkReminderMinutes = HomeworkReminderMinutes.ToArray(),
        NotificationsEnabled = NotificationsEnabled,
        Autostart = Autostart,
        MinimizeToTray = MinimizeToTray
    };
}
