using System.IO;
using System.Threading;
using System.Windows;
using UniSchedule.Data;
using UniSchedule.Services;
using UniSchedule.Windows;

namespace UniSchedule;

public partial class App : System.Windows.Application
{
    public static bool IsExiting { get; private set; }

    internal static string[]? ArgsOverride { get; set; }

    private int _reportingUiError;
    private bool _ownsMutex;
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private TrayService? _tray;
    private NotificationService? _notifications;
    private MainWindow? _main;
    private Thread? _signalThread;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrash(args.Exception);
            args.Handled = true;
            if (Interlocked.Exchange(ref _reportingUiError, 1) == 1)
            {
                return;
            }

            try
            {
                AppDialog.Info(MainWindow, "Ошибка", args.Exception.Message);
            }
            finally
            {
                _reportingUiError = 0;
            }
        };

        base.OnStartup(e);
        var args = ArgsOverride ?? e.Args;
        try
        {
            if (TryHeadlessImport(args))
            {
                Shutdown();
                return;
            }

            var dataPath = AppLaunch.ReadDataPath(args);
            _mutex = new Mutex(true, AppLaunch.MutexName(dataPath), out var created);
            _ownsMutex = created;
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppLaunch.ShowEventName(dataPath));

            if (!created)
            {
                _showEvent.Set();
                Shutdown();
                return;
            }

            var db = dataPath is null ? new AppDatabase() : new AppDatabase(dataPath);
            var settings = db.GetSettings();
            db.SaveSettings(settings);
            if (dataPath is null)
            {
                AutostartService.Apply(settings.Autostart);
            }

            _notifications = new NotificationService(db, settings);
            _notifications.Failed += OnToastFailed;
            _notifications.Start();

            _main = new MainWindow(db, settings, _notifications, manageAutostart: dataPath is null);
            MainWindow = _main;
            _main.Show();

            _tray = new TrayService();
            _tray.OpenRequested += ShowMain;
            _tray.ExitRequested += ExitApp;

            _signalThread = new Thread(ListenForShow)
            {
                IsBackground = true,
                Name = "UniSchedule.ShowListener"
            };
            _signalThread.Start();
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            AppDialog.Info(MainWindow, "Ошибка запуска", ex.Message);
            Shutdown();
        }
    }

    private static void WriteCrash(Exception ex)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "unischedule-crash.txt"),
                ex.ToString());
        }
        catch
        {
            // ignore
        }
    }

    private void ListenForShow()
    {
        while (_showEvent?.WaitOne() == true && !IsExiting)
        {
            Dispatcher.Invoke(ShowMain);
        }
    }

    private void OnToastFailed(Exception exception)
    {
        Window? owner = null;
        foreach (Window window in Windows)
        {
            if (window.IsActive)
            {
                owner = window;
                break;
            }
        }

        AppDialog.Info(owner ?? (MainWindow is { IsLoaded: true } ? MainWindow : null),
            "Уведомление не показано",
            exception.Message);
    }

    private void ShowMain()
    {
        if (_main is null)
        {
            return;
        }

        if (!_main.IsVisible)
        {
            _main.Show();
        }

        if (_main.WindowState == WindowState.Minimized)
        {
            _main.WindowState = WindowState.Normal;
        }

        _main.Activate();
    }

    private void ExitApp()
    {
        IsExiting = true;
        _showEvent?.Set();
        Shutdown();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        IsExiting = true;
        _notifications?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
        _showEvent?.Dispose();
    }

    private static bool TryHeadlessImport(string[] args)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, "--import", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
        {
            return false;
        }

        var path = args[index + 1];
        var report = Path.Combine(Path.GetTempPath(), "unischedule-import.txt");
        var outIndex = Array.FindIndex(args, a => string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase));
        if (outIndex >= 0 && outIndex + 1 < args.Length)
        {
            report = args[outIndex + 1];
        }

        try
        {
            var result = ItisExcelParser.Parse(path);
            var dataPath = AppLaunch.ReadDataPath(args);
            var db = dataPath is null ? new AppDatabase() : new AppDatabase(dataPath);
            var count = db.ReplaceImported(result.Lessons);
            var settings = db.GetSettings();
            db.SaveSettings(settings);
            File.WriteAllText(report,
                $"OK groups={result.Groups.Count} lessons={count} links={result.LinkMatches}{Environment.NewLine}" +
                $"group={settings.SelectedGroup}{Environment.NewLine}" +
                string.Join(Environment.NewLine, result.Groups));
        }
        catch (Exception ex)
        {
            File.WriteAllText(report, "ERROR " + ex);
        }

        return true;
    }
}
