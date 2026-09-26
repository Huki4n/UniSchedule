using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using UniSchedule.Data;
using UniSchedule.Models;
using UniSchedule.Services;
using UniSchedule.ViewModels;
using UniSchedule.Windows;

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
    private readonly ObservableCollection<DayColumnVm> _days = [];
    private readonly ObservableCollection<MonthWeekVm> _monthWeeks = [];
    private DateTime _viewDate = DateTime.Today;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private long? _homeworkLessonFilter;
    private DispatcherTimer? _dayHighlightTimer;
    private int _editorEpoch;

    public MainWindow(AppDatabase db, AppSettings settings, NotificationService notifications, bool manageAutostart)
    {
        InitializeComponent();
        _db = db;
        _settings = settings;
        _notifications = notifications;
        _manageAutostart = manageAutostart;
        DaysHost.ItemsSource = _days;
        MonthHost.ItemsSource = _monthWeeks;
        GroupBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(GroupBox_OnTextChanged));
        GroupBox.DropDownOpened += (_, _) => ResetGroupListIfIdle();
        ReloadGroups();
        ReloadBoard();
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
        GroupBox.SelectedItem = _groups.FirstOrDefault(g =>
            string.Equals(g, _settings.SelectedGroup, StringComparison.OrdinalIgnoreCase)) ?? _groups.First();
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
        if (GroupBox.SelectedItem is string selected &&
            string.Equals(selected, text, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var editor = e.OriginalSource as System.Windows.Controls.TextBox;
        var caret = editor?.CaretIndex ?? text.Length;
        var filtered = string.IsNullOrWhiteSpace(text)
            ? _groups
            : _groups.Where(g => g.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

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

    private void LessonSearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => ReloadBoard();

    private void ReloadBoard()
    {
        ReloadSchedule();
        ReloadHomework();
    }

    private void ReloadSchedule()
    {
        var snapshot = ScheduleComposer.Build(
            _db.GetLessons(_settings.SelectedGroup),
            LessonSearchBox.Text,
            DateTime.Now,
            _settings.SemesterStart,
            _viewDate,
            _db.GetHomework(_settings.SelectedGroup));
        WeekLabel.Text = snapshot.WeekLabel;
        NextLessonLabel.Text = snapshot.NextLessonText;
        _days.Clear();
        foreach (var day in snapshot.Days)
        {
            _days.Add(day);
        }
    }

    private void ReloadHomework()
    {
        var lessons = _db.GetLessons(_settings.SelectedGroup);
        if (_homeworkLessonFilter is long filter && lessons.All(lesson => lesson.Id != filter))
        {
            _homeworkLessonFilter = null;
        }

        var snapshot = HomeworkCalendar.Build(
            _db.GetHomework(_settings.SelectedGroup),
            lessons,
            _month,
            DateTime.Now,
            LessonSearchBox.Text,
            _homeworkLessonFilter);
        MonthLabel.Text = snapshot.MonthLabel;
        var visible = snapshot.Weeks.Sum(week => week.Days.Sum(day => day.Items.Count));
        HomeworkEmptyLabel.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_homeworkLessonFilter is long lessonId)
        {
            var subject = lessons.First(lesson => lesson.Id == lessonId).Subject;
            HomeworkFilterLabel.Text = $"Только пара «{subject}»";
            HomeworkFilterBar.Visibility = Visibility.Visible;
        }
        else
        {
            HomeworkFilterBar.Visibility = Visibility.Collapsed;
        }

        _monthWeeks.Clear();
        foreach (var week in snapshot.Weeks)
        {
            _monthWeeks.Add(week);
        }
    }

    private void PrevWeek_Click(object sender, RoutedEventArgs e)
    {
        _viewDate = AcademicCalendar.StartOfWeek(_viewDate).AddDays(-7);
        ReloadSchedule();
    }

    private void NextWeek_Click(object sender, RoutedEventArgs e)
    {
        _viewDate = AcademicCalendar.StartOfWeek(_viewDate).AddDays(7);
        ReloadSchedule();
    }

    private void TodayWeek_Click(object sender, RoutedEventArgs e)
    {
        _viewDate = DateTime.Today;
        ReloadSchedule();
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(-1);
        ReloadHomework();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(1);
        ReloadHomework();
    }

    private void ClearHomeworkFilter_Click(object sender, RoutedEventArgs e)
    {
        _homeworkLessonFilter = null;
        ReloadHomework();
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
        ReloadBoard();
    }

    internal static string? ImportFileOverride { get; set; }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (ImportPanel.Visibility == Visibility.Visible)
        {
            return;
        }

        var path = ImportFileOverride;
        if (path is null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                Title = "Импорт расписания ИТИС",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            path = dialog.FileName;
        }

        var selectedGroup = _settings.SelectedGroup;
        ImportStatus.Text = "Читаю лист";
        ImportPanel.Visibility = Visibility.Visible;
        _notifications.Pause();
        try
        {
            var progress = new Progress<string>(text => ImportStatus.Text = text);
            var result = await Task.Run(() => ItisExcelParser.Parse(path, progress));
            ImportStatus.Text = "Записываю в базу";
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            var count = await Task.Run(() => _db.ReplaceImported(result.Lessons));
            ImportPanel.Visibility = Visibility.Collapsed;
            AppDialog.Info(this, "Импорт", result.FormatStoredMessage(count, selectedGroup));
            CloseEditor();
            ReloadGroups();
            ReloadBoard();
        }
        catch (Exception ex)
        {
            ImportPanel.Visibility = Visibility.Collapsed;
            AppDialog.Info(this, "Импорт не удался", ex.Message);
        }
        finally
        {
            _notifications.Resume();
        }
    }

    private void ShowTestNotification()
    {
        var lessons = _db.GetLessons(_settings.SelectedGroup);
        var next = lessons
            .Where(l => AcademicCalendar.AppliesOnDate(l, DateTime.Now, _settings.SemesterStart)
                        && DateTime.Today + l.Start > DateTime.Now)
            .OrderBy(l => l.Start)
            .FirstOrDefault()
            ?? lessons.FirstOrDefault();
        _notifications.ShowTest(next);
    }

    private void Add_Click(object sender, RoutedEventArgs e) => EditLesson(null, DateTime.Today.DayOfWeek);

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var groups = _db.GetGroups();
        if (!groups.Contains(_settings.SelectedGroup, StringComparer.OrdinalIgnoreCase))
        {
            groups.Insert(0, _settings.SelectedGroup);
        }

        var window = new SettingsWindow(_settings.Clone(), groups)
        {
            Owner = this,
            TestNotification = ShowTestNotification,
            ClearStoredData = _db.ClearStoredData
        };
        if (window.ShowDialog() != true)
        {
            if (window.DataCleared)
            {
                CloseEditor();
                ReloadGroups();
                ReloadBoard();
            }

            return;
        }

        _settings = window.Settings;
        _db.SaveSettings(_settings);
        if (_manageAutostart)
        {
            AutostartService.Apply(_settings.Autostart);
        }
        _notifications.UpdateSettings(_settings);
        ReloadGroups();
        ReloadBoard();
    }

    private void LessonCard_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LessonCardVm card })
        {
            EditLesson(card.Lesson, card.Lesson.DayOfWeek);
        }
    }

    private void LessonHomework_OnClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: HomeworkLinkVm link })
        {
            EditHomework(link.Homework, link.Homework.LessonId);
        }
    }

    internal void OpenLesson(string subject)
    {
        var card = _days
            .SelectMany(day => day.Lessons)
            .FirstOrDefault(item => item.Subject == subject)
            ?? throw new InvalidOperationException("Нет пары " + subject);
        EditLesson(card.Lesson, card.Lesson.DayOfWeek);
    }

    private void EditMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetLesson(sender) is { } lesson)
        {
            EditLesson(lesson, lesson.DayOfWeek);
        }
    }

    private void DeleteMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetLesson(sender) is not { } lesson)
        {
            return;
        }

        if (!AppDialog.Confirm(this, "Удалить пару?", LessonForm.DeleteConfirmText(lesson.Subject, _db.CountHomework(lesson.Id))))
        {
            return;
        }

        _db.DeleteLesson(lesson.Id);
        CloseEditorIfStale(lesson.Id, homeworkId: null);
        ReloadBoard();
    }

    private void OpenLinkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetLesson(sender) is not { } lesson || !lesson.HasLink)
        {
            AppDialog.Info(this, "Нет ссылки", "У этой пары нет ссылки на занятие.");
            return;
        }

        OpenAddress(lesson.PrimaryUrl);
    }

    private void CardLink_OnClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: string url })
        {
            OpenAddress(url);
        }
    }

    private void OpenAddress(string url)
    {
        if (!MeetingLinks.TryGetWebUri(url, out var uri))
        {
            AppDialog.Info(this, "Ссылка", "Открываются только адреса http и https.");
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void EditLesson(Lesson? existing, DayOfWeek day)
    {
        var isNew = existing is null;
        var lesson = existing?.Clone() ?? new Lesson
        {
            GroupCode = _settings.SelectedGroup,
            DayOfWeek = day is DayOfWeek.Sunday ? DayOfWeek.Monday : day,
            Source = LessonCodes.Manual
        };

        var groupLessons = _db.GetLessons(_settings.SelectedGroup);
        var homework = existing is null
            ? []
            : _db.GetHomework(_settings.SelectedGroup).Where(item => item.LessonId == existing.Id).ToList();
        var hasRelated = existing is not null && LessonSeries.HasOthers(groupLessons, existing);
        var rollback = existing is null ? null : _db.GetSubjectRollback(existing.Id, DateTime.Today);
        var editor = new LessonEditWindow(lesson, isNew, homework, hasRelated, rollback);
        editor.Finished += (_, _) => HideEditor(() => ApplyLesson(editor, existing, lesson, isNew, groupLessons));
        ShowEditor(editor, isNew ? "Новая пара" : "Редактирование пары");
    }

    private void ApplyLesson(LessonEditWindow editor, Lesson? existing, Lesson lesson, bool isNew, List<Lesson> groupLessons)
    {
        if (editor.OpenHomework is not null)
        {
            EditHomework(editor.OpenHomework, editor.OpenHomework.LessonId);
            return;
        }

        if (!editor.Accepted)
        {
            return;
        }

        if (editor.Deleted && existing is not null)
        {
            _db.DeleteLesson(existing.Id);
        }
        else
        {
            if (isNew)
            {
                lesson.GroupCode = _settings.SelectedGroup;
                lesson.Source = LessonCodes.Manual;
            }

            var affected = existing is null
                ? []
                : LessonSeries.Select(groupLessons, existing, editor.Scope);
            var previousSubjects = affected.ToDictionary(item => item.Id, item => item.Subject);
            var batch = new List<Lesson> { lesson };
            foreach (var other in affected)
            {
                LessonSeries.CopyShared(lesson, other);
                batch.Add(other);
            }

            _db.UpsertLessons(batch);
            RememberSubjectNames(existing, lesson, previousSubjects);
        }

        ReloadBoard();
    }

    private void HomeworkOfLesson_Click(object sender, RoutedEventArgs e)
    {
        if (GetLesson(sender) is not { } lesson)
        {
            return;
        }

        var items = _db.GetHomework(_settings.SelectedGroup)
            .Where(item => item.LessonId == lesson.Id)
            .ToList();
        _homeworkLessonFilter = lesson.Id;
        if (items.Count > 0)
        {
            var nearest = HomeworkCalendar.NearestDeadline(items, DateTime.Today);
            _month = new DateTime(nearest.Year, nearest.Month, 1);
        }

        MainTabs.SelectedItem = HomeworkTab;
        ReloadHomework();
    }

    private void AddHomeworkForLesson_Click(object sender, RoutedEventArgs e)
    {
        if (GetLesson(sender) is { } lesson)
        {
            EditHomework(null, lesson.Id);
        }
    }

    private void AddHomework_Click(object sender, RoutedEventArgs e) => EditHomework(null, _homeworkLessonFilter);

    private void HomeworkDay_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MonthDayVm day } || !day.IsCurrentMonth || day.Items.Count > 0)
        {
            return;
        }

        EditHomework(null, _homeworkLessonFilter, day.Date);
    }

    private void HomeworkCard_OnClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: HomeworkCardVm card })
        {
            EditHomework(card.Homework, card.Homework.LessonId);
        }
    }

    private void EditHomeworkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetHomeworkCard(sender) is { } card)
        {
            EditHomework(card.Homework, card.Homework.LessonId);
        }
    }

    private void DeleteHomeworkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetHomeworkCard(sender) is not { } card)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(card.Title) ? "Домашка будет удалена." : $"«{card.Title}» будет удалена.";
        if (!AppDialog.Confirm(this, "Удалить домашку?", name))
        {
            return;
        }

        _db.DeleteHomework(card.Homework.Id);
        CloseEditorIfStale(lessonId: null, card.Homework.Id);
        ReloadBoard();
    }

    private void HomeworkWeekMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetHomeworkCard(sender) is { } card)
        {
            ShowScheduleDay(card.Homework.Deadline);
        }
    }

    private void ShowScheduleDay(DateTime date)
    {
        _viewDate = date.Date;
        MainTabs.SelectedItem = ScheduleTab;
        ReloadSchedule();
        PulseDay(date.Date);
    }

    private void PulseDay(DateTime date)
    {
        _dayHighlightTimer?.Stop();
        foreach (var day in _days)
        {
            day.IsHighlighted = false;
        }

        if (date.DayOfWeek == DayOfWeek.Sunday)
        {
            return;
        }

        var column = _days.FirstOrDefault(day => day.Date == date);
        if (column is null)
        {
            return;
        }

        column.IsHighlighted = true;
        _dayHighlightTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _dayHighlightTimer.Tick += (_, _) =>
        {
            _dayHighlightTimer.Stop();
            column.IsHighlighted = false;
        };
        _dayHighlightTimer.Start();
    }

    private void EditHomework(Homework? existing, long? lessonId, DateTime? deadline = null)
    {
        var lessons = _db.GetLessons(_settings.SelectedGroup);
        if (lessons.Count == 0)
        {
            AppDialog.Info(this, "Нет пар", HomeworkForm.NoLessonsMessage);
            return;
        }

        var isNew = existing is null;
        var homework = existing?.Clone() ?? new Homework
        {
            LessonId = lessonId ?? 0,
            Deadline = deadline?.Date ?? DateTime.Today
        };

        var comments = homework.Id > 0 ? _db.GetHomeworkComments(homework.Id) : [];
        var draft = new HomeworkCommentDraft(comments);
        var editor = new HomeworkEditWindow(homework, isNew, lessons, comments);
        editor.CommentAdded += (_, text) =>
        {
            draft.Add(text, DateTime.Now);
            editor.SetComments(draft.Visible);
        };
        editor.CommentRemoved += (_, id) =>
        {
            draft.Remove(id);
            editor.SetComments(draft.Visible);
        };
        editor.Finished += (_, _) => HideEditor(() => ApplyHomework(editor, existing, homework, draft));
        ShowEditor(editor, isNew ? "Новая домашка" : "Редактирование домашки");
    }

    private void ApplyHomework(HomeworkEditWindow editor, Homework? existing, Homework homework, HomeworkCommentDraft draft)
    {
        if (editor.OpenSchedule)
        {
            ShowScheduleDay(editor.ScheduleDate);
            return;
        }

        if (!editor.Accepted)
        {
            return;
        }

        if (editor.Deleted && existing is not null)
        {
            _db.DeleteHomework(existing.Id);
        }
        else
        {
            _db.SaveHomeworkWithComments(homework, draft.RemovedIds, draft.Added);
        }

        ReloadBoard();
    }

    private void ShowEditor(UIElement editor, string title)
    {
        _editorEpoch++;
        EditorTitle.Text = title;
        var opening = EditorPanel.Visibility != Visibility.Visible;
        EditorHost.Content = editor;
        EditorPanel.Visibility = Visibility.Visible;
        if (opening)
        {
            EditorShift.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(560, 0, TimeSpan.FromMilliseconds(180)));
        }
        else
        {
            EditorShift.BeginAnimation(TranslateTransform.XProperty, null);
            EditorShift.X = 0;
        }
    }

    private void HideEditor(Action? then)
    {
        var epoch = _editorEpoch;
        var animation = new DoubleAnimation(0, 560, TimeSpan.FromMilliseconds(160));
        animation.Completed += (_, _) =>
        {
            if (epoch != _editorEpoch)
            {
                return;
            }

            EditorPanel.Visibility = Visibility.Collapsed;
            EditorHost.Content = null;
            then?.Invoke();
        };
        EditorShift.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void CloseEditor()
    {
        if (EditorPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        _editorEpoch++;
        EditorShift.BeginAnimation(TranslateTransform.XProperty, null);
        EditorShift.X = 560;
        EditorPanel.Visibility = Visibility.Collapsed;
        EditorHost.Content = null;
    }

    private void CloseEditorIfStale(long? lessonId, long? homeworkId)
    {
        if (EditorHost.Content is LessonEditWindow lesson &&
            lessonId is long openLesson &&
            lesson.Result.Id == openLesson)
        {
            CloseEditor();
            return;
        }

        if (EditorHost.Content is not HomeworkEditWindow homework)
        {
            return;
        }

        if (homeworkId is long openHomework && homework.Result.Id == openHomework)
        {
            CloseEditor();
            return;
        }

        if (lessonId is long owner && homework.Result.LessonId == owner)
        {
            CloseEditor();
        }
    }

    private void CloseEditor_Click(object sender, RoutedEventArgs e)
    {
        switch (EditorHost.Content)
        {
            case LessonEditWindow lesson:
                lesson.RequestCancel();
                break;
            case HomeworkEditWindow homework:
                homework.RequestCancel();
                break;
        }
    }

    private void RememberSubjectNames(Lesson? original, Lesson saved, IReadOnlyDictionary<long, string> previousSubjects)
    {
        if (original is null)
        {
            return;
        }

        var today = DateTime.Today;
        RememberSubjectName(original.Id, original.Subject, saved.Subject, today);
        foreach (var (lessonId, previous) in previousSubjects)
        {
            RememberSubjectName(lessonId, previous, saved.Subject, today);
        }
    }

    private void RememberSubjectName(long lessonId, string previous, string next, DateTime today)
    {
        var stored = _db.GetSubjectRollback(lessonId, today);
        switch (SubjectRollbackPolicy.Decide(previous, stored, next))
        {
            case SubjectRollbackDecision.Forget:
                _db.ForgetSubjectRollback(lessonId);
                break;
            case SubjectRollbackDecision.Remember:
                _db.RememberSubjectRollback(lessonId, SubjectRollbackPolicy.OriginalName(previous, stored), today);
                break;
        }
    }

    private static Lesson? GetLesson(object sender)
    {
        if (sender is System.Windows.Controls.MenuItem { Parent: System.Windows.Controls.ContextMenu { PlacementTarget: FrameworkElement element } } &&
            element.DataContext is LessonCardVm card)
        {
            return card.Lesson;
        }

        return null;
    }

    private static HomeworkCardVm? GetHomeworkCard(object sender)
    {
        if (sender is System.Windows.Controls.MenuItem { Parent: System.Windows.Controls.ContextMenu { PlacementTarget: FrameworkElement element } } &&
            element.DataContext is HomeworkCardVm card)
        {
            return card;
        }

        return null;
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

