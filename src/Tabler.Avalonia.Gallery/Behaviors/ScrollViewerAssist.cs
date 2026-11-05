namespace Tabler.Avalonia.Gallery.Behaviors;

/// <summary>
/// Attached behavior: scrolls a <see cref="ScrollViewer"/> back to the top whenever the
/// bound value changes. Used to reset the icon grid when the result list is replaced;
/// otherwise scroll anchoring keeps an arbitrary offset into the new list.
/// </summary>
public static class ScrollViewerAssist
{
    public static readonly AttachedProperty<object?> ScrollToTopOnChangeProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, ScrollViewer, object?>("ScrollToTopOnChange");

    static ScrollViewerAssist()
    {
        ScrollToTopOnChangeProperty.Changed.AddClassHandler<ScrollViewer>((scrollViewer, _) =>
            scrollViewer.Offset = default);
    }

    public static object? GetScrollToTopOnChange(ScrollViewer element) =>
        element.GetValue(ScrollToTopOnChangeProperty);

    public static void SetScrollToTopOnChange(ScrollViewer element, object? value) =>
        element.SetValue(ScrollToTopOnChangeProperty, value);
}
