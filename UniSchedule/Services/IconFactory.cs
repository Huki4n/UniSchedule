using Drawing = System.Drawing;

namespace UniSchedule.Services;

public static class IconFactory
{
    public static Drawing.Icon CreateTrayIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using var g = Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Drawing.Color.FromArgb(37, 99, 235));
        using var white = new Drawing.SolidBrush(Drawing.Color.White);
        using var font = new Drawing.Font("Segoe UI Semibold", 11, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        g.DrawString("Р", font, white, 8, 6);
        var handle = bmp.GetHicon();
        using var temp = Drawing.Icon.FromHandle(handle);
        return (Drawing.Icon)temp.Clone();
    }
}
