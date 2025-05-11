using AutoDesktopApplication.Services;
using AutoDesktopApplication.ViewModels;
using AutoDesktopApplication.Views;

namespace AutoDesktopApplication;

public partial class MainPage : ContentPage
{
    private MainViewModel _viewModel;
    private readonly InputLogService _logService;

    public MainPage(MainViewModel viewModel, InputLogService logService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logService = logService;
        BindingContext = _viewModel;
    }

    private async void OnDebugButtonClicked(object sender, EventArgs e)
    {
        // Create and show the InputLogView
        var inputLogView = new InputLogView(_logService);
        await Navigation.PushAsync(inputLogView);
    }
}
