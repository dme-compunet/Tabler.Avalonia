using Avalonia.Layout;

namespace Tabler.Avalonia.Gallery.Behaviors;

/// <summary>
/// Attached behavior for an <see cref="ItemsRepeater"/> using a <see cref="UniformGridLayout"/>:
/// keeps the layout's cell size in sync with the content size.
/// <para>
/// <see cref="UniformGridLayout"/> sizes every cell from the first measured item and caches it,
/// so changing the icon size alone would leave tiles at their old size (clipping or gaps).
/// Cell size = <c>ItemSize + ItemChrome</c> (chrome = the tile's padding + margin).
/// </para>
/// </summary>
public static class UniformGridAssist
{
    public static readonly AttachedProperty<double> ItemSizeProperty =
        AvaloniaProperty.RegisterAttached<ItemsRepeater, ItemsRepeater, double>("ItemSize", double.NaN);

    public static readonly AttachedProperty<double> ItemChromeProperty =
        AvaloniaProperty.RegisterAttached<ItemsRepeater, ItemsRepeater, double>("ItemChrome");

    static UniformGridAssist()
    {
        ItemSizeProperty.Changed.AddClassHandler<ItemsRepeater>((repeater, _) => Apply(repeater));
        ItemChromeProperty.Changed.AddClassHandler<ItemsRepeater>((repeater, _) => Apply(repeater));

        // In XAML, attributes (and their bindings) are applied before property elements
        // such as <ItemsRepeater.Layout>, so re-apply once the layout is assigned.
        ItemsRepeater.LayoutProperty.Changed.AddClassHandler<ItemsRepeater>((repeater, _) => Apply(repeater));
    }

    public static double GetItemSize(ItemsRepeater element) => element.GetValue(ItemSizeProperty);

    public static void SetItemSize(ItemsRepeater element, double value) => element.SetValue(ItemSizeProperty, value);

    public static double GetItemChrome(ItemsRepeater element) => element.GetValue(ItemChromeProperty);

    public static void SetItemChrome(ItemsRepeater element, double value) => element.SetValue(ItemChromeProperty, value);

    private static void Apply(ItemsRepeater repeater)
    {
        var size = GetItemSize(repeater);

        if (double.IsNaN(size) || repeater.Layout is not UniformGridLayout layout)
        {
            return;
        }

        var cell = size + GetItemChrome(repeater);

        layout.MinItemWidth = cell;
        layout.MinItemHeight = cell;
        layout.ItemsStretch = UniformGridLayoutItemsStretch.None;

        repeater.InvalidateMeasure();
    }
}
