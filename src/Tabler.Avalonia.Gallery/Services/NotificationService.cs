namespace Tabler.Avalonia.Gallery.Services;

public interface INotificationService
{
    void ShowInfo(string title, string message);
}

public sealed class ToastNotificationService(ISukiToastManager toastManager) : INotificationService
{
    public void ShowInfo(string title, string message) =>
        toastManager.CreateSimpleInfoToast()
                    .WithTitle(title)
                    .WithContent(message)
                    .Queue();
}
