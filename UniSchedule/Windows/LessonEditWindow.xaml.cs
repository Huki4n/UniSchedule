using System.Windows;
using System.Windows.Controls;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class LessonEditWindow : Window
{
    private readonly Lesson _lesson;
    private readonly bool _isNew;

    public bool Deleted { get; private set; }

    public LessonEditWindow(Lesson lesson, bool isNew)
    {
        InitializeComponent();
        _lesson = lesson;
        _isNew = isNew;
        Title = isNew ? "Новая пара" : "Редактирование пары";
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
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
        TypeBox.SelectedIndex = _lesson.LessonType switch
        {
            LessonCodes.Lecture => 1,
            LessonCodes.Practice => 2,
            LessonCodes.Lab => 3,
            _ => 0
        };

        ParityBox.Items.Add(new ComboBoxItem { Content = "Все недели", Tag = WeekParity.All });
        ParityBox.Items.Add(new ComboBoxItem { Content = "Нечётные", Tag = WeekParity.Odd });
        ParityBox.Items.Add(new ComboBoxItem { Content = "Чётные", Tag = WeekParity.Even });
        ParityBox.SelectedIndex = (int)_lesson.Parity;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!LessonForm.TryValidate(SubjectBox.Text, StartBox.Text, EndBox.Text, out var start, out var end, out var error))
        {
            AppDialog.Info(this, "Нельзя сохранить", error ?? LessonForm.SubjectError);
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

        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm(this, "Удалить пару?",
                string.IsNullOrWhiteSpace(_lesson.Subject) ? "Пара будет удалена." : $"«{_lesson.Subject}» будет удалена."))
        {
            return;
        }

        Deleted = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
