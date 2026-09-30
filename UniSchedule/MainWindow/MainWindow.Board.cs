using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using UniSchedule.Models;
using UniSchedule.Services;
using UniSchedule.ViewModels;
using UniSchedule.Windows;

namespace UniSchedule;

public partial class MainWindow
{
    private void LessonSearchBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
        ReloadBoard();

    private void ReloadBoard()
    {
        ReloadSchedule();
        ReloadHomework();
    }

    private void ReloadSchedule()
    {
        ReloadScheduleVisible();
        ReloadElectiveBox();
    }

    private void ReloadScheduleVisible()
    {
        var snapshot = ScheduleComposer.Build(
            _db.GetLessons(_settings.SelectedGroup),
            LessonSearchBox.Text,
            DateTime.Now,
            _settings.SemesterStart,
            _viewDate,
            _db.GetHomework(_settings.SelectedGroup),
            _db.GetElectivePicks(),
            _db.GetElectiveSubjects(_settings.SelectedGroup)
        );
        WeekLabel.Text = snapshot.WeekLabel;
        ApplyHeader(snapshot);
        _days.Clear();
        foreach (var day in snapshot.Days)
        {
            _days.Add(day);
        }

        _wideRows = snapshot.Rows.ToList();
        FillBoardRows();
    }

    private void Board_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width > 0 && e.NewSize.Width < 980;
        if (narrow == NarrowBoard)
        {
            return;
        }

        NarrowBoard = narrow;
        FillBoardRows();
    }

    private void FillBoardRows()
    {
        _rows.Clear();
        if (NarrowBoard)
        {
            foreach (var day in _days)
            {
                _rows.Add(
                    new BoardRowVm
                    {
                        Cells =
                        [
                            new BoardCellVm
                            {
                                Day = day,
                                ShowTitle = true,
                                Cards = day.Lessons.ToList(),
                            },
                        ],
                    }
                );
            }

            return;
        }

        foreach (var row in _wideRows)
        {
            _rows.Add(row);
        }
    }

    private void RefreshClock()
    {
        var snapshot = ScheduleComposer.Build(
            _db.GetLessons(_settings.SelectedGroup),
            LessonSearchBox.Text,
            DateTime.Now,
            _settings.SemesterStart,
            _viewDate,
            _db.GetHomework(_settings.SelectedGroup),
            _db.GetElectivePicks(),
            _db.GetElectiveSubjects(_settings.SelectedGroup)
        );
        ApplyHeader(snapshot);
        var today = DateTime.Today;
        var currentId = snapshot.CurrentLesson?.Id ?? 0;
        foreach (var day in _days)
        {
            day.IsToday = day.Date == today;
            foreach (var card in day.Lessons)
            {
                card.IsCurrent =
                    currentId > 0
                    && day.Date == today
                    && card.Lesson.Id == currentId
                    && !card.IsDimmed;
            }
        }
    }

    private void ApplyHeader(ScheduleSnapshot snapshot)
    {
        _openLesson = snapshot.OpenLesson;
        NextLessonLabel.Text = snapshot.NextLessonText;
        NextLessonLabel.Cursor = _openLesson is null
            ? System.Windows.Input.Cursors.Arrow
            : System.Windows.Input.Cursors.Hand;
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
        var card =
            _days.SelectMany(day => day.Lessons).FirstOrDefault(item => item.Subject == subject)
            ?? throw new InvalidOperationException("Нет пары " + subject);
        EditLesson(card.Lesson, card.Lesson.DayOfWeek);
    }

    internal void OpenFromToast(string argument)
    {
        if (!ToastOpen.TryParse(argument, out var kind, out var id))
        {
            return;
        }

        if (kind == ToastOpen.Lesson)
        {
            var lesson = _db.GetAllLessons().FirstOrDefault(item => item.Id == id);
            if (lesson is null)
            {
                return;
            }

            _viewDate = DateTime.Today;
            UseGroup(lesson.GroupCode);
            MainTabs.SelectedItem = ScheduleTab;
            ReloadBoard();
            EditLesson(lesson, lesson.DayOfWeek);
            return;
        }

        var homework = _db.FindHomework(id);
        if (homework is null)
        {
            return;
        }

        var owner = _db.GetAllLessons().FirstOrDefault(item => item.Id == homework.LessonId);
        if (owner is null)
        {
            return;
        }

        _month = new DateTime(homework.Deadline.Year, homework.Deadline.Month, 1);
        UseGroup(owner.GroupCode);
        MainTabs.SelectedItem = HomeworkTab;
        ReloadBoard();
        EditHomework(homework, homework.LessonId);
    }

    private void UseGroup(string group)
    {
        if (string.Equals(_settings.SelectedGroup, group, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _suppressGroupChange = true;
        _settings.SelectedGroup = group;
        _db.SaveSettings(_settings);
        _notifications.UpdateSettings(_settings);
        _homeworkLessonFilter = null;
        CloseEditor();
        ReloadGroups();
        _suppressGroupChange = false;
        ReloadBoard();
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

        if (
            !AppDialog.Confirm(
                this,
                "Удалить пару?",
                LessonForm.DeleteConfirmText(lesson.Subject, _db.CountHomework(lesson.Id))
            )
        )
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
        if (sender is FrameworkElement { DataContext: CardLinkVm link })
        {
            OpenAddress(link.Url);
        }
    }

    private void NextLesson_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (_openLesson is null)
        {
            return;
        }

        EditLesson(_openLesson, _openLesson.DayOfWeek);
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

    private void ShowScheduleLesson(DateTime date, long lessonId)
    {
        _viewDate = date.Date;
        MainTabs.SelectedItem = ScheduleTab;
        ReloadSchedule();
        PulseLesson(lessonId);
    }

    private void PulseLesson(long lessonId)
    {
        _dayHighlightTimer?.Stop();
        foreach (var day in _days)
        {
            day.IsHighlighted = false;
            foreach (var card in day.Lessons)
            {
                card.IsHighlighted = false;
            }
        }

        var lesson = _db.GetLessons(_settings.SelectedGroup)
            .FirstOrDefault(item => item.Id == lessonId);
        if (lesson is null)
        {
            return;
        }

        var matches = _days
            .SelectMany(day => day.Lessons)
            .Where(card => HomeworkForm.MatchesSlot(lesson, card.Lesson))
            .ToList();
        if (matches.Count == 0)
        {
            return;
        }

        foreach (var card in matches)
        {
            card.IsHighlighted = true;
        }

        _dayHighlightTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _dayHighlightTimer.Tick += (_, _) =>
        {
            _dayHighlightTimer.Stop();
            foreach (var card in matches)
            {
                card.IsHighlighted = false;
            }
        };
        _dayHighlightTimer.Start();
    }

    private static Lesson? GetLesson(object sender)
    {
        if (
            sender
                is System.Windows.Controls.MenuItem
                {
                    Parent: System.Windows.Controls.ContextMenu
                    {
                        PlacementTarget: FrameworkElement element
                    }
                }
            && element.DataContext is LessonCardVm card
        )
        {
            return card.Lesson;
        }

        return null;
    }
}
