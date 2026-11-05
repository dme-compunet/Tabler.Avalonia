namespace Tabler.Avalonia.Gallery.ViewModels;

public enum IconVariantFilter
{
    All,
    Outline,
    Filled,
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    public const double DefaultSize = 24;
    public const double DefaultStrokeWidth = 2;

    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(300);

    private readonly IThemeService _themeService;
    private readonly Lazy<IconViewModel[]> _allIcons;

    private CancellationTokenSource? _filterCts;

    public MainWindowViewModel(IIconCopyService copyService, IThemeService themeService)
    {
        _themeService = themeService;
        _allIcons = new(() => [.. Enum.GetValues<TablerIconKind>().Select(k => new IconViewModel(k, copyService))]);

        _ = ApplyFilterAsync(TimeSpan.Zero);
    }

    public IconVariantFilter[] Variants { get; } = Enum.GetValues<IconVariantFilter>();

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial double IconSize { get; set; } = DefaultSize;

    [ObservableProperty]
    public partial double StrokeWidth { get; set; } = DefaultStrokeWidth;

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial IconVariantFilter SelectedVariant { get; set; } = IconVariantFilter.All;

    [ObservableProperty]
    public partial IReadOnlyList<IconViewModel> Icons { get; set; } = [];

    [ObservableProperty]
    public partial string? CountText { get; set; }

    partial void OnSearchTextChanged(string? value) => _ = ApplyFilterAsync(SearchDebounce);

    partial void OnSelectedVariantChanged(IconVariantFilter value) => _ = ApplyFilterAsync(TimeSpan.Zero);

    [RelayCommand]
    private void Reset()
    {
        IconSize = DefaultSize;
        StrokeWidth = DefaultStrokeWidth;
    }

    [RelayCommand]
    private void ToggleTheme() => _themeService.ToggleBaseTheme();

    /// <summary>
    /// Debounced, cancellable filtering. Matching runs on the thread pool; results are
    /// applied back on the UI thread (the awaits capture the UI synchronization context).
    /// </summary>
    private async Task ApplyFilterAsync(TimeSpan delay)
    {
        _filterCts?.Cancel();
        var cts = _filterCts = new CancellationTokenSource();

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cts.Token);
            }

            IsBusy = true;

            var search = SearchText;
            var variant = SelectedVariant;

            var result = await Task.Run(() => Filter(_allIcons.Value, search, variant), cts.Token);

            cts.Token.ThrowIfCancellationRequested();

            Icons = result;
            CountText = $"{result.Length} of {_allIcons.Value.Length} icons";
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer search.
        }
        finally
        {
            // All of this runs on the UI thread, so no locking is needed. Clear the field
            // before disposing, so the next call never cancels a disposed source.
            if (ReferenceEquals(_filterCts, cts))
            {
                _filterCts = null;
                IsBusy = false;
            }

            cts.Dispose();
        }
    }

    private static IconViewModel[] Filter(IconViewModel[] icons, string? search, IconVariantFilter variant)
    {
        TablerIconVariant? wanted = variant switch
        {
            IconVariantFilter.Outline => TablerIconVariant.Outline,
            IconVariantFilter.Filled => TablerIconVariant.Filled,
            _ => null,
        };

        var queue = new PriorityQueue<IconViewModel, (int Priority, int Index)>(icons.Length);

        for (var i = 0; i < icons.Length; i++)
        {
            var icon = icons[i];

            if (wanted is not null && icon.Metadata.Variant != wanted)
            {
                continue;
            }

            if (icon.Metadata.Contains(search, out var priority))
            {
                queue.Enqueue(icon, (priority, i));
            }
        }

        var result = new IconViewModel[queue.Count];

        for (var i = 0; i < result.Length; i++)
        {
            result[i] = queue.Dequeue();
        }

        return result;
    }
}
