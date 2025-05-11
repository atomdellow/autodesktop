using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoDesktopApplication.Models;
using AutoDesktopApplication.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using WindowsInput.Native;
// Import the NuGet package's VirtualKeyCode with an alias to avoid conflicts
using InputSimVK = WindowsInput.Native.VirtualKeyCode;
using System.Diagnostics;

namespace AutoDesktopApplication.ViewModels
{
    /// <summary>
    /// ViewModel for the main application window
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;
        private readonly InputRecordingService _recordingService;
        private readonly InputPlaybackService _playbackService;
        private readonly ScreenCaptureService _screenshotService;
        private readonly OllamaService _ollamaService;
        private readonly KeyboardHookService _keyboardHookService;
        private readonly InputLogService _inputLogService;

        // Navigation state
        private ViewModelBase _currentViewModel = null!;

        // Collection of projects
        private ObservableCollection<Project> _projects = null!;
        private Project? _selectedProject;

        // Flag for recording state
        private bool _isRecording;
        
        // Flag for playback state and cancellation
        private bool _isPlaying;
        private CancellationTokenSource? _playbackCancellation;

        // Selected model for AI
        private string _selectedAiModel = string.Empty;
        private ObservableCollection<string> _availableAiModels = null!;

        // Key combination for stopping playback
        public const InputSimVK StopPlaybackHotkey = InputSimVK.ESCAPE;

        #region Hotkey Management

        private bool _hotkeyRegistered = false;

        /// <summary>
        /// Registers a global hotkey to stop playback (Escape key)
        /// </summary>
        private void RegisterStopPlaybackHotkey()
        {
            if (_hotkeyRegistered) return;

            try
            {
                // Set up the keyboard hook to listen for the Escape key
                _keyboardHookService.KeyDown += OnHotkeyDown;
                _keyboardHookService.StartHook();
                _hotkeyRegistered = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register hotkey: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregisters the global hotkey
        /// </summary>
        private void UnregisterStopPlaybackHotkey()
        {
            if (!_hotkeyRegistered) return;
            
            // Remove the keyboard hook
            _keyboardHookService.KeyDown -= OnHotkeyDown;
            _keyboardHookService.StopHook();
            _hotkeyRegistered = false;
        }

        /// <summary>
        /// Handler for hotkey press - updated to match the new delegate signature
        /// </summary>
        private void OnHotkeyDown(object sender, int vkCode)
        {
            // Check if Escape key is pressed
            if (vkCode == (int)StopPlaybackHotkey && IsPlaying)
            {
                // Cancel the playback
                _playbackCancellation?.Cancel();
            }
        }

        private void RegisterAbortHotkey()
        {
            // Subscribe to the specific Escape key event
            _keyboardHookService.EscapeKeyPressed += OnEscapeKeyPressed;
            _keyboardHookService.StartHook();
        }

        private void UnregisterAbortHotkey()
        {
            // Unsubscribe from the event
            _keyboardHookService.EscapeKeyPressed -= OnEscapeKeyPressed;
        }

        private void OnEscapeKeyPressed(object? sender, EventArgs e)
        {
            // If we're playing back a workflow, cancel it
            if (IsPlaying && _playbackCancellation != null && !_playbackCancellation.IsCancellationRequested)
            {
                _playbackCancellation.Cancel();
                IsPlaying = false;
                
                // Show a message on the UI thread
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => {
                    if (Microsoft.Maui.Controls.Application.Current?.MainPage != null)
                    {
                        Microsoft.Maui.Controls.Application.Current.MainPage.DisplayAlert(
                            "Playback Aborted", 
                            "Playback was stopped by pressing the Escape key", 
                            "OK");
                    }
                });
            }
        }

        #endregion

        public MainViewModel(
            AppDbContext dbContext,
            InputRecordingService recordingService,
            InputPlaybackService playbackService,
            ScreenCaptureService screenshotService,
            OllamaService ollamaService,
            KeyboardHookService keyboardHookService,
            InputLogService inputLogService)
        {
            _dbContext = dbContext;
            _recordingService = recordingService;
            _playbackService = playbackService;
            _screenshotService = screenshotService;
            _ollamaService = ollamaService;
            _keyboardHookService = keyboardHookService;
            _inputLogService = inputLogService;

            // Initialize commands
            LoadProjectsCommand = new AsyncRelayCommand(LoadProjectsAsync);
            CreateProjectCommand = new AsyncRelayCommand<string>(CreateProjectAsync);
            DeleteProjectCommand = new AsyncRelayCommand<Project>(DeleteProjectAsync);
            StartRecordingCommand = new RelayCommand(StartRecording);
            StopRecordingCommand = new RelayCommand(StopRecording);
            CheckOllamaStatusCommand = new AsyncRelayCommand(CheckOllamaStatusAsync);
            NavigateToProjectsCommand = new RelayCommand(() => CurrentViewModel = new ProjectsViewModel(this));
            NavigateToWorkflowsCommand = new RelayCommand<Project>(project => 
            {
                if (project != null)
                {
                    CurrentViewModel = new WorkflowsViewModel(this, project);
                }
            });
            NavigateToSettingsCommand = new RelayCommand(() => CurrentViewModel = new SettingsViewModel(this));
            NavigateToInputLogCommand = new RelayCommand(NavigateToInputLog);
            
            // Initialize collections
            Projects = new ObservableCollection<Project>();
            AvailableAiModels = new ObservableCollection<string>();
            
            // Initial screen is Projects
            CurrentViewModel = new ProjectsViewModel(this);

            // Register for playback service events
            _playbackService.ContinueIterationRequested += PlaybackService_ContinueIterationRequested;
        }

        #region Properties

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public ObservableCollection<Project> Projects
        {
            get => _projects;
            set => SetProperty(ref _projects, value);
        }

        public Project? SelectedProject
        {
            get => _selectedProject;
            set => SetProperty(ref _selectedProject, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            set => SetProperty(ref _isRecording, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public string SelectedAiModel
        {
            get => _selectedAiModel;
            set => SetProperty(ref _selectedAiModel, value);
        }

        public ObservableCollection<string> AvailableAiModels
        {
            get => _availableAiModels;
            set => SetProperty(ref _availableAiModels, value);
        }

        #endregion

        #region Commands

        public ICommand LoadProjectsCommand { get; }
        public ICommand CreateProjectCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand CheckOllamaStatusCommand { get; }
        public ICommand NavigateToProjectsCommand { get; }
        public ICommand NavigateToWorkflowsCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }
        public ICommand NavigateToInputLogCommand { get; }

        #endregion

        #region Methods

        public async Task LoadProjectsAsync()
        {
            await ExecuteAsync(async () =>
            {
                var projects = await _dbContext.Projects
                    .Include(p => p.Workflows)
                    .ToListAsync();

                Projects.Clear();
                foreach (var project in projects)
                {
                    Projects.Add(project);
                }
            }, "Loading projects...");
        }

        private async Task CreateProjectAsync(string? projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                ErrorMessage = "Project name cannot be empty";
                return;
            }

            await ExecuteAsync(async () =>
            {
                var project = new Project
                {
                    Name = projectName,
                    Description = "New project",
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                };

                try
                {
                    _dbContext.Projects.Add(project);
                    await _dbContext.SaveChangesAsync();
                    
                    Projects.Add(project);
                    SelectedProject = project;
                }
                catch (DbUpdateException ex)
                {
                    // Get the inner exception details for better error reporting
                    var innerMessage = ex.InnerException?.Message ?? ex.Message;
                    ErrorMessage = $"Failed to save project: {innerMessage}";
                    throw new Exception($"Failed to save project: {innerMessage}");
                }
            }, "Creating project...");
        }

        private async Task DeleteProjectAsync(Project? project)
        {
            if (project == null)
                return;

            await ExecuteAsync(async () =>
            {
                _dbContext.Projects.Remove(project);
                await _dbContext.SaveChangesAsync();
                
                Projects.Remove(project);
                
                if (SelectedProject == project)
                    SelectedProject = null;
            }, "Deleting project...");
        }

        private void StartRecording()
        {
            _inputLogService.LogInfo("MainViewModel: StartRecordingCommand executed.");
            if (SelectedProject == null)
            {
                ErrorMessage = "Please select a project before starting a recording.";
                _inputLogService.LogInfo("MainViewModel: StartRecording attempted without a selected project. (Was LogWarning)"); // Changed LogWarning
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () => {
                    if (Microsoft.Maui.Controls.Application.Current?.MainPage != null)
                    {
                        await Microsoft.Maui.Controls.Application.Current.MainPage.DisplayAlert("No Project Selected", "Please select or create a project first.", "OK");
                    }
                });
                return;
            }
            _recordingService.StartRecording();
            IsRecording = true;
            _inputLogService.LogInfo("MainViewModel: Recording started.");
        }

        private async void StopRecording() 
        {
            _inputLogService.LogInfo("MainViewModel: StopRecordingCommand executed.");
            IsRecording = false; 

            if (SelectedProject == null)
            {
                ErrorMessage = "No project selected to save the workflow.";
                _inputLogService.LogError("MainViewModel: StopRecording failed - SelectedProject is null.");
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () => {
                    if (Microsoft.Maui.Controls.Application.Current?.MainPage != null)
                    {
                        await Microsoft.Maui.Controls.Application.Current.MainPage.DisplayAlert("Error", "No project selected. Cannot save workflow.", "OK");
                    }
                });
                // Ensure hooks are released even if no project is selected
                _recordingService.StopRecording(); // Call simple stop
                return;
            }

            // Let's use a default name or derive it if the prompt is removed.
            string workflowName = $"Workflow - {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
            _inputLogService.LogInfo($"MainViewModel: Using default workflow name: {workflowName}");

            _inputLogService.LogInfo($"MainViewModel: Attempting to stop recording and save workflow with name: {workflowName}");
            
            Workflow? recordedWorkflow = _recordingService.StopRecordingAndSaveWorkflow(workflowName, $"Recorded on {DateTime.Now}", SelectedProject.Id);

            if (recordedWorkflow != null)
            {
                _inputLogService.LogInfo($"MainViewModel: Workflow '{recordedWorkflow.Name}' received from recording service with {recordedWorkflow.TaskBots.Count} TaskBots.");
                foreach (var taskBot in recordedWorkflow.TaskBots)
                {
                    _inputLogService.LogInfo($"MainViewModel: TaskBot Name: {taskBot.Name}, Type: {taskBot.Type}, Order: {taskBot.SequenceOrder}, InputData Length: {taskBot.InputData?.Length ?? 0}, Delay: {taskBot.DelayBefore}, Duration: {taskBot.EstimatedDuration}");
                }

                if (CurrentViewModel is WorkflowsViewModel workflowsVM)
                {
                    _inputLogService.LogInfo("MainViewModel: CurrentViewModel is WorkflowsViewModel. Attempting to add workflow via AddAndSaveWorkflowAsync.");
                    await workflowsVM.AddAndSaveWorkflowAsync(recordedWorkflow); 
                    _inputLogService.LogInfo("MainViewModel: Call to workflowsVM.AddAndSaveWorkflowAsync completed.");
                }
                else
                {
                    _inputLogService.LogInfo("MainViewModel: CurrentViewModel is not WorkflowsViewModel. Workflow not added to UI automatically. Saving to DB as fallback. (Was LogWarning)");
                    try
                    {
                        var dbContext = GetDbContext(); // Get a fresh context or use the existing one carefully.
                        dbContext.Workflows.Add(recordedWorkflow);
                        await dbContext.SaveChangesAsync();
                        _inputLogService.LogInfo($"MainViewModel: Workflow '{recordedWorkflow.Name}' saved to DB directly (fallback path).");
                    }
                    catch (Exception ex)
                    {
                        _inputLogService.LogError($"MainViewModel: Error saving workflow directly to DB (fallback path): {ex.Message} - Inner: {ex.InnerException?.Message}");
                    }
                }
            }
            else
            {
                _inputLogService.LogError("MainViewModel: StopRecordingAndSaveWorkflow returned null. Workflow not saved.");
                 Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () => {
                    if (Microsoft.Maui.Controls.Application.Current?.MainPage != null)
                    {
                        await Microsoft.Maui.Controls.Application.Current.MainPage.DisplayAlert("Error", "Failed to create workflow from recording.", "OK");
                    }
                });
            }
        }

        private async Task CheckOllamaStatusAsync()
        {
            await ExecuteAsync(async () =>
            {
                bool isAvailable = await _ollamaService.IsServiceAvailableAsync();
                if (isAvailable)
                {
                    var models = await _ollamaService.GetAvailableModelsAsync();
                    
                    AvailableAiModels.Clear();
                    foreach (var model in models)
                    {
                        AvailableAiModels.Add(model);
                    }
                    
                    if (models.Count > 0 && string.IsNullOrEmpty(SelectedAiModel))
                    {
                        SelectedAiModel = models[0];
                    }
                }
                else
                {
                    AvailableAiModels.Clear();
                    SelectedAiModel = string.Empty;
                    ErrorMessage = "Ollama service is not available. Please make sure it's running at http://localhost:11434";
                }
            }, "Checking Ollama status...");
        }

        // Additional service access methods required by the ViewModels
        public AppDbContext GetDbContext()
        {
            return _dbContext;
        }

        public OllamaService GetOllamaService()
        {
            return _ollamaService;
        }

        public InputLogService GetInputLogService()
        {
            return _inputLogService;
        }

        // Modified to create a temporary workflow to play a list of TaskBots
        public async Task PlayTaskSequenceAsync(System.Collections.Generic.List<TaskBot> tasks)
        {
            if (tasks == null || !tasks.Any())
            {
                _inputLogService.LogInfo("PlayTaskSequenceAsync called with no tasks.");
                return;
            }

            // Create a temporary workflow to host these tasks
            // Ensure Project is initialized as it's required by the Workflow model
            var tempWorkflow = new Workflow
            {
                Name = "Temporary Task Sequence",
                Description = "Ad-hoc execution of a list of tasks",
                TaskBots = tasks,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                ProjectId = SelectedProject?.Id ?? 0, // Use selected project or a default
                Project = SelectedProject ?? new Project { Id = 0, Name = "Default" } // Provide a default project if none selected
            };

            // Ensure SequenceOrder is set if not already, as ExecuteWorkflow relies on it.
            for (int i = 0; i < tempWorkflow.TaskBots.Count; i++)
            {
                if (tempWorkflow.TaskBots[i].SequenceOrder == 0) // Basic check, might need more robust logic
                {
                    tempWorkflow.TaskBots[i].SequenceOrder = i + 1;
                }
            }
            
            await _playbackService.ExecuteWorkflow(tempWorkflow, default, null);
        }
        
        public async Task ExecuteWorkflowAsync(Workflow workflow, IProgress<(int current, int total, string message)>? progress = null)
        {
            // Create a new cancellation token source for this playback
            _playbackCancellation?.Dispose();
            _playbackCancellation = new CancellationTokenSource();

            RegisterAbortHotkey(); // Use the simpler Escape key handler
            IsPlaying = true;

            try
            {
                await _playbackService.ExecuteWorkflow(workflow, _playbackCancellation.Token, progress);
            }
            catch (OperationCanceledException)
            {
                progress?.Report((0, 0, "Workflow execution cancelled by user."));
                _inputLogService.LogInfo("ExecuteWorkflowAsync cancelled by user.");
            }
            catch (Exception ex)
            {
                progress?.Report((0,0, $"Workflow execution failed: {ex.Message}"));
                _inputLogService.LogError($"ExecuteWorkflowAsync failed: {ex.Message}");
            }
            finally
            {
                UnregisterAbortHotkey();
                IsPlaying = false;
                _playbackCancellation?.Dispose();
                _playbackCancellation = null;
            }
        }

        public async Task ExecuteWorkflowWithIterationAsync(Workflow workflow, IProgress<(int current, int total, string message)>? progress = null)
        {
            _playbackCancellation?.Dispose();
            _playbackCancellation = new CancellationTokenSource();
            
            RegisterAbortHotkey(); 
            IsPlaying = true;

            try
            {
                _inputLogService.LogInfo($"Workflow '{workflow.Name}': Starting execution.");

                await _playbackService.ExecuteWorkflow(workflow, _playbackCancellation.Token, progress);

                if (_playbackCancellation.Token.IsCancellationRequested)
                {
                    _inputLogService.LogInfo($"Workflow '{workflow.Name}': Execution cancelled by user.");
                    progress?.Report((0, 0, "Workflow execution cancelled by user."));
                }
                else
                {
                    _inputLogService.LogInfo($"Workflow '{workflow.Name}': Execution completed.");
                    progress?.Report((1, 1, "Workflow execution completed.")); // Simplified progress reporting
                }
            }
            catch (OperationCanceledException)
            {
                progress?.Report((0, 0, "Workflow execution cancelled by user."));
                _inputLogService.LogInfo($"ExecuteWorkflowWithIterationAsync cancelled by user for workflow '{workflow.Name}'.");
            }
            catch (Exception ex)
            {
                progress?.Report((0,0, $"Workflow execution with iteration failed: {ex.Message}"));
                _inputLogService.LogError($"ExecuteWorkflowWithIterationAsync for workflow '{workflow.Name}' failed: {ex.Message}");
            }
            finally
            {
                UnregisterAbortHotkey();
                IsPlaying = false;
                _playbackCancellation?.Dispose();
                _playbackCancellation = null;
            }
        }
        
        private void PlaybackService_ContinueIterationRequested(object? sender, InputPlaybackService.ContinueIterationEventArgs e)
        {
            e.Continue();
        }

        public async Task<byte[]> CaptureScreenshotAsync()
        {
            return await _screenshotService.CaptureScreenshotAsync();
        }

        public async Task<(string, string)> MakeAiDecisionAsync(byte[] screenshot, string criteria)
        {
            if (string.IsNullOrEmpty(SelectedAiModel))
            {
                return ("error", "No AI model selected");
            }

            return await _ollamaService.MakeDecisionAsync(SelectedAiModel, screenshot, criteria);
        }

        private void NavigateToInputLog()
        {
            CurrentViewModel = new InputLogViewModel(this);
        }

        #endregion
    }
}