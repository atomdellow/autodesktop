using AutoDesktopApplication.Models;
using AutoDesktopApplication.Services;
using AutoDesktopApplication.ViewModels;
using Microsoft.Maui.Media;
using Microsoft.Maui.Graphics;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using SkiaSharp;

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

                ScreenshotDisplayImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));

                var base64Image = Convert.ToBase64String(imageBytes);
                var request = new DetectionRequest { Screenshot = base64Image };
                var response = await _visionApiService.DetectObjectsAsync(request);

                if (response != null && response.Detections != null && response.Detections.Any())
                {
                    SKBitmap? originalBitmap = null;
                    try
                    {
                        using (var imageStream = new MemoryStream(imageBytes))
                        {
                            originalBitmap = SKBitmap.Decode(imageStream);
                        }

                        if (originalBitmap != null)
                        {
                            using (var canvas = new SKCanvas(originalBitmap))
                            {
                                foreach (var detection in response.Detections)
                                {
                                    if (detection.Width > 0 && detection.Height > 0)
                                    {
                                        var rect = new SKRect(detection.X, detection.Y, detection.X + detection.Width, detection.Y + detection.Height);
                                        
                                        using (var paint = new SKPaint
                                        {
                                            Style = SKPaintStyle.Stroke,
                                            Color = SKColors.Red,
                                            StrokeWidth = 3,
                                            IsAntialias = true
                                        })
                                        {
                                            canvas.DrawRect(rect, paint);
                                        }

                                        using (var textPaint = new SKPaint
                                        {
                                            Color = SKColors.Yellow,
                                            TextSize = 20,
                                            IsAntialias = true,
                                            Style = SKPaintStyle.Fill,
                                            TextAlign = SKTextAlign.Left
                                        })
                                        {
                                            var textLabel = $"{detection.Label} ({detection.Confidence:F2})";
                                            var textBounds = new SKRect();
                                            textPaint.MeasureText(textLabel, ref textBounds);
                                            
                                            float textX = detection.X;
                                            float textY = detection.Y - 5;

                                            if (textY - textBounds.Height < 0) textY = detection.Y + detection.Height + textBounds.Height + 5;
                                            if (textX + textBounds.Width > originalBitmap.Width) textX = originalBitmap.Width - textBounds.Width - 2;
                                            if (textX < 0) textX = 2;

                                            var backgroundRect = SKRect.Create(textX, textY + textBounds.Top - 2, textBounds.Width + 4, textBounds.Height + 4);
                                            using (var bgPaint = new SKPaint { Color = SKColors.Black.WithAlpha(180), Style = SKPaintStyle.Fill })
                                            {
                                                canvas.DrawRect(backgroundRect, bgPaint);
                                            }
                                            canvas.DrawText(textLabel, textX + 2, textY, textPaint);
                                        }
                                    }
                                }
                            }

                            using (var ms = new MemoryStream())
                            using (var skImage = SKImage.FromBitmap(originalBitmap))
                            using (var data = skImage.Encode(SKEncodedImageFormat.Png, 100))
                            {
                                data.SaveTo(ms);
                                var annotatedImageBytes = ms.ToArray();
                                ScreenshotDisplayImage.Source = ImageSource.FromStream(() => new MemoryStream(annotatedImageBytes));
                            }
                        }
                        else
                        {
                            await DisplayAlert("Error", "Could not decode screenshot for drawing (originalBitmap is null).", "OK");
                        }
                    }
                    catch (Exception drawingEx)
                    {
                        await DisplayAlert("Drawing Error", $"Could not draw detections: {drawingEx.Message}", "OK");
                    }
                    finally
                    {
                        originalBitmap?.Dispose();
                    }
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
                await DisplayAlert("Error", $"An error occurred: {ex.Message}\n{ex.StackTrace}", "OK");
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
