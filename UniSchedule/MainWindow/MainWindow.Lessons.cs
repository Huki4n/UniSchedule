using System.Windows;
using System.Windows.Threading;
using UniSchedule.Models;
using UniSchedule.Services;
using UniSchedule.Windows;

namespace UniSchedule;

public partial class MainWindow
{
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
                InitialDirectory =
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    + @"\Downloads",
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
            var count = await Task.Run(() => _db.ReplaceImported(result.Lessons, result.Groups));
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
        var next =
            lessons
                .Where(l =>
                    AcademicCalendar.AppliesOnDate(l, DateTime.Now, _settings.SemesterStart)
                    && DateTime.Today + l.Start > DateTime.Now
                )
                .OrderBy(l => l.Start)
                .FirstOrDefault()
            ?? lessons.FirstOrDefault();
        _notifications.ShowTest(next);
    }

    private void Add_Click(object sender, RoutedEventArgs e) =>
        EditLesson(null, DateTime.Today.DayOfWeek);

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
            ClearStoredData = _db.ClearStoredData,
            CopyDatabase = _db.BackupTo,
            RestoreDatabase = _db.RestoreFrom,
        };
        if (window.ShowDialog() != true)
        {
            if (window.DatabaseRestored)
            {
                CloseEditor();
                _settings = _db.GetSettings();
                if (_manageAutostart)
                {
                    AutostartService.Apply(_settings.Autostart);
                }

                _notifications.UpdateSettings(_settings);
                ReloadGroups();
                ReloadBoard();
            }
            else if (window.DataCleared)
            {
                CloseEditor();
                ReloadGroups();
                ReloadBoard();
            }

            return;
        }

        var previousGroup = _settings.SelectedGroup;
        _settings = window.Settings;
        _db.SaveSettings(_settings);
        if (_manageAutostart)
        {
            AutostartService.Apply(_settings.Autostart);
        }
        _notifications.UpdateSettings(_settings);
        if (
            !string.Equals(
                previousGroup,
                _settings.SelectedGroup,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            CloseEditor();
        }

        ReloadGroups();
        ReloadBoard();
    }

    private void EditLesson(Lesson? existing, DayOfWeek day)
    {
        var isNew = existing is null;
        var lesson =
            existing?.Clone()
            ?? new Lesson
            {
                GroupCode = _settings.SelectedGroup,
                DayOfWeek = day is DayOfWeek.Sunday ? DayOfWeek.Monday : day,
                Source = LessonCodes.Manual,
            };

        var groupLessons = _db.GetLessons(_settings.SelectedGroup);
        var homework = existing is null
            ? []
            : _db.GetHomework(_settings.SelectedGroup)
                .Where(item => item.LessonId == existing.Id)
                .ToList();
        var hasRelated = existing is not null && LessonSeries.HasOthers(groupLessons, existing);
        var rollback = existing is null
            ? null
            : _db.GetSubjectRollback(existing.Id, DateTime.Today);
        _commentDraft = null;
        var editor = new LessonEditWindow(lesson, isNew, homework, hasRelated, rollback);
        editor.Finished += (_, _) =>
        {
            if (ApplyLesson(editor, existing, lesson, isNew))
            {
                HideEditor();
            }
        };
        ShowEditor(editor, isNew ? "Новая пара" : "Редактирование пары");
    }

    private bool ApplyLesson(LessonEditWindow editor, Lesson? existing, Lesson lesson, bool isNew)
    {
        if (editor.OpenHomework is not null)
        {
            return !EditHomework(editor.OpenHomework, editor.OpenHomework.LessonId);
        }

        if (!editor.Accepted)
        {
            return true;
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

            var groupLessons = _db.GetLessons(_settings.SelectedGroup);
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
            foreach (var item in batch)
            {
                if (!string.IsNullOrWhiteSpace(item.ElectiveKey))
                {
                    _db.SetElectivePick(item.GroupCode, item.DayOfWeek, item.Start, item.Subject);
                }
            }

            RememberSubjectNames(existing, lesson, previousSubjects);
        }

        ReloadBoard();
        return true;
    }

    private void RememberSubjectNames(
        Lesson? original,
        Lesson saved,
        IReadOnlyDictionary<long, string> previousSubjects
    )
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
                _db.RememberSubjectRollback(
                    lessonId,
                    SubjectRollbackPolicy.OriginalName(previous, stored),
                    today
                );
                break;
        }
    }
}
