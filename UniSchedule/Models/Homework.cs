namespace UniSchedule.Models;

public sealed class Homework
{
    public long Id { get; set; }
    public long LessonId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";
    public string ExtraUrl { get; set; } = "";
    public DateTime Deadline { get; set; }
    public bool IsDone { get; set; }

    public Homework Clone() => (Homework)MemberwiseClone();
}
