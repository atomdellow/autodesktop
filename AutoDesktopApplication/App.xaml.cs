namespace AutoDesktopApplication;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }
    
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        
        // Set default window size for desktop
        window.Width = 1200;
        window.Height = 800;
        window.Title = "Auto Desktop Application";
        
        return window;
    }
}
