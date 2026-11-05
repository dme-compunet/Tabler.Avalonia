using System.Collections.Concurrent;

namespace Tabler.Avalonia;

internal static class IconGeometryProvider
{
    private static readonly ConcurrentDictionary<TablerIconKind, TablerIconGeometry> _cache = [];
    private static readonly Lazy<TablerIconData[]> _data = new(Initialize);

    public static TablerIconData GetData(TablerIconKind icon) => _data.Value[(int)icon];

    public static TablerIconGeometry GetGeometry(TablerIconKind icon) =>
        _cache.GetOrAdd(icon, static kind =>
        {
            var data = GetData(kind);

            var stroke = string.IsNullOrEmpty(data.Stroke) ? null : Geometry.Parse(data.Stroke);
            var fills = data.Fills.Length == 0 ? [] : Array.ConvertAll(data.Fills, Geometry.Parse);

            return new TablerIconGeometry(stroke, fills);
        });

    private static TablerIconData[] Initialize()
    {
        using var stream = EmbeddedResource.Open("icons.bin");
        using var reader = new BinaryReader(stream);

        try
        {
            var count = Enum.GetValues<TablerIconKind>().Length;
            var result = new TablerIconData[count];

            for (var index = 0; index < count; index++)
            {
                var stroke = reader.ReadString();
                var fills = new string[reader.ReadInt32()];

                for (var i = 0; i < fills.Length; i++)
                {
                    fills[i] = reader.ReadString();
                }

                result[index] = new TablerIconData(stroke, fills);
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("The icon binary file is corrupted or in an invalid format.", ex);
        }
    }
}
