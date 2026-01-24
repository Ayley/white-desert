using System;
using Avalonia;
using AvaloniaEdit;

namespace White_Desert.Helper;

public class TextEditorHelper : AvaloniaObject
{
    public static readonly AttachedProperty<bool> ScrollToTopOnDocChangeProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("ScrollToTopOnDocChange", typeof(TextEditorHelper));

    static TextEditorHelper()
    {
        ScrollToTopOnDocChangeProperty.Changed.AddClassHandler<TextEditor>(HandlePropertyChange);
    }

    public static void SetScrollToTopOnDocChange(AvaloniaObject element, bool value)
    {
        element.SetValue(ScrollToTopOnDocChangeProperty, value);
    }

    public static bool GetScrollToTopOnDocChange(AvaloniaObject element)
    {
        return element.GetValue(ScrollToTopOnDocChangeProperty);
    }
    
    private static void HandlePropertyChange(TextEditor editor, AvaloniaPropertyChangedEventArgs arg2)
    {
        editor.TextChanged += (_, _) =>
        {
            editor.ScrollTo(0, 0);
        };
    }
}