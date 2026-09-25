namespace UniSchedule.Services;

internal static class ToastLog
{
    public static string DefaultPath =>
        Path.Combine(Path.GetTempPath(), "unischedule-toast.txt");

    public static void Append(Exception exception, string path)
    {
        var entry =
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
        File.AppendAllText(path, entry);
    }
}
