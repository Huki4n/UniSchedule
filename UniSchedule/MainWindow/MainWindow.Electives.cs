using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UniSchedule.Services;

namespace UniSchedule;

public partial class MainWindow
{
    private void ReloadElectiveBox()
    {
        var lessons = _db.GetLessons(_settings.SelectedGroup);
        var subjects = new List<string>();
        foreach (var lesson in lessons)
        {
            if (string.IsNullOrWhiteSpace(lesson.ElectiveKey))
            {
                continue;
            }

            if (!subjects.Contains(lesson.Subject, StringComparer.OrdinalIgnoreCase))
            {
                subjects.Add(lesson.Subject);
            }
        }

        ElectiveList.Children.Clear();
        if (subjects.Count == 0)
        {
            ElectiveBox.IsChecked = false;
            ElectiveHost.Visibility = Visibility.Collapsed;
            return;
        }

        var stored = _db.GetElectiveSubjects(_settings.SelectedGroup);
        var picked =
            stored
            ?? ElectiveChoice
                .Visible(lessons, _db.GetElectivePicks())
                .Where(lesson => !string.IsNullOrWhiteSpace(lesson.ElectiveKey))
                .Select(lesson => lesson.Subject)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        var text = (System.Windows.Media.Brush)FindResource("AppText");
        foreach (var subject in subjects)
        {
            var box = new System.Windows.Controls.CheckBox
            {
                Content = subject,
                IsChecked = picked.Any(item =>
                    string.Equals(item, subject, StringComparison.OrdinalIgnoreCase)
                ),
                IsHitTestVisible = false,
                Focusable = false,
                FontSize = 16,
                Foreground = text,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var row = new Border
            {
                Child = box,
                Background = System.Windows.Media.Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 12, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            row.MouseEnter += (_, _) => row.Background = ElectiveHover;
            row.MouseLeave += (_, _) => row.Background = System.Windows.Media.Brushes.Transparent;
            row.MouseLeftButtonDown += ElectiveRow_Click;
            ElectiveList.Children.Add(row);
        }

        ShowElectiveSummary();
        ElectiveHost.Visibility = Visibility.Visible;
    }

    private void ElectiveRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Child: System.Windows.Controls.CheckBox box })
        {
            return;
        }

        e.Handled = true;
        box.IsChecked = box.IsChecked != true;
        var selected = ElectiveList
            .Children.OfType<Border>()
            .Select(row => row.Child)
            .OfType<System.Windows.Controls.CheckBox>()
            .Where(item => item.IsChecked == true)
            .Select(item => item.Content?.ToString() ?? "")
            .Where(subject => subject.Length > 0)
            .ToList();
        _db.SetElectiveSubjects(_settings.SelectedGroup, selected);
        ShowElectiveSummary();
        ReloadScheduleVisible();
    }

    private void ShowElectiveSummary()
    {
        var marked = ElectiveList
            .Children.OfType<Border>()
            .Select(row => row.Child)
            .OfType<System.Windows.Controls.CheckBox>()
            .Where(item => item.IsChecked == true)
            .Select(item => item.Content?.ToString() ?? "")
            .Where(subject => subject.Length > 0)
            .ToList();
        ElectiveSummary.Text = marked.Count == 0 ? "Не выбрано" : string.Join(", ", marked);
        ElectiveSummary.Foreground =
            marked.Count == 0
                ? (System.Windows.Media.Brush)FindResource("AppMuted")
                : (System.Windows.Media.Brush)FindResource("AppText");
    }

    private static System.Windows.Media.SolidColorBrush CreateElectiveHover()
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x2A)
        );
        brush.Freeze();
        return brush;
    }
}
