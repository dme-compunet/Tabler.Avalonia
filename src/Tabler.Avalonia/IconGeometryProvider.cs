using System.Collections.Concurrent;

namespace Tabler.Avalonia;

internal static class IconGeometryProvider
{
    private static readonly ConcurrentDictionary<TablerIconKind, TablerIconGeometry> _cache = [];
    private static readonly Lazy<IconPathCodec.IconPathTable> _table = new(Initialize);
    // Indexed by enum value. Values of removed icons decode to empty data, so code compiled
    // against an older version of the package renders nothing instead of a wrong icon.
    private static readonly TablerIconData?[] _data = new TablerIconData?[TablerIconKindInfo.Capacity];

    public static TablerIconData GetData(TablerIconKind icon)
    {
        var index = (int)icon;

        if ((uint)index >= (uint)_data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown icon.");
        }

        if (Volatile.Read(ref _data[index]) is { } data)
        {
            return data;
        }

        // Icons are decoded on demand, so only the compact binary stays in memory.
        var (stroke, fills) = _table.Value.Decode(index);

        return Interlocked.CompareExchange(ref _data[index], new TablerIconData(stroke, fills), null) ?? _data[index]!;
    }

    public static TablerIconGeometry GetGeometry(TablerIconKind icon) =>
        _cache.GetOrAdd(icon, static kind =>
        {
            var data = GetData(kind);

            var stroke = string.IsNullOrEmpty(data.Stroke) ? null : Geometry.Parse(data.Stroke);
            var fills = data.Fills.Length == 0 ? [] : Array.ConvertAll(data.Fills, Geometry.Parse);

            return new TablerIconGeometry(stroke, fills);
        });

    private static IconPathCodec.IconPathTable Initialize()
    {
        using var stream = EmbeddedResource.Open("icons.bin");
        using var buffer = new MemoryStream();

        try
        {
            stream.CopyTo(buffer);

            var table = IconPathCodec.Open(buffer.ToArray());

            if (table.Count != _data.Length)
            {
                throw new InvalidDataException($"Expected {_data.Length} icons but found {table.Count}.");
            }

            return table;
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("The icon binary file is corrupted or in an invalid format.", ex);
        }
    }
}
