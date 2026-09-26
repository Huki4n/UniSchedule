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

    [Theory]
    [InlineData("https://edu.kpfu.ru/course/1", true)]
    [InlineData(" http://example.com/a ", true)]
    [InlineData("HTTP://example.com", true)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData(@"C:\Windows\notepad.exe", false)]
    [InlineData("ms-msdt:id", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void WebUri_AllowsOnlyAbsoluteHttpAndHttps(string url, bool allowed)
    {
        Assert.Equal(allowed, MeetingLinks.TryGetWebUri(url, out var uri));
        if (allowed)
        {
            Assert.True(uri.Scheme is "http" or "https");
        }
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
