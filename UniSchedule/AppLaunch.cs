using System.Security.Cryptography;
using System.Text;

namespace UniSchedule;

public static class AppLaunch
{
    public static string? ReadDataPath(string[] args)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, "--data", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            return null;
        }

        return Path.GetFullPath(args[index + 1]);
    }

    public static string MutexName(string? dataPath) =>
        dataPath is null
            ? @"Local\UniSchedule.SingleInstance"
            : @"Local\UniSchedule." + Token(dataPath);

    public static string ShowEventName(string? dataPath) =>
        dataPath is null
            ? @"Local\UniSchedule.Show"
            : @"Local\UniSchedule.Show." + Token(dataPath);

    private static string Token(string dataPath) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataPath)))[..16];
}
