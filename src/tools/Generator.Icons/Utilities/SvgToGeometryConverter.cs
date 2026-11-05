using SkiaSharp;
using Svg.Skia;
using SvgToXaml.Model.Drawing;

namespace Generator.Icons.Utilities;

/// <summary>
/// The result of converting an SVG file: geometry that should be stroked and
/// geometry that should be filled. Tabler outline icons are mostly stroke-only,
/// but ~80 of them contain filled sub-paths (dots, etc.); filled icons are fill-only.
/// </summary>
public sealed record SvgGeometry(string Stroke, string[] Fills);

public static class SvgToGeometryConverter
{
    public static SvgGeometry GenerateFromFile(string path, int decimals = 2)
    {
        using var svg = SKSvg.CreateFromFile(path);

        if (svg.Model is null)
        {
            throw new InvalidOperationException($"Failed to load SVG '{path}'.");
        }

        var drawingGroup = new DrawingGroup(svg.Model);

        var stroke = new System.Text.StringBuilder();
        var fills = new List<string>();

        foreach (var item in EnumerateGeometryDrawing(drawingGroup.Children))
        {
            var skPath = item.Geometry ?? throw new InvalidOperationException($"Drawing without geometry in '{path}'.");

            var data = GeometryPathOptimizer.Optimize(skPath.ToSvgPathData(), decimals);

            if (string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            var style = item.Paint?.Style ?? throw new InvalidOperationException($"Drawing without paint in '{path}'.");

            if (style is ShimSkiaSharp.SKPaintStyle.Stroke or ShimSkiaSharp.SKPaintStyle.StrokeAndFill)
            {
                stroke.Append(data);
            }

            if (style is ShimSkiaSharp.SKPaintStyle.Fill or ShimSkiaSharp.SKPaintStyle.StrokeAndFill)
            {
                // Each SVG <path> is kept as its own fill geometry, so overlapping
                // sub-shapes don't punch holes into each other when combined (non-zero winding).
                var rule = skPath.FillType == SKPathFillType.EvenOdd ? "F0 " : "F1 ";
                fills.Add(rule + data);
            }
        }

        return new SvgGeometry(stroke.ToString(), [.. fills]);
    }

    private static IEnumerable<GeometryDrawing> EnumerateGeometryDrawing(IEnumerable<Drawing> source)
    {
        foreach (var drawing in source)
        {
            if (drawing is DrawingGroup group)
            {
                foreach (var item in EnumerateGeometryDrawing(group.Children))
                {
                    yield return item;
                }
            }
            else if (drawing is GeometryDrawing geometryDrawing)
            {
                yield return geometryDrawing;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported drawing type '{drawing.GetType().Name}'.");
            }
        }
    }
}
