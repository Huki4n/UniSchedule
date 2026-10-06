using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using UniSchedule.Windows;

namespace UniSchedule;

public partial class MainWindow
{
    private void EditorResize_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement grip)
        {
            return;
        }

        _editorResizing = true;
        _editorResizeX = e.GetPosition(this).X;
        _editorResizeWidth = EditorPanel.ActualWidth;
        grip.CaptureMouse();
        e.Handled = true;
    }

    private void EditorResize_OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_editorResizing || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var grown = _editorResizeX - e.GetPosition(this).X;
        var max = Math.Min(EditorMaxWidth, Math.Max(EditorMinWidth, ActualWidth * 0.6));
        _editorWidth = Math.Clamp(_editorResizeWidth + grown, EditorMinWidth, max);
        EditorPanel.Width = _editorWidth;
    }

    private void EditorResize_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _editorResizing = false;
        if (sender is UIElement grip)
        {
            grip.ReleaseMouseCapture();
        }
    }

    private void ShowEditor(UIElement editor, string title)
    {
        _editorEpoch++;
        EditorTitle.Text = title;
        _editorClosing = false;
        var opening = EditorPanel.Visibility != Visibility.Visible;
        EditorHost.Content = editor;
        EditorPanel.Visibility = Visibility.Visible;
        if (opening)
        {
            EditorShift.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(_editorWidth, 0, TimeSpan.FromMilliseconds(180))
            );
        }
        else
        {
            EditorShift.BeginAnimation(TranslateTransform.XProperty, null);
            EditorShift.X = 0;
        }
    }

    private void HideEditor()
    {
        _editorClosing = true;
        var epoch = _editorEpoch;
        var animation = new DoubleAnimation(0, _editorWidth, TimeSpan.FromMilliseconds(160));
        animation.Completed += (_, _) =>
        {
            if (epoch != _editorEpoch)
            {
                return;
            }

            EditorPanel.Visibility = Visibility.Collapsed;
            EditorHost.Content = null;
            _editorClosing = false;
        };
        EditorShift.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void CloseEditor()
    {
        if (EditorPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        _editorEpoch++;
        _editorClosing = false;
        EditorShift.BeginAnimation(TranslateTransform.XProperty, null);
        EditorShift.X = _editorWidth;
        EditorPanel.Visibility = Visibility.Collapsed;
        EditorHost.Content = null;
    }

    private void CloseEditorIfStale(long? lessonId, long? homeworkId)
    {
        if (
            EditorHost.Content is LessonEditWindow lesson
            && lessonId is long openLesson
            && lesson.Result.Id == openLesson
        )
        {
            CloseEditor();
            return;
        }

        if (EditorHost.Content is not HomeworkEditWindow homework)
        {
            return;
        }

        if (homeworkId is long openHomework && homework.Result.Id == openHomework)
        {
            CloseEditor();
            return;
        }

        if (lessonId is long owner && homework.Result.LessonId == owner)
        {
            CloseEditor();
        }
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (
            EditorPanel.Visibility != Visibility.Visible
            || _editorClosing
            || e.OriginalSource is not DependencyObject source
        )
        {
            return;
        }

        if (IsInsideEditor(source) || _savePrompt)
        {
            if (_savePrompt)
            {
                e.Handled = true;
            }

            return;
        }

        if (!EditorHasEdits())
        {
            CloseEditor_Click(this, e);
            return;
        }

        e.Handled = true;
        _savePrompt = true;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                PromptSaveEditor();
            }
            finally
            {
                _savePrompt = false;
            }
        });
    }

    private bool EditorHasEdits() =>
        EditorHost.Content switch
        {
            LessonEditWindow lesson => lesson.HasEdits(),
            HomeworkEditWindow homework => homework.HasEdits(_commentDraft?.HasEdits == true),
            _ => false,
        };

    private void PromptSaveEditor()
    {
        if (EditorPanel.Visibility != Visibility.Visible || _editorClosing || !EditorHasEdits())
        {
            return;
        }

        switch (
            AppDialog.PromptSave(
                this,
                "Сохранить изменения?",
                "В панели есть несохранённые правки."
            )
        )
        {
            case SavePrompt.Save:
                switch (EditorHost.Content)
                {
                    case LessonEditWindow lesson:
                        lesson.Save();
                        break;
                    case HomeworkEditWindow homework:
                        homework.Save();
                        break;
                }

                break;
            case SavePrompt.Discard:
                CloseEditor_Click(this, new RoutedEventArgs());
                break;
        }
    }

    private bool IsInsideEditor(DependencyObject source)
    {
        var popupContent = EditorPopupContent();
        for (DependencyObject? node = source; node is not null; node = ParentOf(node))
        {
            if (node == EditorPanel || popupContent.Contains(node))
            {
                return true;
            }
        }

        return false;
    }

    private HashSet<DependencyObject> EditorPopupContent()
    {
        var content = new HashSet<DependencyObject>();
        AddPopupContent(EditorPanel, content);
        return content;
    }

    private static void AddPopupContent(DependencyObject node, HashSet<DependencyObject> content)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Popup { Child: { } popupChild })
            {
                content.Add(popupChild);
            }

            AddPopupContent(child, content);
        }
    }

    private static DependencyObject? ParentOf(DependencyObject node)
    {
        if (node is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(node);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(node);
    }

    private void CloseEditor_Click(object sender, RoutedEventArgs e)
    {
        switch (EditorHost.Content)
        {
            case LessonEditWindow lesson:
                lesson.RequestCancel();
                break;
            case HomeworkEditWindow homework:
                homework.RequestCancel();
                break;
        }
    }
}
