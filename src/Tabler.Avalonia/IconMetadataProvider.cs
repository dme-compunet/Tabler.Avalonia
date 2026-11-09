namespace Tabler.Avalonia;

internal static class IconMetadataProvider
{
    // Indexed by enum value; null for values of removed icons.
    private static readonly Lazy<TablerIconMetadata?[]> _data = new(Initialize);

    public static TablerIconMetadata GetMetadata(TablerIconKind icon)
    {
        var data = _data.Value;
        var index = (int)icon;

        return (uint)index < (uint)data.Length && data[index] is { } metadata
            ? metadata
            : throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown icon.");
    }

    private static TablerIconMetadata?[] Initialize()
    {
        using var stream = EmbeddedResource.Open("metadata.bin");
        using var reader = new BinaryReader(stream);

        try
        {
            var count = reader.ReadInt32();

            if (count != TablerIconKindInfo.Capacity)
            {
                throw new InvalidDataException($"Expected {TablerIconKindInfo.Capacity} icons but found {count}.");
            }

            var result = new TablerIconMetadata?[count];

            for (var index = 0; index < count; index++)
            {
                var name = reader.ReadString();
                var isFilled = reader.ReadBoolean();
                var category = reader.ReadString();
                var tags = reader.ReadString();

                // An empty name marks the value of a removed icon.
                if (name.Length == 0)
                {
                    continue;
                }

                result[index] = new TablerIconMetadata
                {
                    Kind = (TablerIconKind)index,
                    Name = name,
                    Variant = isFilled ? TablerIconVariant.Filled : TablerIconVariant.Outline,
                    Category = category,
                    Tags = tags.Length == 0 ? [] : tags.Split(','),
                };
            }

            return result;
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("The icon metadata file is corrupted or in an invalid format.", ex);
        }
    }
}
