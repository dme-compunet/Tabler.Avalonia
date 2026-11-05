namespace Tabler.Avalonia.Gallery.Views;

public partial class MainWindow : SukiWindow
{
    // Parameterless constructor for the XAML previewer.
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel, ISukiToastManager toastManager) : this()
    {
        DataContext = viewModel;

        // The toast manager is a view concern; keeping it out of the view model
        // keeps the view model free of SukiUI types.
        ToastHost.Manager = toastManager;
    }
}
