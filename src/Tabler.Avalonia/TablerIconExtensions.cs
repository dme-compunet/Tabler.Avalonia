namespace Tabler.Avalonia;

public static class TablerIconExtensions
{
    public static TablerIconData GetData(this TablerIconKind kind) => IconGeometryProvider.GetData(kind);

    public static TablerIconMetadata GetMetadata(this TablerIconKind kind) => IconMetadataProvider.GetMetadata(kind);
}
