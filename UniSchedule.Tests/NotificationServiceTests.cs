using Microsoft.Data.Sqlite;
using UniSchedule.Data;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public sealed class NotificationServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _logPath;
    private readonly AppDatabase _database;
    private readonly DateTime _dueAt = new(2026, 9, 4, 9, 0, 0);

    public NotificationServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "unischedule-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _logPath = Path.Combine(_directory, "toast.txt");
        _database = new AppDatabase(Path.Combine(_directory, "schedule.db"));
        _database.UpsertLesson(new Lesson
        {
            GroupCode = "11-321",
            DayOfWeek = DayOfWeek.Friday,
            Start = new TimeSpan(10, 0, 0),
            End = new TimeSpan(11, 30, 0),
            Subject = "Базы данных",
            Source = LessonCodes.Manual
        });
    }

    [Fact]
    public void Dispatch_MarksSentOnlyWhenToastSucceeds()
    {
        var service = Create((_, _) => null);
        service.Dispatch(_dueAt);

        var lessonId = _database.GetLessons("11-321")[0].Id;
        Assert.True(_database.WasNotificationSent(lessonId, _dueAt.Date, 60));
        Assert.False(File.Exists(_logPath));
    }

    [Fact]
    public void Dispatch_KeepsReminderAndRecordsEveryFailure_ButNotifiesOnce()
    {
        var notices = new List<Exception>();
        var service = Create((_, _) => new InvalidOperationException("toast down"));
        service.Failed += notices.Add;

        service.Dispatch(_dueAt);
        service.Dispatch(_dueAt);

        var lessonId = _database.GetLessons("11-321")[0].Id;
        Assert.False(_database.WasNotificationSent(lessonId, _dueAt.Date, 60));
        var logged = File.ReadAllText(_logPath);
        Assert.Equal(2, logged.Split("toast down", StringSplitOptions.None).Length - 1);
        var notice = Assert.Single(notices);
        Assert.Contains("toast down", notice.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ShowTest_NotifiesAgainAfterAutomaticFailure()
    {
        var notices = 0;
        var service = Create((_, _) => new InvalidOperationException("toast down"));
        service.Failed += _ => notices++;

        service.Dispatch(_dueAt);
        service.ShowTest(null);

        Assert.Equal(2, notices);
    }

    [Fact]
    public void Pause_TracksNestedDepthUntilMatchingResume()
    {
        var service = Create((_, _) => null);
        Assert.False(service.IsPaused);

        service.Pause();
        service.Pause();
        Assert.True(service.IsPaused);

        service.Resume();
        Assert.True(service.IsPaused);
        service.Resume();
        Assert.False(service.IsPaused);
    }

    [Fact]
    public void Dispatch_MarksHomeworkOnceInsideTheMinuteWindow()
    {
        var lessonId = _database.GetLessons("11-321")[0].Id;
        var deadline = new DateTime(2026, 9, 4);
        var fireAt = new DateTime(2026, 9, 4, 23, 59, 0);
        _database.UpsertHomework(new Homework
        {
            LessonId = lessonId,
            Title = "ЛР",
            Url = "https://example.test/hw",
            Deadline = deadline
        });

        var shown = new List<(string Title, string Subject, int Offset)>();
        var service = Create((_, _) => null, (homework, subject, offset) =>
        {
            shown.Add((homework.Title, subject, offset));
            return null;
        }, [1]);

        service.Dispatch(fireAt);
        service.Dispatch(fireAt);

        var item = Assert.Single(shown);
        Assert.Equal(("ЛР", "Базы данных", 1), item);
        var homeworkId = _database.GetHomework("11-321")[0].Id;
        Assert.True(_database.WasHomeworkNotificationSent(homeworkId, fireAt.Date, 1));
    }

    [Fact]
    public void Dispatch_RetriesHomeworkOnlyInsideTheWindow()
    {
        var lessonId = _database.GetLessons("11-321")[0].Id;
        var fireAt = new DateTime(2026, 9, 4, 23, 59, 0);
        var homeworkId = _database.UpsertHomework(new Homework
        {
            LessonId = lessonId,
            Title = "ЛР",
            Deadline = fireAt.Date
        });

        var shows = 0;
        var service = Create((_, _) => null, (_, _, _) =>
        {
            shows++;
            return new InvalidOperationException("hw toast");
        }, [1]);

        service.Dispatch(fireAt);
        service.Dispatch(fireAt.AddMinutes(3));

        Assert.Equal(1, shows);
        Assert.False(_database.WasHomeworkNotificationSent(homeworkId, fireAt.Date, 1));
    }

    private NotificationService Create(
        Func<Lesson, int, Exception?> show,
        Func<Homework, string, int, Exception?>? showHomework = null,
        int[]? homeworkMinutes = null)
    {
        var settings = new AppSettings
        {
            SelectedGroup = "11-321",
            SemesterStart = new DateTime(2026, 9, 1),
            ReminderMinutes = [60],
            NotificationsEnabled = true
        };
        if (homeworkMinutes is not null)
        {
            settings.HomeworkReminderMinutes = homeworkMinutes;
        }
        return showHomework is null
            ? new NotificationService(_database, settings, show, _logPath)
            : new NotificationService(_database, settings, show, showHomework, _logPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
