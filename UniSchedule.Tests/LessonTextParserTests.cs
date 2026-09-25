using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class LessonTextParserTests
{
    [Fact]
    public void Parse_ExtractsSubjectTeacherRoomTypeAndCallUrl()
    {
        var lesson = LessonTextParser.Parse(
            "Базы данных, лекция Иванов И.И. ауд. 1301 https://telemost.yandex.ru/j/abc https://edu.kpfu.ru/course/1",
            DayOfWeek.Monday,
            new TimeSpan(8, 30, 0),
            new TimeSpan(10, 0, 0),
            "11-321");

        Assert.Equal("Базы данных", lesson.Subject);
        Assert.Equal("Иванов И.И.", lesson.Teacher);
        Assert.Equal("1301", lesson.Room);
        Assert.Equal(LessonCodes.Lecture, lesson.LessonType);
        Assert.Equal(LessonCodes.Imported, lesson.Source);
        Assert.Equal("https://telemost.yandex.ru/j/abc", lesson.MeetingUrl);
        Assert.Equal("https://edu.kpfu.ru/course/1", lesson.LmsUrl);
    }

    [Fact]
    public void Parse_ReadsParityAndWeekRange()
    {
        var lesson = LessonTextParser.Parse(
            "Сети, практика Петров П.П. нечетн. нед 1-8 нед",
            DayOfWeek.Tuesday,
            new TimeSpan(10, 10, 0),
            new TimeSpan(11, 40, 0),
            "11-321");

        Assert.Equal(WeekParity.Odd, lesson.Parity);
        Assert.Equal(LessonCodes.Practice, lesson.LessonType);
        Assert.Equal(1, lesson.WeekFrom);
        Assert.Equal(8, lesson.WeekTo);
    }

    [Fact]
    public void ParseAll_SplitsElectives()
    {
        var lessons = LessonTextParser.ParseAll(
            "Дисциплины по выбору: Предмет один Петров П.П. (онлайн) Предмет два Сидоров С.С. ауд. 1301",
            DayOfWeek.Wednesday,
            new TimeSpan(8, 30, 0),
            new TimeSpan(10, 0, 0),
            "11-321");

        Assert.Equal(2, lessons.Count);
        Assert.Contains(lessons, lesson => lesson.Subject.Contains("Предмет один", StringComparison.Ordinal));
        Assert.Contains(lessons, lesson => lesson.Subject.Contains("Предмет два", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("8:30-10:00", 8, 30, 10, 0)]
    [InlineData("8.30–10.00", 8, 30, 10, 0)]
    public void TryParseTime_AcceptsSlotFormats(string text, int sh, int sm, int eh, int em)
    {
        Assert.True(LessonTextParser.TryParseTime(text, out var start, out var end));
        Assert.Equal(new TimeSpan(sh, sm, 0), start);
        Assert.Equal(new TimeSpan(eh, em, 0), end);
    }

    [Theory]
    [InlineData("Понедельник", DayOfWeek.Monday)]
    [InlineData("Пятница", DayOfWeek.Friday)]
    [InlineData("выходной", null)]
    public void ParseDay_RecognizesWeekdays(string text, DayOfWeek? expected)
    {
        Assert.Equal(expected, LessonTextParser.ParseDay(text));
    }
}
