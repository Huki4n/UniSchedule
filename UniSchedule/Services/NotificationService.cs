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
    private readonly string _failureLogPath;
    private AppSettings _settings;
    private bool _checking;
    private bool _failureNotified;
    private int _pauseDepth;

    public event Action<Exception>? Failed;

    public NotificationService(AppDatabase db, AppSettings settings)
        : this(db, settings, TryShow, ToastLog.DefaultPath)
    {
    }

    internal NotificationService(
        AppDatabase db,
        AppSettings settings,
        Func<Lesson, int, Exception?> show,
        string failureLogPath)
    {
        _db = db;
        _settings = settings;
        _show = show;
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

        var due = NotificationPlanner.Collect(
            _db.GetLessons(_settings.SelectedGroup),
            _settings,
            now,
            _db.WasNotificationSent);
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
}
