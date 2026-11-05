using Avalonia.Input.Platform;

namespace Tabler.Avalonia.Gallery.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

/// <summary>
/// Clipboard access through the main window's <see cref="TopLevel"/>
/// (Avalonia 12: always resolve it with <see cref="TopLevel.GetTopLevel"/>).
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            && TopLevel.GetTopLevel(window)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
