using System.Globalization;
using System.Windows.Data;
using UniSchedule.Models;

namespace UniSchedule.Converters;

public sealed class HomeworkReminderLabelConverter : IValueConverter
{
    public static string Format(int minutes) =>
        "за " + AppSettings.HomeworkReminderSpan(minutes);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int minutes ? Format(minutes) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
