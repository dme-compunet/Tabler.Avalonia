namespace Tabler.Avalonia;

/// <summary>
/// Markup extension for using an icon as content: <c>Content="{TablerIconContent Home}"</c>.
/// </summary>
public class TablerIconContentExtension : MarkupExtension
{
    public TablerIconKind Kind { get; set; }

    public double? Size { get; set; }

    public double? StrokeWidth { get; set; }

    public TablerIconContentExtension() { }

    public TablerIconContentExtension(TablerIconKind kind) => Kind = kind;

    public TablerIconContentExtension(TablerIconKind kind, double size)
    {
        Kind = kind;
        Size = size;
    }

    public TablerIconContentExtension(TablerIconKind kind, double size, double strokeWidth)
    {
        Kind = kind;
        Size = size;
        StrokeWidth = strokeWidth;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var icon = new TablerIcon { Kind = Kind };

        if (Size.HasValue)
        {
            icon.Size = Size.Value;
        }

        if (StrokeWidth.HasValue)
        {
            icon.StrokeWidth = StrokeWidth.Value;
        }

        return icon;
    }
}
