namespace UniSchedule.Models;

public sealed class ElectivePick
{
    public string GroupCode { get; set; } = "";
    public DayOfWeek DayOfWeek { get; set; }
    public string Start { get; set; } = "";
    public string Subject { get; set; } = "";
}
