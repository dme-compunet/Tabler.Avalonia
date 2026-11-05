namespace Tabler.Avalonia;

/// <summary>
/// Raw path data of a Tabler icon in a 24x24 coordinate space.
/// </summary>
/// <param name="Stroke">Path data drawn with a pen (empty for filled icons).</param>
/// <param name="Fills">Path data drawn with a brush, one entry per original SVG path.</param>
public sealed record TablerIconData(string Stroke, string[] Fills);

/// <summary>
/// Parsed, cached geometries of a Tabler icon.
/// </summary>
internal sealed class TablerIconGeometry(Geometry? stroke, Geometry[] fills)
{
    public Geometry? Stroke { get; } = stroke;

    public Geometry[] Fills { get; } = fills;
}
