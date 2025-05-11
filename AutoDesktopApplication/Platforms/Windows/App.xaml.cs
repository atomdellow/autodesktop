using Microsoft.UI.Xaml;

namespace AutoDesktopApplication.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
