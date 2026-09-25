using System.Collections.ObjectModel;
using UniSchedule.Models;

namespace UniSchedule.ViewModels;

public sealed class DayColumnVm
{
    public DayOfWeek Day { get; init; }
    public string Title { get; init; } = "";
    public bool IsToday { get; init; }
    public ObservableCollection<LessonCardVm> Lessons { get; } = [];
}

public sealed class LessonCardVm
{
    public required Lesson Lesson { get; init; }
    public string TimeText { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Meta { get; init; } = "";
    public string Place { get; init; } = "";
    public string Badge { get; init; } = "";
    public bool IsOnline { get; init; }
    public bool IsDimmed { get; init; }
    public string Accent { get; init; } = "#2563EB";
}
