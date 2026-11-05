namespace Tabler.Avalonia.Gallery.Services;

public interface IThemeService
{
    void ToggleBaseTheme();
}

public sealed class SukiThemeService : IThemeService
{
    public void ToggleBaseTheme() => SukiTheme.GetInstance().SwitchBaseTheme();
}
