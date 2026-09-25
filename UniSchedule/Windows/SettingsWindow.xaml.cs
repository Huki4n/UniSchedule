using System.Collections.ObjectModel;
using System.Windows;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }
    public bool DataCleared { get; private set; }
    public Action? TestNotification { get; init; }
    public Action? ClearStoredData { get; init; }

    private readonly ObservableCollection<int> _reminders = [];

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

        var index = 0;
        while (index < _reminders.Count && _reminders[index] > minutes)
        {
            index++;
        }

        _reminders.Insert(index, minutes);
        ReminderList.SelectedItem = minutes;
        ReminderBox.Clear();
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
