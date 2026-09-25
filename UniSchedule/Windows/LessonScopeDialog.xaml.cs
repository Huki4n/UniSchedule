using System.Windows;
using UniSchedule.Services;

namespace UniSchedule.Windows;

public partial class LessonScopeDialog : Window
{
    private LessonScopeDialog()
    {
        InitializeComponent();
    }

    public static LessonEditScope? Ask(Window owner)
    {
        var dialog = new LessonScopeDialog();
        if (owner.IsLoaded)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true ? dialog.Scope : null;
    }

    public LessonEditScope Scope { get; private set; } = LessonEditScope.OnlyThis;

    private void OnlyThis_Click(object sender, RoutedEventArgs e) => Close(LessonEditScope.OnlyThis);

    private void Following_Click(object sender, RoutedEventArgs e) => Close(LessonEditScope.ThisAndFollowing);

    private void All_Click(object sender, RoutedEventArgs e) => Close(LessonEditScope.All);

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close(LessonEditScope scope)
    {
        Scope = scope;
        DialogResult = true;
    }
}
