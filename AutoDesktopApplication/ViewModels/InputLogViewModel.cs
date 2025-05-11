using System;
using System.Collections.ObjectModel;
using AutoDesktopApplication.Services;

namespace AutoDesktopApplication.ViewModels
{
    /// <summary>
    /// ViewModel for the Input Log view
    /// </summary>
    public class InputLogViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly InputLogService _logService;

        public InputLogViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _logService = _mainViewModel.GetInputLogService();
        }

        /// <summary>
        /// Gets the log messages from the service
        /// </summary>
        public ObservableCollection<InputLogMessage> LogMessages => _logService.LogMessages;

        /// <summary>
        /// Clears all log messages
        /// </summary>
        public void ClearLogs()
        {
            _logService.ClearLogs();
        }
    }
}