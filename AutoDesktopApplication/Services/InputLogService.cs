using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using AutoDesktopApplication.Models;

namespace AutoDesktopApplication.Services
{
    public class InputLogMessage
    {
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogMessageType Type { get; set; }
        
        public string FormattedMessage => $"[{Timestamp.ToString("HH:mm:ss.fff")}] {Message}";
    }
    
    public enum LogMessageType
    {
        Recording,
        Playback,
        Info,
        Error
    }
    
    /// <summary>
    /// Service to log recording and playback of inputs for debugging
    /// </summary>
    public class InputLogService
    {
        private readonly ObservableCollection<InputLogMessage> _logMessages = new ObservableCollection<InputLogMessage>();
        private const int MaxLogMessages = 1000;
        
        public ObservableCollection<InputLogMessage> LogMessages => _logMessages;
        
        public void LogKeyEvent(string message)
        {
            LogRecording(message);
        }
        
        public void LogRecording(string message)
        {
            AddLogMessage(message, LogMessageType.Recording);
        }
        
        public void LogPlayback(string message)
        {
            AddLogMessage(message, LogMessageType.Playback);
        }
        
        public void LogInfo(string message)
        {
            AddLogMessage(message, LogMessageType.Info);
        }
        
        public void LogError(string message)
        {
            AddLogMessage(message, LogMessageType.Error);
        }
        
        private void AddLogMessage(string message, LogMessageType type)
        {
            var logMessage = new InputLogMessage
            {
                Message = message,
                Type = type
            };
            
            // Add to collection on the UI thread
            MainThread.BeginInvokeOnMainThread(() => 
            {
                _logMessages.Insert(0, logMessage); // Add to beginning for newest-first order
                
                // Limit log size
                if (_logMessages.Count > MaxLogMessages)
                {
                    _logMessages.RemoveAt(_logMessages.Count - 1);
                }
            });
            
            // Also write to debug output
            Debug.WriteLine($"[{type}] {message}");
        }
        
        public void ClearLogs()
        {
            MainThread.BeginInvokeOnMainThread(() => _logMessages.Clear());
        }
        
        /// <summary>
        /// Format details of a keyboard action into a readable log entry
        /// </summary>
        public string FormatKeyboardAction(KeyboardAction action)
        {
            string typeStr = action.ActionType.ToString();
            string modifiers = "";
            
            if (action.Shift) modifiers += "Shift+";
            if (action.Ctrl) modifiers += "Ctrl+";
            if (action.Alt) modifiers += "Alt+";
            if (action.Win) modifiers += "Win+";
            
            string keyInfo = action.IsDirectTextInput ? 
                $"Text: \"{action.Key}\"" : 
                $"Key: {action.Key}";
                
            return $"{typeStr} {modifiers}{keyInfo} (Time: {action.RelativeTimeMs}ms)";
        }
        
        /// <summary>
        /// Format details of a mouse action into a readable log entry
        /// </summary>
        public string FormatMouseAction(MouseAction action)
        {
            return $"{action.ActionType} {action.Button} at X:{action.X}, Y:{action.Y} (Time: {action.RelativeTimeMs}ms)";
        }
    }
}