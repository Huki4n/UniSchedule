using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using UniSchedule.Converters;
using UniSchedule.Models;
using UniSchedule.Services;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;

namespace UniSchedule.Windows;

public partial class NotificationSettingsWindow : Window
{
    public int[] Reminders => _reminders.ToArray();
    public int[] HomeworkReminders => _homeworkReminders.ToArray();
    public bool NotificationsEnabled => NotifyBox.IsChecked == true;
    public Action? TestNotification { get; init; }

    private static readonly (int Minutes, string AutomationId)[] ReminderPresetButtons =
    [
        (60, "ReminderPreset60"),
        (30, "ReminderPreset30"),
        (15, "ReminderPreset15"),
        (5, "ReminderPreset5"),
    ];

    private static readonly (int Minutes, string AutomationId)[] HomeworkPresetButtons =
    [
        (AppSettings.HomeworkPresetWeek, "HomeworkPreset7d"),
        (AppSettings.HomeworkPresetFiveDays, "HomeworkPreset5d"),
        (AppSettings.HomeworkPresetThreeDays, "HomeworkPreset3d"),
        (AppSettings.HomeworkPresetDay, "HomeworkPreset1d"),
        (AppSettings.HomeworkPresetTwelveHours, "HomeworkPreset12h"),
        (AppSettings.HomeworkPresetFourHours, "HomeworkPreset4h"),
    ];

    private readonly ObservableCollection<int> _reminders = [];
    private readonly ObservableCollection<int> _homeworkReminders = [];

    public NotificationSettingsWindow(
        IReadOnlyList<int> reminders,
        IReadOnlyList<int> homeworkReminders,
        bool notificationsEnabled
    )
    {
        InitializeComponent();
        foreach (var minutes in reminders)
        {
            _reminders.Add(minutes);
        }

        _reminders.CollectionChanged += (_, _) => UpdateReminderSummary();
        ReminderList.ItemsSource = _reminders;
        if (_reminders.Count > 0)
        {
            ReminderList.SelectedIndex = 0;
        }

        UpdateReminderSummary();
        foreach (var (minutes, automationId) in ReminderPresetButtons)
        {
            var button = new Button
            {
                Content = $"за {minutes} мин.",
                Tag = minutes,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 15,
            };
            AutomationProperties.SetAutomationId(button, automationId);
            button.Click += AddReminderPreset_Click;
            ReminderPresets.Children.Add(button);
        }

        foreach (var minutes in homeworkReminders)
        {
            _homeworkReminders.Add(minutes);
        }

        _homeworkReminders.CollectionChanged += (_, _) => UpdateHomeworkReminderSummary();
        HomeworkReminderList.ItemsSource = _homeworkReminders;
        if (_homeworkReminders.Count > 0)
        {
            HomeworkReminderList.SelectedIndex = 0;
        }

        UpdateHomeworkReminderSummary();
        foreach (var (minutes, automationId) in HomeworkPresetButtons)
        {
            var button = new Button
            {
                Content = HomeworkReminderLabelConverter.Format(minutes),
                Tag = minutes,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 15,
            };
            AutomationProperties.SetAutomationId(button, automationId);
            button.Click += AddHomeworkPreset_Click;
            HomeworkReminderPresets.Children.Add(button);
        }

        NotifyBox.IsChecked = notificationsEnabled;
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReminderBox.Text))
        {
            return;
        }

        if (!SettingsForm.TryParseReminder(ReminderBox.Text, out var minutes))
        {
            AppDialog.Info(this, "Проверьте поля", SettingsForm.ReminderError);
            return;
        }

        if (_reminders.Contains(minutes))
        {
            return;
        }

        InsertRanked(_reminders, minutes);
        ReminderList.SelectedItem = minutes;
        ReminderBox.Clear();
    }

    private void AddReminderPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int minutes } || _reminders.Contains(minutes))
        {
            return;
        }

        InsertRanked(_reminders, minutes);
        ReminderList.SelectedItem = minutes;
    }

    private void AddHomeworkReminder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(HomeworkReminderBox.Text))
        {
            return;
        }

        if (!SettingsForm.TryParseHomeworkReminder(HomeworkReminderBox.Text, out var days))
        {
            AppDialog.Info(this, "Проверьте поля", SettingsForm.HomeworkReminderError);
            return;
        }

        if (_homeworkReminders.Contains(days))
        {
            return;
        }

        InsertRanked(_homeworkReminders, days);
        HomeworkReminderList.SelectedItem = days;
        HomeworkReminderBox.Clear();
    }

    private void AddHomeworkPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int minutes } || _homeworkReminders.Contains(minutes))
        {
            return;
        }

        InsertRanked(_homeworkReminders, minutes);
        HomeworkReminderList.SelectedItem = minutes;
    }

    private void HomeworkReminderBox_OnPreviewKeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e
    )
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        AddHomeworkReminder_Click(sender, e);
        e.Handled = true;
    }

    private void ReminderBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        AddReminder_Click(sender, e);
        e.Handled = true;
    }

    private void RemoveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (ReminderList.SelectedItem is not int minutes)
        {
            return;
        }

        var index = _reminders.IndexOf(minutes);
        _reminders.Remove(minutes);
        if (_reminders.Count > 0)
        {
            ReminderList.SelectedIndex = Math.Min(index, _reminders.Count - 1);
        }
    }

    private void UpdateReminderSummary()
    {
        var empty = _reminders.Count == 0;
        ReminderSummary.Text = empty ? "Напоминаний нет" : string.Join(", ", _reminders);
        ReminderSummary.Foreground = empty
            ? (Brush)FindResource("AppMuted")
            : (Brush)FindResource("AppText");
        ReminderRemoveButton.IsEnabled = !empty;
    }

    private void RemoveHomeworkReminder_Click(object sender, RoutedEventArgs e)
    {
        if (HomeworkReminderList.SelectedItem is not int days)
        {
            return;
        }

        var index = _homeworkReminders.IndexOf(days);
        _homeworkReminders.Remove(days);
        if (_homeworkReminders.Count > 0)
        {
            HomeworkReminderList.SelectedIndex = Math.Min(index, _homeworkReminders.Count - 1);
        }
    }

    private void UpdateHomeworkReminderSummary()
    {
        var empty = _homeworkReminders.Count == 0;
        HomeworkReminderSummary.Text = empty
            ? "Напоминаний нет"
            : string.Join(", ", _homeworkReminders.Select(HomeworkReminderLabelConverter.Format));
        HomeworkReminderSummary.Foreground = empty
            ? (Brush)FindResource("AppMuted")
            : (Brush)FindResource("AppText");
        HomeworkReminderRemoveButton.IsEnabled = !empty;
    }

    private void Title_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private static void InsertRanked(ObservableCollection<int> items, int value)
    {
        var rank = ReminderRank(value);
        var index = 0;
        while (index < items.Count && ReminderRank(items[index]) > rank)
        {
            index++;
        }

        items.Insert(index, value);
    }

    private static int ReminderRank(int value) =>
        value == AppSettings.HomeworkMonthOffset ? int.MaxValue : value;

    private void TestNotify_Click(object sender, RoutedEventArgs e) => TestNotification?.Invoke();

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
