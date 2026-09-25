using System.Windows;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }
    public Action? TestNotification { get; init; }

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
        FirstReminderBox.Text = settings.FirstReminderMinutes.ToString();
        SecondReminderBox.Text = settings.SecondReminderMinutes.ToString();
        NotifyBox.IsChecked = settings.NotificationsEnabled;
        TrayBox.IsChecked = settings.MinimizeToTray;
        AutostartBox.IsChecked = settings.Autostart;
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
        if (!SettingsForm.TryParseReminders(FirstReminderBox.Text, SecondReminderBox.Text, out var first, out var second))
        {
            AppDialog.Info(this, "Проверьте поля", SettingsForm.ReminderError);
            return;
        }

        Settings.SelectedGroup = SettingsForm.NormalizeGroup(GroupBox.Text);
        Settings.SemesterStart = SettingsForm.NormalizeSemesterStart(SemesterPicker.SelectedDate);
        Settings.FirstReminderMinutes = first;
        Settings.SecondReminderMinutes = second;
        Settings.NotificationsEnabled = NotifyBox.IsChecked == true;
        Settings.MinimizeToTray = TrayBox.IsChecked == true;
        Settings.Autostart = AutostartBox.IsChecked == true;
        DialogResult = true;
    }

    private void TestNotify_Click(object sender, RoutedEventArgs e) => TestNotification?.Invoke();

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
