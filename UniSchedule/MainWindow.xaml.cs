using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public MainWindow(AppDatabase db, AppSettings settings, NotificationService notifications, bool manageAutostart)
    {
        InitializeComponent();
        _db = db;
        _settings = settings;
        _notifications = notifications;
        _manageAutostart = manageAutostart;
        DaysHost.ItemsSource = _days;
        GroupBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(GroupBox_OnTextChanged));
        GroupBox.DropDownOpened += (_, _) => ResetGroupListIfIdle();
        ReloadGroups();
        ReloadSchedule();
    }

    public void ReloadAll()
    {
        _settings = _db.GetSettings();
        ReloadGroups();
        ReloadSchedule();
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

    private void LessonSearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => ReloadSchedule();

    private void ReloadSchedule()
    {
        var snapshot = ScheduleComposer.Build(
            _db.GetLessons(_settings.SelectedGroup),
            LessonSearchBox.Text,
            DateTime.Now,
            _settings.SemesterStart);
        WeekLabel.Text = snapshot.WeekLabel;
        NextLessonLabel.Text = snapshot.NextLessonText;
        _days.Clear();
        foreach (var day in snapshot.Days)
        {
            _days.Add(day);
        }
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
        ReloadSchedule();
    }

    internal static string? ImportFileOverride { get; set; }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = ImportFileOverride;
        if (path is null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
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

        try
        {
            var result = ItisExcelParser.Parse(path);
            var count = _db.ReplaceImported(result.Lessons);
            AppDialog.Info(this, "Импорт", result.FormatStoredMessage(count, _settings.SelectedGroup));
            ReloadGroups();
            ReloadSchedule();
        }
        catch (Exception ex)
        {
            AppDialog.Info(this, "Импорт не удался", ex.Message);
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
            TestNotification = ShowTestNotification
        };
        if (window.ShowDialog() != true)
        {
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
        ReloadSchedule();
    }

    private void LessonCard_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LessonCardVm card })
        {
            EditLesson(card.Lesson, card.Lesson.DayOfWeek);
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

        if (!AppDialog.Confirm(this, "Удалить пару?", $"«{lesson.Subject}» будет удалена."))
        {
            return;
        }

        _db.DeleteLesson(lesson.Id);
        ReloadSchedule();
    }

    private void OpenLinkMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetLesson(sender) is not { } lesson || !lesson.HasLink)
        {
            AppDialog.Info(this, "Нет ссылки", "У этой пары нет ссылки на занятие.");
            return;
        }

        Process.Start(new ProcessStartInfo(lesson.PrimaryUrl) { UseShellExecute = true });
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

        var window = new LessonEditWindow(lesson, isNew) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (window.Deleted && existing is not null)
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

            _db.UpsertLesson(lesson);
        }

        ReloadSchedule();
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

