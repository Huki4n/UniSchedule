using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UniSchedule.Data;
using UniSchedule.Models;
using UniSchedule.Services;
using UniSchedule.ViewModels;

namespace UniSchedule;

public partial class MainWindow : Window
{
    private readonly AppDatabase _db;
    private readonly NotificationService _notifications;
    private readonly bool _manageAutostart;
    private AppSettings _settings;
    private bool _suppressGroupChange;
    private bool _filteringGroups;
    private List<string> _groups = [];
    private const double EditorMinWidth = 360;
    private const double EditorMaxWidth = 720;
    private double _editorWidth = 440;
    private bool _editorResizing;
    private double _editorResizeX;
    private double _editorResizeWidth;

    private readonly ObservableCollection<DayColumnVm> _days = [];
    private readonly ObservableCollection<BoardRowVm> _rows = [];
    private List<BoardRowVm> _wideRows = [];

    public static readonly DependencyProperty NarrowBoardProperty = DependencyProperty.Register(
        nameof(NarrowBoard),
        typeof(bool),
        typeof(MainWindow),
        new PropertyMetadata(false)
    );

    public bool NarrowBoard
    {
        get => (bool)GetValue(NarrowBoardProperty);
        set => SetValue(NarrowBoardProperty, value);
    }
    private readonly ObservableCollection<MonthWeekVm> _monthWeeks = [];
    private readonly ObservableCollection<HomeworkCardVm> _overdue = [];
    private bool _overdueOpen;
    private Lesson? _openLesson;
    private DispatcherTimer? _clockTimer;
    private DateTime _viewDate = DateTime.Today;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private long? _homeworkLessonFilter;
    private DispatcherTimer? _dayHighlightTimer;
    private HomeworkCommentDraft? _commentDraft;
    private bool _savePrompt;
    private bool _editorClosing;
    private int _editorEpoch;
    private static readonly System.Windows.Media.SolidColorBrush ElectiveHover =
        CreateElectiveHover();

    public MainWindow(
        AppDatabase db,
        AppSettings settings,
        NotificationService notifications,
        bool manageAutostart
    )
    {
        InitializeComponent();
        _db = db;
        _settings = settings;
        _notifications = notifications;
        _manageAutostart = manageAutostart;
        DaysHost.ItemsSource = _days;
        RowsHost.ItemsSource = _rows;
        MonthHost.ItemsSource = _monthWeeks;
        OverdueHost.ItemsSource = _overdue;
        GroupBox.AddHandler(
            System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(GroupBox_OnTextChanged)
        );
        GroupBox.DropDownOpened += (_, _) => ResetGroupListIfIdle();
        ReloadGroups();
        ReloadBoard();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _clockTimer.Tick += (_, _) => RefreshClock();
        _clockTimer.Start();
    }

    public void ReloadAll()
    {
        _settings = _db.GetSettings();
        ReloadGroups();
        ReloadBoard();
    }

    private void ReloadGroups()
    {
        _suppressGroupChange = true;
        _groups = _db.GetGroups();
        if (!_groups.Contains(_settings.SelectedGroup, StringComparer.OrdinalIgnoreCase))
        {
            _groups.Insert(0, _settings.SelectedGroup);
        }

        GroupBox.ItemsSource = _groups;
        GroupBox.SelectedItem =
            _groups.FirstOrDefault(g =>
                string.Equals(g, _settings.SelectedGroup, StringComparison.OrdinalIgnoreCase)
            ) ?? _groups.First();
        GroupBox.Text = _settings.SelectedGroup;
        _suppressGroupChange = false;
    }

    private void GroupBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressGroupChange || _filteringGroups || !GroupBox.IsEditable)
        {
            return;
        }

        var text = GroupBox.Text ?? "";
        if (
            GroupBox.SelectedItem is string selected
            && string.Equals(selected, text, StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        var editor = e.OriginalSource as System.Windows.Controls.TextBox;
        var caret = editor?.CaretIndex ?? text.Length;
        var filtered = string.IsNullOrWhiteSpace(text)
            ? _groups
            : _groups
                .Where(g => g.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

        _filteringGroups = true;
        _suppressGroupChange = true;
        GroupBox.ItemsSource = filtered;
        GroupBox.Text = text;
        if (editor is not null)
        {
            editor.CaretIndex = Math.Min(caret, editor.Text.Length);
        }

        GroupBox.IsDropDownOpen = true;
        _suppressGroupChange = false;
        _filteringGroups = false;
    }

    private void ResetGroupListIfIdle()
    {
        if (_filteringGroups || GroupBox.SelectedItem is not string selected)
        {
            return;
        }

        if (!string.Equals(GroupBox.Text, selected, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _suppressGroupChange = true;
        GroupBox.ItemsSource = _groups;
        GroupBox.SelectedItem = selected;
        _suppressGroupChange = false;
    }

    private void GroupBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressGroupChange || GroupBox.SelectedItem is not string group)
        {
            return;
        }

        _settings.SelectedGroup = group;
        _db.SaveSettings(_settings);
        _notifications.UpdateSettings(_settings);
        _homeworkLessonFilter = null;
        CloseEditor();
        ReloadBoard();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (App.IsExiting || !_settings.MinimizeToTray)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
