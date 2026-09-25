using System.Windows;

namespace UniSchedule.Windows;

public partial class AppDialog : Window
{
    private AppDialog()
    {
        InitializeComponent();
    }

    public static bool Confirm(Window? owner, string title, string message, string okText = "Удалить")
    {
        var dialog = Create(owner, title, message, okText, showCancel: true);
        return dialog.ShowDialog() == true;
    }

    public static void Info(Window? owner, string title, string message)
    {
        var dialog = Create(owner, title, message, "Понятно", showCancel: false);
        dialog.ShowDialog();
    }

    private static AppDialog Create(Window? owner, string title, string message, string okText, bool showCancel)
    {
        var dialog = new AppDialog();
        if (owner is { IsLoaded: true })
        {
            dialog.Owner = owner;
        }

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.OkButton.Content = okText;
        dialog.CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        return dialog;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
