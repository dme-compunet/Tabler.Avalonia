namespace Tabler.Avalonia.Gallery.ViewModels;

/// <summary>
/// A single gallery tile. Immutable: size and stroke width are bound from the
/// main view model, so changing them doesn't touch 6,000+ item instances.
/// </summary>
public sealed partial class IconViewModel(TablerIconKind kind, IIconCopyService copyService)
{
    public TablerIconKind Kind { get; } = kind;

    public TablerIconMetadata Metadata { get; } = kind.GetMetadata();

    public string Tip => Metadata.Category.Length > 0
        ? $"{Kind}  ({Metadata.Name}, {Metadata.Variant}, {Metadata.Category})"
        : $"{Kind}  ({Metadata.Name}, {Metadata.Variant})";

    [RelayCommand]
    private Task CopyNameAsync() => copyService.CopyAsync($"{Kind}");

    [RelayCommand]
    private Task CopyXamlAsync() => copyService.CopyAsync($"<TablerIcon Kind=\"{Kind}\"/>");

    [RelayCommand]
    private Task CopyExtensionAsync() => copyService.CopyAsync($"{{TablerIcon {Kind}}}");
}
