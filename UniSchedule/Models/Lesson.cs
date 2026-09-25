namespace UniSchedule.Models;

public sealed class Lesson
{
    public long Id { get; set; }
    public string GroupCode { get; set; } = "";
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;
    public TimeSpan Start { get; set; } = new(8, 30, 0);
    public TimeSpan End { get; set; } = new(10, 0, 0);
    public string Subject { get; set; } = "";
    public string LessonType { get; set; } = "";
    public string Teacher { get; set; } = "";
    public string Room { get; set; } = "";
    public string MeetingUrl { get; set; } = "";
    public string LmsUrl { get; set; } = "";
    public WeekParity Parity { get; set; } = WeekParity.All;
    public int? WeekFrom { get; set; }
    public int? WeekTo { get; set; }
    public string Notes { get; set; } = "";
    public string RawText { get; set; } = "";
    public string Source { get; set; } = LessonCodes.Manual;

    public Lesson Clone() => (Lesson)MemberwiseClone();

    public string TimeText => $"{Start:hh\\:mm}–{End:hh\\:mm}";

    public string PlaceText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Room))
            {
                return Room;
            }

            if (!string.IsNullOrWhiteSpace(MeetingUrl))
            {
                return "Онлайн";
            }

            return "—";
        }
    }

    public bool HasLink =>
        !string.IsNullOrWhiteSpace(MeetingUrl) || !string.IsNullOrWhiteSpace(LmsUrl);

    public string PrimaryUrl =>
        !string.IsNullOrWhiteSpace(MeetingUrl) ? MeetingUrl : LmsUrl;
}
