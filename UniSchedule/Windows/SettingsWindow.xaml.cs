using System.Windows;
using System.Windows.Input;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }
    public bool DataCleared { get; private set; }
    public bool DatabaseRestored { get; private set; }
    public Action? TestNotification { get; init; }
    public Action? ClearStoredData { get; init; }
    public Action<string>? CopyDatabase { get; init; }
    public Action<string>? RestoreDatabase { get; init; }

    private int[] _reminders = [];
    private int[] _homeworkReminders = [];
    private bool _notificationsEnabled;

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
        _reminders = settings.ReminderOffsets.ToArray();
        _homeworkReminders = settings.HomeworkReminderMinutes.ToArray();
        _notificationsEnabled = settings.NotificationsEnabled;
        TrayBox.IsChecked = settings.MinimizeToTray;
        AutostartBox.IsChecked = settings.Autostart;
    }

    private void OpenNotifications_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NotificationSettingsWindow(
            _reminders,
            _homeworkReminders,
            _notificationsEnabled
        )
        {
            Owner = this,
            TestNotification = TestNotification,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _reminders = dialog.Reminders;
        _homeworkReminders = dialog.HomeworkReminders;
        _notificationsEnabled = dialog.NotificationsEnabled;
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        var code = GroupBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        if (
            !GroupBox
                .Items.Cast<object>()
                .Any(i => string.Equals(i.ToString(), code, StringComparison.OrdinalIgnoreCase))
        )
        {
            GroupBox.Items.Add(code);
        }

        GroupBox.Text = code;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings.SelectedGroup = SettingsForm.NormalizeGroup(GroupBox.Text);
        Settings.SemesterStart = SettingsForm.NormalizeSemesterStart(SemesterPicker.SelectedDate);
        Settings.ReminderMinutes = _reminders;
        Settings.HomeworkReminderMinutes = _homeworkReminders;
        Settings.NotificationsEnabled = _notificationsEnabled;
        Settings.MinimizeToTray = TrayBox.IsChecked == true;
        Settings.Autostart = AutostartBox.IsChecked == true;
        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (
            !AppDialog.Confirm(
                this,
                "Очистить данные?",
                "Пары, домашки и комментарии будут удалены.",
                "Очистить"
            )
        )
        {
            return;
        }

        ClearStoredData?.Invoke();
        DataCleared = true;
        DialogResult = false;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "База SQLite (*.db)|*.db",
            FileName = "schedule.db",
            AddExtension = true,
            DefaultExt = ".db",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != true || CopyDatabase is null)
        {
            return;
        }

        try
        {
            CopyDatabase(dialog.FileName);
            AppDialog.Info(this, "Копия сохранена", dialog.FileName);
        }
        catch (Exception)
        {
            AppDialog.Info(this, "Не удалось сохранить копию.", "Файл не записан.");
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "База SQLite (*.db)|*.db",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true || RestoreDatabase is null)
        {
            return;
        }

        if (
            !AppDialog.Confirm(
                this,
                "Открыть копию?",
                "Текущая база будет заменена этим файлом.",
                "Заменить"
            )
        )
        {
            return;
        }

        try
        {
            RestoreDatabase(dialog.FileName);
            DatabaseRestored = true;
            DialogResult = false;
        }
        catch (Exception)
        {
            AppDialog.Info(this, "Не удалось открыть копию.", "Файл не прочитан.");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Title_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
