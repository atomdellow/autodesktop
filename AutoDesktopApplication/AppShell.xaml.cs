using AutoDesktopApplication.Views;

namespace AutoDesktopApplication;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        // Register routes for navigation
        Routing.RegisterRoute(nameof(InputLogView), typeof(InputLogView));
    }
}