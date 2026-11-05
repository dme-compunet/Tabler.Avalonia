namespace Tabler.Avalonia;

internal static class IconMetadataProvider
{
    private static readonly Lazy<TablerIconMetadata[]> _data = new(Initialize);

    public static TablerIconMetadata GetMetadata(TablerIconKind icon) => _data.Value[(int)icon];

    private static TablerIconMetadata[] Initialize()
    {
        using var stream = EmbeddedResource.Open("metadata.bin");
        using var reader = new BinaryReader(stream);

        try
        {
            var count = Enum.GetValues<TablerIconKind>().Length;
            var result = new TablerIconMetadata[count];

            for (var index = 0; index < count; index++)
            {
                var name = reader.ReadString();
                var isFilled = reader.ReadBoolean();
                var category = reader.ReadString();
                var tags = reader.ReadString();

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
        catch (Exception ex)
        {
            throw new InvalidDataException("The icon metadata file is corrupted or in an invalid format.", ex);
        }
    }
}
