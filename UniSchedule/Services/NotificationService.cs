using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using UniSchedule.Data;
using UniSchedule.Models;

namespace UniSchedule.Services;

public sealed class NotificationService : IDisposable
{
    private readonly AppDatabase _db;
    private readonly DispatcherTimer _timer;
    private readonly Func<Lesson, int, Exception?> _show;
    private readonly Func<Homework, string, int, Exception?> _showHomework;
    private readonly string _failureLogPath;
    private AppSettings _settings;
    private bool _checking;
    private bool _failureNotified;
    private int _pauseDepth;

    public event Action<Exception>? Failed;

    public NotificationService(AppDatabase db, AppSettings settings)
        : this(db, settings, TryShow, TryShowHomework, ToastLog.DefaultPath)
    {
    }

    internal NotificationService(
        AppDatabase db,
        AppSettings settings,
        Func<Lesson, int, Exception?> show,
        string failureLogPath)
        : this(db, settings, show, TryShowHomework, failureLogPath)
    {
    }

    internal NotificationService(
        AppDatabase db,
        AppSettings settings,
        Func<Lesson, int, Exception?> show,
        Func<Homework, string, int, Exception?> showHomework,
        string failureLogPath)
    {
        _db = db;
        _settings = settings;
        _show = show;
        _showHomework = showHomework;
        _failureLogPath = failureLogPath;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Check();
    }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void Start()
    {
        _timer.Start();
        Check();
    }

    public void Pause()
    {
        _pauseDepth++;
        _timer.Stop();
    }

    public void Resume()
    {
        if (_pauseDepth > 0)
        {
            _pauseDepth--;
        }

        if (_pauseDepth == 0)
        {
            _timer.Start();
        }
    }

    internal bool IsPaused => _pauseDepth > 0;

    public void ShowTest(Lesson? lesson)
    {
        var error = _show(
            lesson ?? new Lesson
            {
                Subject = "Тестовое уведомление",
                Start = DateTime.Now.TimeOfDay,
                End = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(90)),
                Room = "1301",
                Teacher = "Проверка"
            },
            15);
        if (error is not null)
        {
            Report(error, notify: true);
        }
    }

    public void Dispose() => _timer.Stop();

    private void Check()
    {
        if (_checking || _pauseDepth > 0)
        {
            return;
        }

        _checking = true;
        try
        {
            Dispatch(DateTime.Now);
        }
        finally
        {
            _checking = false;
        }
    }

    internal void Dispatch(DateTime now)
    {
        if (!_settings.NotificationsEnabled)
        {
            return;
        }

        var lessons = _db.GetLessons(_settings.SelectedGroup);
        var due = NotificationPlanner.Collect(lessons, _settings, now, _db.WasNotificationSent);
        foreach (var item in due)
        {
            var error = _show(item.Lesson, item.OffsetMinutes);
            if (error is null)
            {
                _db.MarkNotificationSent(item.Lesson.Id, now.Date, item.OffsetMinutes);
                continue;
            }

            Report(error, notify: false);
        }

        var subjects = lessons.ToDictionary(lesson => lesson.Id, lesson => lesson.Subject);
        var homeworkDue = NotificationPlanner.CollectHomework(
            _db.GetHomework(_settings.SelectedGroup),
            _settings,
            now,
            _db.WasHomeworkNotificationSent);
        foreach (var item in homeworkDue)
        {
            subjects.TryGetValue(item.Homework.LessonId, out var subject);
            var error = _showHomework(item.Homework, subject ?? "", item.OffsetMinutes);
            if (error is null)
            {
                _db.MarkHomeworkNotificationSent(item.Homework.Id, item.FireDate, item.OffsetMinutes);
                continue;
            }

            Report(error, notify: false);
        }
    }

    private void Report(Exception error, bool notify)
    {
        try
        {
            ToastLog.Append(error, _failureLogPath);
        }
        catch (Exception logError)
        {
            error = new AggregateException(error, logError);
        }

        if (!notify && _failureNotified)
        {
            return;
        }

        _failureNotified = true;
        Failed?.Invoke(error);
    }

    private static Exception? TryShow(Lesson lesson, int offsetMinutes)
    {
        var when = offsetMinutes >= 60
            ? "через час"
            : $"через {offsetMinutes} мин.";
        var place = string.IsNullOrWhiteSpace(lesson.Room)
            ? lesson.HasLink ? "онлайн" : ""
            : lesson.Room;
        var body = string.Join(" · ", new[] { lesson.TimeText, place, when }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        try
        {
            var builder = new ToastContentBuilder()
                .AddHeader("unischedule-lessons", "Расписание", "action=open")
                .AddText(lesson.Subject)
                .AddText(body)
                .AddAttributionText("UniSchedule");

            if (!string.IsNullOrWhiteSpace(lesson.Teacher))
            {
                builder.AddText(lesson.Teacher);
            }

            if (MeetingLinks.TryGetWebUri(lesson.PrimaryUrl, out var uri))
            {
                builder.AddButton(new ToastButton()
                    .SetContent("Открыть ссылку")
                    .SetProtocolActivation(uri));
            }

            builder.SetToastDuration(ToastDuration.Short);
            builder.Show();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static Exception? TryShowHomework(Homework homework, string subject, int offsetMinutes)
    {
        var when = NotificationPlanner.HomeworkDueText(offsetMinutes);
        var body = string.IsNullOrWhiteSpace(subject) ? when : $"{subject} · {when}";
        var title = string.IsNullOrWhiteSpace(homework.Title) ? "Домашка" : homework.Title;

        try
        {
            var builder = new ToastContentBuilder()
                .AddHeader("unischedule-homework", "Домашка", "action=open")
                .AddText(title)
                .AddText(body)
                .AddAttributionText("UniSchedule");

            if (TryHomeworkLink(homework, out var uri))
            {
                builder.AddButton(new ToastButton()
                    .SetContent("Открыть ссылку")
                    .SetProtocolActivation(uri));
            }

            builder.SetToastDuration(ToastDuration.Short);
            builder.Show();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static bool TryHomeworkLink(Homework homework, out Uri uri)
    {
        if (MeetingLinks.TryGetWebUri(homework.Url, out uri))
        {
            return true;
        }

        return MeetingLinks.TryGetWebUri(homework.ExtraUrl, out uri);
    }
}
