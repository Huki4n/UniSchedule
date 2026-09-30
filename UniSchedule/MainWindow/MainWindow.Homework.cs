using System.Windows;
using System.Windows.Input;
using UniSchedule.Models;
using UniSchedule.Services;
using UniSchedule.ViewModels;
using UniSchedule.Windows;

namespace UniSchedule;

public partial class MainWindow
{
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
            _homeworkLessonFilter
        );
        MonthLabel.Text = snapshot.MonthLabel;
        var visible = snapshot.Weeks.Sum(week => week.Days.Sum(day => day.Items.Count));
        HomeworkEmptyLabel.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        _overdue.Clear();
        foreach (var card in snapshot.Overdue)
        {
            _overdue.Add(card);
        }

        var hasOverdue = snapshot.Overdue.Count > 0;
        if (!hasOverdue)
        {
            _overdueOpen = false;
        }

        OverdueLabel.Text = HomeworkCalendar.OverdueLabel(snapshot.Overdue.Count);
        OverdueBar.Visibility = hasOverdue ? Visibility.Visible : Visibility.Collapsed;
        OverdueList.Visibility = _overdueOpen ? Visibility.Visible : Visibility.Collapsed;
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

    private void AddHomework_Click(object sender, RoutedEventArgs e) =>
        EditHomework(null, _homeworkLessonFilter);

    private void HomeworkDay_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (
            sender is not FrameworkElement { DataContext: MonthDayVm day }
            || !day.IsCurrentMonth
            || day.Items.Count > 0
        )
        {
            return;
        }

        EditHomework(null, _homeworkLessonFilter, day.Date);
    }

    private void OverdueBar_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _overdueOpen = !_overdueOpen;
        OverdueList.Visibility = _overdueOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HomeworkMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (
            sender is not ContextMenu menu
            || menu.PlacementTarget is not FrameworkElement { DataContext: HomeworkCardVm card }
        )
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is "done")
            {
                item.Visibility = card.IsDone ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (item.Tag is "undo")
            {
                item.Visibility = card.IsDone ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void MarkHomeworkDone_Click(object sender, RoutedEventArgs e)
    {
        if (
            GetHomeworkCard(sender) is not { } card
            || card.Homework.Id <= 0
            || sender is not MenuItem { Tag: string tag }
            || tag is not ("done" or "undo")
        )
        {
            return;
        }

        var done = tag == "done";
        _db.SetHomeworkDone(card.Homework.Id, done);
        if (EditorHost.Content is HomeworkEditWindow editor && editor.Result.Id == card.Homework.Id)
        {
            editor.ApplyDone(done);
        }

        ReloadBoard();
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

        var name = string.IsNullOrWhiteSpace(card.Title)
            ? "Домашка будет удалена."
            : $"«{card.Title}» будет удалена.";
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
            ShowScheduleLesson(card.Homework.Deadline, card.Homework.LessonId);
        }
    }

    private bool EditHomework(Homework? existing, long? lessonId, DateTime? deadline = null)
    {
        var lessons = _db.GetLessons(_settings.SelectedGroup);
        if (lessons.Count == 0)
        {
            AppDialog.Info(this, "Нет пар", HomeworkForm.NoLessonsMessage);
            return false;
        }

        var isNew = existing is null;
        var homework =
            existing?.Clone()
            ?? new Homework
            {
                LessonId = lessonId ?? 0,
                Deadline = deadline?.Date ?? DateTime.Today,
            };

        var comments = homework.Id > 0 ? _db.GetHomeworkComments(homework.Id) : [];
        var draft = new HomeworkCommentDraft(comments);
        _commentDraft = draft;
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
        editor.Finished += (_, _) =>
        {
            if (ApplyHomework(editor, existing, homework, draft))
            {
                HideEditor();
            }
        };
        ShowEditor(editor, isNew ? "Новая домашка" : "Редактирование домашки");
        return true;
    }

    private bool ApplyHomework(
        HomeworkEditWindow editor,
        Homework? existing,
        Homework homework,
        HomeworkCommentDraft draft
    )
    {
        if (editor.OpenSchedule)
        {
            ShowScheduleLesson(editor.ScheduleDate, editor.ScheduleLessonId);
            return true;
        }

        if (!editor.Accepted)
        {
            return true;
        }

        if (editor.Deleted && existing is not null)
        {
            _db.DeleteHomework(existing.Id);
        }
        else
        {
            var lessons = _db.GetLessons(_settings.SelectedGroup);
            if (!HomeworkForm.LessonExists(lessons, homework.LessonId))
            {
                AppDialog.Info(this, "Нельзя сохранить", HomeworkForm.MissingLessonMessage);
                return false;
            }

            _db.SaveHomeworkWithComments(homework, draft.RemovedIds, draft.Added);
        }

        ReloadBoard();
        return true;
    }

    private static HomeworkCardVm? GetHomeworkCard(object sender)
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
            && element.DataContext is HomeworkCardVm card
        )
        {
            return card;
        }

        return null;
    }
}
