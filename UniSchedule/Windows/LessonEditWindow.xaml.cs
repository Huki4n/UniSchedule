using System.Windows;
using System.Windows.Controls;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class LessonEditWindow : System.Windows.Controls.UserControl
{
    private readonly Lesson _lesson;
    private readonly bool _isNew;
    private readonly bool _hasRelated;
    private readonly int _homeworkCount;
    private readonly string? _rollbackSubject;

    public bool Deleted { get; private set; }

    public bool Accepted { get; private set; }

    public Homework? OpenHomework { get; private set; }

    public LessonEditScope Scope { get; private set; } = LessonEditScope.OnlyThis;

    public event EventHandler? Finished;

    private bool _finished;

    public LessonEditWindow(Lesson lesson, bool isNew, IReadOnlyList<Homework>? homework = null, bool hasRelated = false, string? rollbackSubject = null)
    {
        InitializeComponent();
        _lesson = lesson;
        _isNew = isNew;
        _hasRelated = hasRelated;
        _homeworkCount = homework?.Count ?? 0;
        _rollbackSubject = string.IsNullOrWhiteSpace(rollbackSubject) ? null : rollbackSubject.Trim();
        FillHomework(homework ?? []);
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        if (_rollbackSubject is not null &&
            !string.Equals(_rollbackSubject, lesson.Subject.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            RollbackSubjectButton.Content = $"Вернуть «{_rollbackSubject}»";
            RollbackSubjectButton.Visibility = Visibility.Visible;
        }

        Fill();
    }

    public Lesson Result => _lesson;

    private void Fill()
    {
        foreach (var day in new[]
                 {
                     DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                     DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
                 })
        {
            DayBox.Items.Add(new ComboBoxItem { Content = AcademicCalendar.DayName(day), Tag = day });
        }

        DayBox.SelectedIndex = Math.Clamp((int)_lesson.DayOfWeek - 1, 0, 5);
        StartBox.Text = _lesson.Start.ToString(@"hh\:mm");
        EndBox.Text = _lesson.End.ToString(@"hh\:mm");
        SubjectBox.Text = _lesson.Subject;
        TeacherBox.Text = _lesson.Teacher;
        RoomBox.Text = _lesson.Room;
        MeetingBox.Text = _lesson.MeetingUrl;
        LmsBox.Text = _lesson.LmsUrl;
        WeekFromBox.Text = _lesson.WeekFrom?.ToString() ?? "";
        WeekToBox.Text = _lesson.WeekTo?.ToString() ?? "";
        NotesBox.Text = _lesson.Notes;

        TypeBox.Items.Add(new ComboBoxItem { Content = "Не указан", Tag = "" });
        TypeBox.Items.Add(new ComboBoxItem { Content = "Лекция", Tag = LessonCodes.Lecture });
        TypeBox.Items.Add(new ComboBoxItem { Content = "Практика", Tag = LessonCodes.Practice });
        TypeBox.Items.Add(new ComboBoxItem { Content = "Лабораторная", Tag = LessonCodes.Lab });
        TypeBox.Items.Add(new ComboBoxItem { Content = "Зачет", Tag = LessonCodes.Credit });
        TypeBox.Items.Add(new ComboBoxItem { Content = "Экзамен", Tag = LessonCodes.Exam });
        TypeBox.SelectedIndex = _lesson.LessonType switch
        {
            LessonCodes.Lecture => 1,
            LessonCodes.Practice => 2,
            LessonCodes.Lab => 3,
            LessonCodes.Credit => 4,
            LessonCodes.Exam => 5,
            _ => 0
        };

        ParityBox.Items.Add(new ComboBoxItem { Content = "Все недели", Tag = WeekParity.All });
        ParityBox.Items.Add(new ComboBoxItem { Content = "Нечётные", Tag = WeekParity.Odd });
        ParityBox.Items.Add(new ComboBoxItem { Content = "Чётные", Tag = WeekParity.Even });
        ParityBox.SelectedIndex = (int)_lesson.Parity;
    }

    private void FillHomework(IReadOnlyList<Homework> homework)
    {
        if (homework.Count == 0)
        {
            return;
        }

        HomeworkPanel.Visibility = Visibility.Visible;
        foreach (var item in homework.OrderBy(item => item.Deadline).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            var button = new System.Windows.Controls.Button
            {
                Content = $"{item.Title.Trim()} · {item.Deadline:dd.MM.yyyy}",
                Tag = item,
                Height = 36,
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left
            };
            button.Click += OpenHomework_Click;
            HomeworkList.Items.Add(button);
        }
    }

    private void OpenHomework_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: Homework homework })
        {
            OpenHomework = homework;
            Finish(accepted: false);
        }
    }

    public void RequestCancel() => Finish(accepted: false);

    private void RollbackSubject_Click(object sender, RoutedEventArgs e)
    {
        if (_rollbackSubject is not null)
        {
            SubjectBox.Text = _rollbackSubject;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!LessonForm.TryValidate(SubjectBox.Text, StartBox.Text, EndBox.Text, out var start, out var end, out var error))
        {
            AppDialog.Info(Window.GetWindow(this), "Нельзя сохранить", error ?? LessonForm.SubjectError);
            return;
        }

        _lesson.DayOfWeek = DayBox.SelectedItem is ComboBoxItem { Tag: DayOfWeek day }
            ? day
            : DayOfWeek.Monday;
        _lesson.Start = start;
        _lesson.End = end;
        _lesson.Subject = SubjectBox.Text.Trim();
        _lesson.LessonType = TypeBox.SelectedItem is ComboBoxItem { Tag: string type } ? type : "";
        _lesson.Teacher = TeacherBox.Text.Trim();
        _lesson.Room = RoomBox.Text.Trim();
        _lesson.MeetingUrl = MeetingBox.Text.Trim();
        _lesson.LmsUrl = LmsBox.Text.Trim();
        _lesson.Parity = ParityBox.SelectedItem is ComboBoxItem { Tag: WeekParity parity }
            ? parity
            : WeekParity.All;
        _lesson.WeekFrom = int.TryParse(WeekFromBox.Text, out var from) ? from : null;
        _lesson.WeekTo = int.TryParse(WeekToBox.Text, out var to) ? to : null;
        _lesson.Notes = NotesBox.Text.Trim();
        if (_isNew)
        {
            _lesson.Source = LessonCodes.Manual;
        }

        if (_hasRelated)
        {
            var scope = LessonScopeDialog.Ask(Window.GetWindow(this)!);
            if (scope is null)
            {
                return;
            }

            Scope = scope.Value;
        }

        Finish(accepted: true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm(Window.GetWindow(this), "Удалить пару?", LessonForm.DeleteConfirmText(_lesson.Subject, _homeworkCount)))
        {
            return;
        }

        Deleted = true;
        Finish(accepted: true);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => RequestCancel();

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
