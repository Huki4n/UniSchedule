namespace UniSchedule.Models;

public sealed class HomeworkComment
{
    public long Id { get; set; }
    public long HomeworkId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
