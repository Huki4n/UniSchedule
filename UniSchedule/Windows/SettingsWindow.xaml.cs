using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using Button = System.Windows.Controls.Button;
using UniSchedule.Converters;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }
    public bool DataCleared { get; private set; }
    public Action? TestNotification { get; init; }
    public Action? ClearStoredData { get; init; }

    private static readonly (int Minutes, string AutomationId)[] HomeworkPresetButtons =
    [
        (AppSettings.HomeworkPresetWeek, "HomeworkPreset7d"),
        (AppSettings.HomeworkPresetFiveDays, "HomeworkPreset5d"),
        (AppSettings.HomeworkPresetThreeDays, "HomeworkPreset3d"),
        (AppSettings.HomeworkPresetDay, "HomeworkPreset1d"),
        (AppSettings.HomeworkPresetTwelveHours, "HomeworkPreset12h"),
        (AppSettings.HomeworkPresetFourHours, "HomeworkPreset4h")
    ];

    private readonly ObservableCollection<int> _reminders = [];
    private readonly ObservableCollection<int> _homeworkReminders = [];

    public SettingsWindow(AppSettings settings, IEnumerable<string> groups)
    {
        InitializeComponent();
        Settings = settings;
        foreach (var group in groups)
        {
            GroupBox.Items.Add(group);
        }

        GroupBox.Text = settings.SelectedGroup;
        SemesterPicker.SelectedDate = settings.SemesterStart;
        foreach (var minutes in settings.ReminderOffsets)
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
        foreach (var minutes in settings.HomeworkReminderMinutes)
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
                FontSize = 15
            };
            AutomationProperties.SetAutomationId(button, automationId);
            button.Click += AddHomeworkPreset_Click;
            HomeworkReminderPresets.Children.Add(button);
        }

        NotifyBox.IsChecked = settings.NotificationsEnabled;
        TrayBox.IsChecked = settings.MinimizeToTray;
        AutostartBox.IsChecked = settings.Autostart;
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

    private void HomeworkReminderBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
            ? (System.Windows.Media.Brush)FindResource("AppMuted")
            : (System.Windows.Media.Brush)FindResource("AppText");
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
            ? (System.Windows.Media.Brush)FindResource("AppMuted")
            : (System.Windows.Media.Brush)FindResource("AppText");
        HomeworkReminderRemoveButton.IsEnabled = !empty;
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

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        var code = GroupBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        if (!GroupBox.Items.Cast<object>().Any(i => string.Equals(i.ToString(), code, StringComparison.OrdinalIgnoreCase)))
        {
            GroupBox.Items.Add(code);
        }

        GroupBox.Text = code;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings.SelectedGroup = SettingsForm.NormalizeGroup(GroupBox.Text);
        Settings.SemesterStart = SettingsForm.NormalizeSemesterStart(SemesterPicker.SelectedDate);
        Settings.ReminderMinutes = _reminders.ToArray();
        Settings.HomeworkReminderMinutes = _homeworkReminders.ToArray();
        Settings.NotificationsEnabled = NotifyBox.IsChecked == true;
        Settings.MinimizeToTray = TrayBox.IsChecked == true;
        Settings.Autostart = AutostartBox.IsChecked == true;
        DialogResult = true;
    }

    private void TestNotify_Click(object sender, RoutedEventArgs e) => TestNotification?.Invoke();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm(this, "Очистить данные?", "Пары, домашки и комментарии будут удалены.", "Очистить"))
        {
            return;
        }

        ClearStoredData?.Invoke();
        DataCleared = true;
        DialogResult = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
