using AutoDesktopApplication.Models;
using AutoDesktopApplication.Services;
using AutoDesktopApplication.ViewModels;
using Microsoft.Maui.Media;
using Microsoft.Maui.Accessibility; // Added for SemanticScreenReader
using Microsoft.Maui.ApplicationModel; // Ensures replacement for Essentials
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace AutoDesktopApplication
{
    public partial class MainPage : ContentPage
    {
        private readonly VisionApiService _visionApiService;
        private readonly IScreenshot _screenshotService; 
        private readonly MainViewModel _viewModel;

        public MainPage(VisionApiService visionApiService, IScreenshot screenshotService, MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel; 
            _visionApiService = visionApiService;
            _screenshotService = screenshotService; 
        }

        private async void OnTestVisionApiClicked(object sender, EventArgs e)
        {
            try
            {
                if (_screenshotService == null)
                {
                    await DisplayAlert("Error", "Screenshot service is not available.", "OK");
                    return;
                }

                var screenshotResult = await _screenshotService.CaptureAsync(); 
                if (screenshotResult == null)
                {
                    await DisplayAlert("Error", "Failed to capture screenshot.", "OK");
                    return;
                }

                using var stream = await screenshotResult.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);

                var request = new DetectionRequest { Screenshot = base64Image };
                var response = await _visionApiService.DetectObjectsAsync(request); 

                if (response != null && response.Detections != null && response.Detections.Any())
                {
                    var firstDetection = response.Detections.First();
                    await DisplayAlert("Vision API Test", $"Detected: {firstDetection.Label} with confidence {firstDetection.Confidence}", "OK");
                }
                else if (response != null && !string.IsNullOrEmpty(response.Error))
                {
                    await DisplayAlert("Vision API Test", $"API Error: {response.Error}", "OK");
                }
                else
                {
                    await DisplayAlert("Vision API Test", "No detections found or error in response.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }
        
        private void OnDebugButtonClicked(object sender, EventArgs e)
        {
            if (_viewModel?.NavigateToInputLogCommand?.CanExecute(null) == true)
            {
                _viewModel.NavigateToInputLogCommand.Execute(null);
            }
            else
            {
                DisplayAlert("Info", "Debug log command not available.", "OK");
            }
        }
    }
}
