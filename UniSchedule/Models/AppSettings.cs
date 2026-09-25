namespace UniSchedule.Models;

public sealed class AppSettings
{
    public const string DefaultGroupCode = "11-321";
    public static readonly DateTime DefaultSemesterStart = new(2026, 9, 1);

    public string SelectedGroup { get; set; } = DefaultGroupCode;
    public DateTime SemesterStart { get; set; } = DefaultSemesterStart;
    public int FirstReminderMinutes { get; set; } = 60;
    public int SecondReminderMinutes { get; set; } = 15;
    public bool NotificationsEnabled { get; set; } = true;
    public bool Autostart { get; set; }
    public bool MinimizeToTray { get; set; } = true;

    public int[] ReminderOffsets =>
        new[] { FirstReminderMinutes, SecondReminderMinutes }
            .Where(v => v > 0)
            .Distinct()
            .OrderByDescending(v => v)
            .ToArray();

    public AppSettings Clone() => new()
    {
        SelectedGroup = SelectedGroup,
        SemesterStart = SemesterStart,
        FirstReminderMinutes = FirstReminderMinutes,
        SecondReminderMinutes = SecondReminderMinutes,
        NotificationsEnabled = NotificationsEnabled,
        Autostart = Autostart,
        MinimizeToTray = MinimizeToTray
    };
}
