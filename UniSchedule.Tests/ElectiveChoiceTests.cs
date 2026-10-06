using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class ElectiveChoiceTests
{
    [Fact]
    public void RenameSelected_RewritesCheckedName_WhenNoOtherElectiveKeepsIt()
    {
        var linux = Lesson("Основы Linux", "11-321|5|08:30");
        linux.Subject = "Linux";
        var updated = ElectiveChoice.RenameSelected(
            ["Основы Linux", "Кроссплатформенная разработка"],
            [("Основы Linux", "Linux")],
            [linux]
        );

        Assert.Equal(["Linux", "Кроссплатформенная разработка"], updated);
    }

    [Fact]
    public void RenameSelected_KeepsOld_AndAddsNew_WhenAnotherElectiveStillUsesOldName()
    {
        var renamed = Lesson("Linux", "11-321|5|08:30");
        var other = Lesson("Основы Linux", "11-321|4|12:00");
        var updated = ElectiveChoice.RenameSelected(
            ["Основы Linux"],
            [("Основы Linux", "Linux")],
            [renamed, other]
        );

        Assert.Equal(["Основы Linux", "Linux"], updated);
    }

    [Fact]
    public void RenameSelected_ReturnsSame_WhenSubjectsUnsetOrNameNotChecked()
    {
        Assert.Null(ElectiveChoice.RenameSelected(null, [("A", "B")], []));

        var selected = new List<string> { "Кроссплатформенная разработка" };
        Assert.Same(
            selected,
            ElectiveChoice.RenameSelected(selected, [("Основы Linux", "Linux")], [])
        );
    }

    private static Lesson Lesson(string subject, string electiveKey) =>
        new()
        {
            GroupCode = "11-321",
            Subject = subject,
            ElectiveKey = electiveKey,
            DayOfWeek = DayOfWeek.Friday,
            Start = new TimeSpan(8, 30, 0),
            End = new TimeSpan(10, 0, 0),
        };
}
