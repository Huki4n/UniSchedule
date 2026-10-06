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
    private bool _isToday;

    public bool IsToday
    {
        get => _isToday;
        set
        {
            if (_isToday == value)
            {
                return;
            }

            _isToday = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsToday)));
        }
    }
    public ObservableCollection<LessonCardVm> Lessons { get; } = [];
    public bool IsEmpty => Lessons.Count == 0;

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

public sealed class BoardRowVm
{
    public IReadOnlyList<BoardCellVm> Cells { get; init; } = [];
}

public sealed class BoardCellVm
{
    public required DayColumnVm Day { get; init; }
    public bool IsHeader { get; init; }
    public bool ShowTitle { get; init; }
    public IReadOnlyList<LessonCardVm> Cards { get; init; } = [];
}

public sealed class LessonCardVm : INotifyPropertyChanged
{
    private bool _isHighlighted;

    public required Lesson Lesson { get; init; }
    public string TimeText { get; init; } = "";
    public string StartText { get; init; } = "";
    public string EndText { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Meta { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool ShowOnline { get; init; }
    public string Notes { get; init; } = "";
    public string Place { get; init; } = "";
    public string Badge { get; init; } = "";
    public IReadOnlyList<CardLinkVm> Links { get; init; } = [];
    public bool IsOnline { get; init; }
    public bool IsDimmed { get; init; }
    public string Accent { get; init; } = "#2563EB";
    public IReadOnlyList<HomeworkLinkVm> HomeworkLinks { get; init; } = [];
    public bool IsOverlap { get; set; }

    private bool _isCurrent;

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value)
            {
                return;
            }

            _isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }
    public bool HasElectiveChoice { get; init; }
    public IReadOnlyList<ElectiveChoiceVm> ElectiveChoices { get; init; } = [];

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

public sealed class ElectiveChoiceVm
{
    public long LessonId { get; init; }
    public string Subject { get; init; } = "";
    public bool IsCurrent { get; init; }
}

public sealed class HomeworkLinkVm
{
    public required Homework Homework { get; init; }
    public string Label { get; init; } = "";
}

public sealed class CardLinkVm
{
    public string Label { get; init; } = "";
    public string Url { get; init; } = "";
}
