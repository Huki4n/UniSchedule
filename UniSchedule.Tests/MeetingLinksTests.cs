using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class MeetingLinksTests
{
    [Theory]
    [InlineData("https://telemost.yandex.ru/j/1", true, false)]
    [InlineData("https://edu.kpfu.ru/course/1", false, true)]
    [InlineData("https://example.com", false, false)]
    public void ClassifiesCallAndLms(string url, bool call, bool lms)
    {
        Assert.Equal(call, MeetingLinks.IsCallUrl(url));
        Assert.Equal(lms, MeetingLinks.IsLmsUrl(url));
    }

    [Fact]
    public void ApplyOnlineNote_PutsOnlineFirstWithoutDuplicates()
    {
        var lesson = new Lesson
        {
            MeetingUrl = "https://zoom.us/j/1",
            Notes = "онлайн; перенос"
        };

        MeetingLinks.ApplyOnlineNote(lesson);

        Assert.Equal("онлайн; перенос", lesson.Notes);
    }
}
