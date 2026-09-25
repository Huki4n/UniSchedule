using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace UniSchedule.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon _ownedIcon;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _ownedIcon = IconFactory.CreateTrayIcon();
        _icon = new Forms.NotifyIcon
        {
            Icon = _ownedIcon,
            Text = "Расписание",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Выход", null, (_, _) => ExitRequested?.Invoke());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void ShowBalloon(string title, string text)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _ownedIcon.Dispose();
    }
}
