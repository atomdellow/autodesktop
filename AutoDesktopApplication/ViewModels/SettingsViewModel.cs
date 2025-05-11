using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoDesktopApplication.Services;
using CommunityToolkit.Mvvm.Input;

namespace AutoDesktopApplication.ViewModels
{
    /// <summary>
    /// ViewModel for the application settings view
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly OllamaService _ollamaService;
        
        private bool _ollamaServiceAvailable;
        private string _selectedModel = string.Empty; // CS8618: Initialized _selectedModel
        private ObservableCollection<string> _availableModels = new ObservableCollection<string>(); // CS8618: Initialized _availableModels

        public SettingsViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _ollamaService = mainViewModel.GetOllamaService();
            
            AvailableModels = new ObservableCollection<string>();
            
            // Initialize commands
            CheckOllamaStatusCommand = new AsyncRelayCommand(CheckOllamaStatusAsync);
            BackCommand = new RelayCommand(NavigateBack);
            
            // Check Ollama service when opening settings
            _ = CheckOllamaStatusAsync();
        }

        #region Properties

        public bool OllamaServiceAvailable
        {
            get => _ollamaServiceAvailable;
            set => SetProperty(ref _ollamaServiceAvailable, value);
        }

        public string SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (SetProperty(ref _selectedModel, value))
                {
                    _mainViewModel.SelectedAiModel = value;
                }
            }
        }

        public ObservableCollection<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        #endregion

        #region Commands

        public ICommand CheckOllamaStatusCommand { get; }
        public ICommand BackCommand { get; }

        #endregion

        #region Methods

        private async Task CheckOllamaStatusAsync()
        {
            await ExecuteAsync(async () =>
            {
                OllamaServiceAvailable = await _ollamaService.IsServiceAvailableAsync();
                
                if (OllamaServiceAvailable)
                {
                    var models = await _ollamaService.GetAvailableModelsAsync();
                    
                    AvailableModels.Clear();
                    foreach (var model in models)
                    {
                        AvailableModels.Add(model);
                    }
                    
                    // Set the selected model or keep it if it's already set
                    if (string.IsNullOrEmpty(_mainViewModel.SelectedAiModel) && models.Count > 0)
                    {
                        SelectedModel = models[0];
                    }
                    else if (!string.IsNullOrEmpty(_mainViewModel.SelectedAiModel))
                    {
                        SelectedModel = _mainViewModel.SelectedAiModel;
                    }
                }
                else
                {
                    AvailableModels.Clear();
                    ErrorMessage = "Ollama service is not available. Please make sure it's running at http://localhost:11434";
                }
            }, "Checking Ollama status...");
        }

        private void NavigateBack()
        {
            _mainViewModel.NavigateToProjectsCommand.Execute(null);
        }

        #endregion
    }
}