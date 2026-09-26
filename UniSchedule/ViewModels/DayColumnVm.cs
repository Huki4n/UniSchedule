using System.Collections.ObjectModel;
using System.ComponentModel;
using UniSchedule.Models;

namespace UniSchedule.ViewModels;

public sealed class DayColumnVm : INotifyPropertyChanged
{
    private bool _isHighlighted;

    public DayOfWeek Day { get; init; }
    public DateTime Date { get; init; }
    public string Title { get; init; } = "";
    public bool IsToday { get; init; }
    public ObservableCollection<LessonCardVm> Lessons { get; } = [];

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value)
            {
                return;
            }

            _isHighlighted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHighlighted)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class LessonCardVm : INotifyPropertyChanged
{
    private bool _isHighlighted;

    public required Lesson Lesson { get; init; }
    public string TimeText { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Meta { get; init; } = "";
    public string Place { get; init; } = "";
    public string Badge { get; init; } = "";
    public IReadOnlyList<string> Links { get; init; } = [];
    public bool IsOnline { get; init; }
    public bool IsDimmed { get; init; }
    public string Accent { get; init; } = "#2563EB";
    public IReadOnlyList<HomeworkLinkVm> HomeworkLinks { get; init; } = [];

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value)
            {
                return;
            }

            _isHighlighted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHighlighted)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class HomeworkLinkVm
{
    public required Homework Homework { get; init; }
    public string Label { get; init; } = "";
}
