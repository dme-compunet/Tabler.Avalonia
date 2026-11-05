namespace Tabler.Avalonia.Gallery.Services;

public interface IIconCopyService
{
    Task CopyAsync(string text);
}

/// <summary>Copies text to the clipboard and confirms with a toast.</summary>
public sealed class IconCopyService(IClipboardService clipboard, INotificationService notifications) : IIconCopyService
{
    public async Task CopyAsync(string text)
    {
        await clipboard.SetTextAsync(text);

        notifications.ShowInfo("Icon Copied!", $"The text \"{text}\" has been copied to clipboard.");
    }
}
