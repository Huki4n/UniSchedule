using UniSchedule.Models;

namespace UniSchedule.Services;

public static class LessonAccent
{
    public static string For(Lesson lesson) => lesson.LessonType switch
    {
        LessonCodes.Practice => "#34D399",
        LessonCodes.Lab => "#FB923C",
        LessonCodes.Credit => "#FBBF24",
        LessonCodes.Exam => "#F87171",
        _ => MeetingLinks.IsCallUrl(lesson.MeetingUrl) ? "#A78BFA" : "#60A5FA"
    };
}
