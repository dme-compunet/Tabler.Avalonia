namespace Tabler.Avalonia.Gallery;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<INotificationService, ToastNotificationService>();
        services.AddSingleton<IThemeService, SukiThemeService>();
        services.AddSingleton<IIconCopyService, IconCopyService>();

        // Presentation
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
