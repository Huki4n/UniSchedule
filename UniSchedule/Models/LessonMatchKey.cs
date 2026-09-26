namespace UniSchedule.Models;

public static class LessonMatchKey
{
    public static string Of(Lesson lesson) =>
        string.Join(
            '\u001f',
            lesson.GroupCode,
            ((int)lesson.DayOfWeek).ToString(),
            lesson.Start.ToString(@"hh\:mm"),
            lesson.Subject);
}
