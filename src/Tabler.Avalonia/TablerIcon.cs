namespace Tabler.Avalonia;

/// <summary>
/// Renders a Tabler icon. Outline icons are drawn with a pen (<see cref="StrokeWidth"/>),
/// filled icons and filled sub-paths of outline icons are drawn with <see cref="Foreground"/>.
/// </summary>
public class TablerIcon : Control
{
    private const double RawIconSize = 24;

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<TablerIcon, double>(nameof(Size), 24);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<TablerIcon>();

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<TablerIcon, double>(nameof(StrokeWidth), 1.8);

    public static readonly StyledProperty<TablerIconKind?> KindProperty =
        AvaloniaProperty.Register<TablerIcon, TablerIconKind?>(nameof(Kind));

    private Pen? _pen;
    private TablerIconGeometry? _geometry;

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public TablerIconKind? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    static TablerIcon()
    {
        AffectsRender<TablerIcon>(KindProperty, SizeProperty, ForegroundProperty, StrokeWidthProperty);
        AffectsMeasure<TablerIcon>(SizeProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == KindProperty)
        {
            var kind = change.GetNewValue<TablerIconKind?>();
            _geometry = kind is null ? null : IconGeometryProvider.GetGeometry(kind.Value);
        }
        else if (change.Property == ForegroundProperty || change.Property == StrokeWidthProperty)
        {
            _pen = null;
        }

        base.OnPropertyChanged(change);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override Size ArrangeOverride(Size finalSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        // Transparent background for pointer interaction.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

        if (_geometry is null)
        {
            return;
        }

        var brush = Foreground;
        var scale = Size / RawIconSize;

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            foreach (var fill in _geometry.Fills)
            {
                context.DrawGeometry(brush, null, fill);
            }

            if (_geometry.Stroke is not null)
            {
                _pen ??= new Pen(brush, StrokeWidth, null, PenLineCap.Round, PenLineJoin.Round);
                context.DrawGeometry(null, _pen, _geometry.Stroke);
            }
        }
    }
}
