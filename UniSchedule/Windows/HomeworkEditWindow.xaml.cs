using System.Windows;
using System.Windows.Controls;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class HomeworkEditWindow : System.Windows.Controls.UserControl
{
    private readonly Homework _homework;
    private readonly bool _isNew;
    private HomeworkForm.Fields _baseline;

    public bool Deleted { get; private set; }

    public bool Accepted { get; private set; }

    public bool OpenSchedule { get; private set; }

    public DateTime ScheduleDate { get; private set; }

    public long ScheduleLessonId { get; private set; }

    public event EventHandler? Finished;

    private bool _finished;

    public event EventHandler<string>? CommentAdded;

    public event EventHandler<long>? CommentRemoved;

    public HomeworkEditWindow(Homework homework, bool isNew, IReadOnlyList<Lesson> lessons, IReadOnlyList<HomeworkComment>? comments = null)
    {
        InitializeComponent();
        _homework = homework;
        _isNew = isNew;
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        SetComments(comments ?? []);
        var selected = 0;
        var index = 0;
        foreach (var group in HomeworkForm.GroupBySubject(lessons))
        {
            if (group.Any(lesson => lesson.Id == homework.LessonId))
            {
                selected = index;
            }

            LessonBox.Items.Add(new ComboBoxItem
            {
                Content = HomeworkForm.SubjectTitle(group[0].Subject),
                Tag = group
            });
            index++;
        }

        if (LessonBox.Items.Count > 0)
        {
            LessonBox.SelectedIndex = selected;
        }

        TitleBox.Text = homework.Title;
        DescriptionBox.Text = homework.Description;
        DeadlineBox.SelectedDate = homework.Deadline == default ? DateTime.Today : homework.Deadline.Date;
        DoneBox.IsChecked = homework.IsDone;
        UrlBox.Text = homework.Url;
        ExtraUrlBox.Text = homework.ExtraUrl;
        _baseline = ReadFields(commentsChanged: false);
    }

    public Homework Result => _homework;

    private void LessonBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => FillSlots();

    private void FillSlots()
    {
        SlotBox.Items.Clear();
        if (LessonBox.SelectedItem is not ComboBoxItem { Tag: IReadOnlyList<Lesson> group })
        {
            return;
        }

        var slots = HomeworkForm.DistinctSlots(group, _homework.LessonId);
        var selected = 0;
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].Id == _homework.LessonId)
            {
                selected = i;
            }

            SlotBox.Items.Add(new ComboBoxItem
            {
                Content = HomeworkForm.SlotLabel(slots[i]),
                Tag = slots[i].Id
            });
        }

        if (SlotBox.Items.Count > 0)
        {
            SlotBox.SelectedIndex = selected;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        long? lessonId = SlotBox.SelectedItem is ComboBoxItem { Tag: long id } ? id : null;
        if (!HomeworkForm.TryValidate(lessonId, TitleBox.Text, DeadlineBox.SelectedDate, out var error))
        {
            AppDialog.Info(Window.GetWindow(this), "Нельзя сохранить", error ?? HomeworkForm.TitleError);
            return;
        }

        _homework.LessonId = lessonId!.Value;
        _homework.Title = TitleBox.Text.Trim();
        _homework.Description = DescriptionBox.Text.Trim();
        _homework.Deadline = DeadlineBox.SelectedDate!.Value.Date;
        _homework.IsDone = DoneBox.IsChecked == true;
        _homework.Url = UrlBox.Text.Trim();
        _homework.ExtraUrl = ExtraUrlBox.Text.Trim();
        Finish(accepted: true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(_homework.Title)
            ? "Домашка будет удалена."
            : $"«{_homework.Title.Trim()}» будет удалена.";
        if (!AppDialog.Confirm(Window.GetWindow(this), "Удалить домашку?", name))
        {
            return;
        }

        Deleted = true;
        Finish(accepted: true);
    }

    private void OpenSchedule_Click(object sender, RoutedEventArgs e)
    {
        ScheduleDate = DeadlineBox.SelectedDate?.Date ?? _homework.Deadline.Date;
        ScheduleLessonId = SlotBox.SelectedItem is ComboBoxItem { Tag: long id } ? id : _homework.LessonId;
        OpenSchedule = true;
        Finish(accepted: false);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => RequestCancel();

    public void RequestCancel() => Finish(accepted: false);

    public void Save() => Save_Click(this, new RoutedEventArgs());

    public bool HasEdits(bool commentsChanged) =>
        HomeworkForm.HasEdits(_baseline, ReadFields(commentsChanged));

    private HomeworkForm.Fields ReadFields(bool commentsChanged)
    {
        var lessonId = SlotBox.SelectedItem is ComboBoxItem { Tag: long id } ? id : _homework.LessonId;
        return new HomeworkForm.Fields(
            lessonId,
            TitleBox.Text,
            DescriptionBox.Text,
            DeadlineBox.SelectedDate?.Date ?? default,
            DoneBox.IsChecked == true,
            UrlBox.Text,
            ExtraUrlBox.Text,
            CommentBox.Text,
            commentsChanged);
    }

    public void SetComments(IReadOnlyList<HomeworkComment> comments)
    {
        CommentList.Children.Clear();
        var saved = _homework.Id > 0;
        CommentLocked.Visibility = saved ? Visibility.Collapsed : Visibility.Visible;
        CommentComposer.Visibility = saved ? Visibility.Visible : Visibility.Collapsed;
        var muted = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("AppMuted");
        var text = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("AppText");
        foreach (var comment in comments)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var remove = new System.Windows.Controls.Button
            {
                Content = "×",
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = muted,
                VerticalAlignment = VerticalAlignment.Top
            };
            var confirm = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Visibility = Visibility.Collapsed
            };
            var cancel = new System.Windows.Controls.Button
            {
                Content = "Отмена",
                Height = 36,
                MinWidth = 88,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var erase = new System.Windows.Controls.Button
            {
                Content = "Удалить",
                Height = 36,
                MinWidth = 96,
                Padding = new Thickness(12, 0, 12, 0),
                Tag = comment.Id
            };
            cancel.Click += (_, _) => HideDeleteConfirm(remove, confirm);
            erase.Click += RemoveComment_Click;
            remove.Click += (_, _) => ShowDeleteConfirm(remove, confirm);
            confirm.Children.Add(cancel);
            confirm.Children.Add(erase);
            var actions = new Grid { VerticalAlignment = VerticalAlignment.Top };
            actions.Children.Add(remove);
            actions.Children.Add(confirm);
            DockPanel.SetDock(actions, Dock.Right);
            row.Children.Add(actions);
            var lines = new StackPanel();
            lines.Children.Add(new TextBlock
            {
                Text = CommentWhen(comment.CreatedAt),
                Foreground = muted,
                FontSize = 13
            });
            lines.Children.Add(new TextBlock
            {
                Text = comment.Body,
                TextWrapping = TextWrapping.Wrap,
                Foreground = text,
                FontSize = 15,
                Margin = new Thickness(0, 2, 8, 0)
            });
            row.Children.Add(lines);
            CommentList.Children.Add(row);
        }
    }

    private void ShowDeleteConfirm(System.Windows.Controls.Button mark, StackPanel confirm)
    {
        foreach (DockPanel row in CommentList.Children)
        {
            if (row.Children[0] is Grid actions && actions.Children[1] is StackPanel other && other != confirm)
            {
                other.Visibility = Visibility.Collapsed;
                ((System.Windows.Controls.Button)actions.Children[0]).Visibility = Visibility.Visible;
            }
        }

        mark.Visibility = Visibility.Collapsed;
        confirm.Visibility = Visibility.Visible;
    }

    private static void HideDeleteConfirm(System.Windows.Controls.Button mark, StackPanel confirm)
    {
        confirm.Visibility = Visibility.Collapsed;
        mark.Visibility = Visibility.Visible;
    }

    private static string CommentWhen(DateTime created)
    {
        var clock = created.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (created.Date == DateTime.Today)
        {
            return "Сегодня " + clock;
        }

        if (created.Date == DateTime.Today.AddDays(-1))
        {
            return "Вчера " + clock;
        }

        return created.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void CommentBox_GotFocus(object sender, RoutedEventArgs e) => ExpandComment();

    private void CommentBox_LostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!CommentComposer.IsKeyboardFocusWithin && string.IsNullOrWhiteSpace(CommentBox.Text))
            {
                CollapseComment();
            }
        });
    }

    private void CommentBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCommentPlaceholder();

    private void CollapseComment_Click(object sender, RoutedEventArgs e)
    {
        CommentBox.Text = "";
        CollapseComment();
    }

    private void ExpandComment()
    {
        CommentBox.AcceptsReturn = true;
        CommentBox.ClearValue(MinHeightProperty);
        CommentActions.Visibility = Visibility.Visible;
        CommentPlaceholder.Margin = new Thickness(12, 8, 12, 0);
        CommentPlaceholder.VerticalAlignment = VerticalAlignment.Top;
        UpdateCommentPlaceholder();
    }

    private void CollapseComment()
    {
        CommentBox.AcceptsReturn = false;
        CommentBox.ClearValue(MinHeightProperty);
        CommentActions.Visibility = Visibility.Collapsed;
        CommentPlaceholder.Margin = new Thickness(12, 0, 12, 0);
        CommentPlaceholder.VerticalAlignment = VerticalAlignment.Center;
        UpdateCommentPlaceholder();
    }

    private void UpdateCommentPlaceholder()
    {
        CommentPlaceholder.Visibility = string.IsNullOrEmpty(CommentBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void AddComment_Click(object sender, RoutedEventArgs e)
    {
        var text = CommentBox.Text.Trim();
        if (text.Length == 0 || _homework.Id <= 0)
        {
            return;
        }

        CommentBox.Text = "";
        CollapseComment();
        CommentAdded?.Invoke(this, text);
    }

    private void RemoveComment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: long id })
        {
            CommentRemoved?.Invoke(this, id);
        }
    }

    private void Finish(bool accepted)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Accepted = accepted;
        Finished?.Invoke(this, EventArgs.Empty);
    }
}
