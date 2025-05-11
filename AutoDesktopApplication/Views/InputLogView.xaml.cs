using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AutoDesktopApplication.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // For Colors
using Microsoft.Maui.ApplicationModel; // For MainThread
using Label = Microsoft.Maui.Controls.Label;

namespace AutoDesktopApplication.Views
{
    public partial class InputLogView : ContentPage
    {
        private readonly InputLogService _logService;
        
        public InputLogView(InputLogService logService)
        {
            InitializeComponent();
            _logService = logService;
            
            // Subscribe to collection changes
            _logService.LogMessages.CollectionChanged += OnLogMessagesChanged;
            
            // Initial population of logs
            RefreshLogEntries();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _logService.LogMessages.CollectionChanged -= OnLogMessagesChanged;
        }

        private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(RefreshLogEntries);
        }
        
        private void RefreshLogEntries()
        {
            LogContainer.Children.Clear();
            
            foreach (var logMessage in _logService.LogMessages)
            {
                // Create a label for the log message
                var label = new Label
                {
                    Text = logMessage.FormattedMessage,
                    LineBreakMode = LineBreakMode.WordWrap
                };
                
                // Set color based on message type
                switch (logMessage.Type)
                {
                    case LogMessageType.Recording:
                        label.TextColor = Colors.Blue;
                        break;
                    case LogMessageType.Playback:
                        label.TextColor = Colors.Green;
                        break;
                    case LogMessageType.Error:
                        label.TextColor = Colors.Red;
                        break;
                    default:
                        label.TextColor = Colors.Black;
                        break;
                }
                
                LogContainer.Children.Add(label);
            }
        }

        private void OnClearLogClicked(object sender, EventArgs e)
        {
            _logService.ClearLogs();
        }

        private async void OnCopyToClipboardClicked(object sender, EventArgs e)
        {
            try
            {
                var allLogs = string.Join(Environment.NewLine, _logService.LogMessages.Select(log => log.FormattedMessage));
                if (!string.IsNullOrEmpty(allLogs))
                {
                    await Clipboard.SetTextAsync(allLogs);
                    await DisplayAlert("Success", "Log content copied to clipboard.", "OK");
                }
                else
                {
                    await DisplayAlert("Info", "Log is empty. Nothing to copy.", "OK");
                }
            }
            catch (Exception ex)
            {
                // Log the error or display a more specific error message
                await DisplayAlert("Error", $"Failed to copy logs to clipboard: {ex.Message}", "OK");
            }
        }
    }
}