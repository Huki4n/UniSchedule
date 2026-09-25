using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace UniSchedule.Tests.E2E;

public sealed class AppHarness : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private App? _app;
    private Exception? _error;

    private AppHarness()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "UniSchedule.E2E"
        };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    public static AppHarness Start(string databasePath)
    {
        App.ArgsOverride = ["--data", databasePath];
        var harness = new AppHarness();
        harness._thread.Start();
        harness.WaitFor("Main", TimeSpan.FromSeconds(20));
        return harness;
    }

    public static string ExePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var configuration = baseDir.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UniSchedule.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Не найден корень решения.");
        }

        var exe = Path.Combine(
            dir.FullName,
            "UniSchedule",
            "bin",
            configuration,
            "net10.0-windows10.0.17763.0",
            "UniSchedule.exe");
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException("Сначала соберите приложение.", exe);
        }

        return exe;
    }

    public string Text(string automationId) =>
        OnUi(() => Find<TextBlock>(automationId).Text);

    public void SetText(string automationId, string value) =>
        OnUi(() => Find<TextBox>(automationId).Text = value);

    public void SelectIndex(string automationId, int index) =>
        OnUi(() => Find<ComboBox>(automationId).SelectedIndex = index);

    public void SelectItem(string automationId, string value) =>
        OnUi(() => Find<ComboBox>(automationId).SelectedItem = value);

    public void Uncheck(string automationId) =>
        OnUi(() => Find<CheckBox>(automationId).IsChecked = false);

    public void Press(string automationId) =>
        BeginUi(() => Find<Button>(automationId).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

    public void OpenLesson(string subject) =>
        BeginUi(() => ((MainWindow)FindWindow("Main")!).OpenLesson(subject));

    public void WaitFor(string automationId, TimeSpan? timeout = null) =>
        Wait(() => OnUi(() => FindWindow(automationId) is not null), "Нет окна " + automationId, timeout);

    public void WaitGone(string automationId) =>
        Wait(() => OnUi(() => FindWindow(automationId) is null), "Окно не закрылось: " + automationId, TimeSpan.FromSeconds(10));

    public void CloseMain() => OnUi(() => FindWindow("Main")!.Close());

    public bool Exited(TimeSpan timeout) => _thread.Join(timeout);

    public void Dispose()
    {
        try
        {
            if (_app is not null && _thread.IsAlive)
            {
                _app.Dispatcher.Invoke(() => _app.Shutdown());
            }
        }
        catch (Exception)
        {
        }

        _thread.Join(TimeSpan.FromSeconds(5));
        App.ArgsOverride = null;
    }

    private void Run()
    {
        try
        {
            if (Application.ResourceAssembly is null)
            {
                Application.ResourceAssembly = typeof(App).Assembly;
            }

            _app = new App();
            _app.InitializeComponent();
        }
        catch (Exception ex)
        {
            _error = ex;
            _ready.Set();
            return;
        }

        _ready.Set();
        try
        {
            _app.Run();
        }
        catch (Exception ex)
        {
            _error = ex;
        }
    }

    private void OnUi(Action action) => OnUi(() =>
    {
        action();
        return true;
    });

    private T OnUi<T>(Func<T> action)
    {
        var operation = Current().Dispatcher.InvokeAsync(action);
        if (!operation.Task.Wait(TimeSpan.FromSeconds(8)))
        {
            throw new TimeoutException(_error is null
                ? "Интерфейс не ответил."
                : "Интерфейс не ответил: " + _error.Message);
        }

        return operation.Task.GetAwaiter().GetResult();
    }

    private void BeginUi(Action action)
    {
        Current().Dispatcher.BeginInvoke(action);
        Thread.Sleep(200);
    }

    private App Current()
    {
        if (!_ready.Wait(TimeSpan.FromSeconds(20)))
        {
            throw new TimeoutException("Приложение не запустилось.");
        }

        if (_error is not null && _app is null)
        {
            throw new InvalidOperationException("Приложение не запустилось.", _error);
        }

        return _app ?? throw new InvalidOperationException("Приложение ещё не создано.");
    }

    private static Window? FindWindow(string automationId)
    {
        if (Application.Current is null)
        {
            return null;
        }

        foreach (Window window in Application.Current.Windows)
        {
            if (AutomationProperties.GetAutomationId(window) == automationId && window.IsLoaded)
            {
                return window;
            }
        }

        return null;
    }

    private static T Find<T>(string automationId)
        where T : FrameworkElement =>
        Find<T>(element => AutomationProperties.GetAutomationId(element) == automationId);

    private static T Find<T>(Func<T, bool> match)
        where T : FrameworkElement
    {
        foreach (Window window in Application.Current.Windows)
        {
            var found = Find(window, match);
            if (found is not null)
            {
                return found;
            }
        }

        throw new InvalidOperationException("Не найден элемент " + typeof(T).Name);
    }

    private static T? Find<T>(DependencyObject root, Func<T, bool> match)
        where T : FrameworkElement
    {
        if (root is T candidate && match(candidate))
        {
            return candidate;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = Find(VisualTreeHelper.GetChild(root, i), match);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void Wait(Func<bool> condition, string message, TimeSpan? timeout)
    {
        var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < until)
        {
            if (_error is not null)
            {
                throw new InvalidOperationException(message, _error);
            }

            if (condition())
            {
                return;
            }

            Thread.Sleep(50);
        }

        if (_error is not null)
        {
            throw new InvalidOperationException(message, _error);
        }

        throw new TimeoutException(message);
    }
}
