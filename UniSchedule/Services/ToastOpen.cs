namespace UniSchedule.Services;

public static class ToastOpen
{
    public const string Lesson = "lesson";
    public const string Homework = "homework";

    public static string Argument(string kind, long id) => $"{kind}:{id}";

    public static bool TryParse(string? argument, out string kind, out long id)
    {
        kind = "";
        id = 0;
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        foreach (var key in new[] { Lesson, Homework })
        {
            var token = key + ":";
            var index = argument.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var rest = argument[(index + token.Length)..];
            var end = 0;
            while (end < rest.Length && char.IsDigit(rest[end]))
            {
                end++;
            }

            if (end > 0 && long.TryParse(rest[..end], out id) && id > 0)
            {
                kind = key;
                return true;
            }
        }

        return false;
    }
}
