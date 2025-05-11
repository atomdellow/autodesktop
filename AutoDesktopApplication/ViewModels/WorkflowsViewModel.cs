using System;
using System.Collections.ObjectModel;
using System.Diagnostics; // Adding this for Debug.WriteLine
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoDesktopApplication.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel;
using Newtonsoft.Json;

namespace AutoDesktopApplication.ViewModels
{
    /// <summary>
    /// ViewModel for managing workflows within a project
    /// </summary>
    public class WorkflowsViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Project _project;
        private readonly AppDbContext _dbContext;

        // Initialize non-nullable fields with default values or use required modifier
        private Workflow _selectedWorkflow = default!;
        private string _newWorkflowName = string.Empty;
        private ObservableCollection<Workflow> _workflows = new();
        private ObservableCollection<TaskBot> _tasks = new();
        private TaskBot _selectedTask = default!;
        private bool _isRunningWorkflow;
        private string _workflowProgress = string.Empty;
        private int _workflowProgressValue;
        private int _workflowProgressMax = 100;

        public WorkflowsViewModel(MainViewModel mainViewModel, Project project)
        {
            _mainViewModel = mainViewModel;
            _project = project;
            _dbContext = mainViewModel.GetDbContext();

            // Initialize commands
            CreateWorkflowCommand = new AsyncRelayCommand(CreateWorkflowAsync, CanCreateWorkflow);
            DeleteWorkflowCommand = new AsyncRelayCommand(DeleteWorkflowAsync, CanDeleteWorkflow);
            RunWorkflowCommand = new AsyncRelayCommand(RunWorkflowAsync, CanRunWorkflow);
            BackToProjectsCommand = new RelayCommand(BackToProjects);
            DeleteTaskCommand = new AsyncRelayCommand(DeleteTaskAsync, CanDeleteTask);
            MoveTaskUpCommand = new AsyncRelayCommand(MoveTaskUpAsync, CanMoveTaskUp);
            MoveTaskDownCommand = new AsyncRelayCommand(MoveTaskDownAsync, CanMoveTaskDown);

            // Load workflows
            _ = LoadWorkflowsAsync();
        }

        #region Properties

        public ObservableCollection<Workflow> Workflows
        {
            get => _workflows;
            set => SetProperty(ref _workflows, value);
        }

        public ObservableCollection<TaskBot> Tasks
        {
            get => _tasks;
            set => SetProperty(ref _tasks, value);
        }

        public Workflow SelectedWorkflow
        {
            get => _selectedWorkflow;
            set
            {
                if (SetProperty(ref _selectedWorkflow, value))
                {
                    _ = LoadTasksAsync();
                    ((AsyncRelayCommand)DeleteWorkflowCommand).NotifyCanExecuteChanged();
                    ((AsyncRelayCommand)RunWorkflowCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public TaskBot SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (SetProperty(ref _selectedTask, value))
                {
                    ((AsyncRelayCommand)DeleteTaskCommand).NotifyCanExecuteChanged();
                    ((AsyncRelayCommand)MoveTaskUpCommand).NotifyCanExecuteChanged();
                    ((AsyncRelayCommand)MoveTaskDownCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public string NewWorkflowName
        {
            get => _newWorkflowName;
            set
            {
                if (SetProperty(ref _newWorkflowName, value))
                {
                    ((AsyncRelayCommand)CreateWorkflowCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsRunningWorkflow
        {
            get => _isRunningWorkflow;
            set => SetProperty(ref _isRunningWorkflow, value);
        }

        public string WorkflowProgress
        {
            get => _workflowProgress;
            set => SetProperty(ref _workflowProgress, value);
        }

        public int WorkflowProgressValue
        {
            get => _workflowProgressValue;
            set => SetProperty(ref _workflowProgressValue, value);
        }

        public int WorkflowProgressMax
        {
            get => _workflowProgressMax;
            set => SetProperty(ref _workflowProgressMax, value);
        }

        public string ProjectName => _project.Name;

        #endregion

        #region Commands

        public ICommand CreateWorkflowCommand { get; }
        public ICommand DeleteWorkflowCommand { get; }
        public ICommand RunWorkflowCommand { get; }
        public ICommand BackToProjectsCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand MoveTaskUpCommand { get; }
        public ICommand MoveTaskDownCommand { get; }

        #endregion

        #region Methods

        public async Task LoadWorkflowsAsync() // Changed from private to public
        {
            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    var workflows = await _dbContext.Workflows
                        .Where(w => w.ProjectId == _project.Id)
                        .ToListAsync();

                    // We need to use dispatcher to update UI collection from background thread
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Workflows.Clear();
                        foreach (var workflow in workflows)
                        {
                            Workflows.Add(workflow);
                        }
                    });
                });
            }, "Loading workflows...");
        }

        public async Task AddAndSaveWorkflowAsync(Workflow newWorkflow)
        {
            if (newWorkflow == null)
            {
                Debug.WriteLine("WorkflowsViewModel: AddAndSaveWorkflowAsync called with null workflow.");
                _mainViewModel.GetInputLogService().LogError("Attempted to save a null workflow.");
                return;
            }

            // Ensure the workflow is associated with the current project
            newWorkflow.ProjectId = _project.Id;
            newWorkflow.Project = _project; // Explicitly set the navigation property to the tracked project entity

            _mainViewModel.GetInputLogService().LogInfo($"WorkflowsViewModel: Attempting to save workflow '{newWorkflow.Name}' with ProjectId: {newWorkflow.ProjectId} and Project Name: {newWorkflow.Project?.Name}");

            await ExecuteAsync(async () =>
            {
                try
                {
                    _dbContext.Workflows.Add(newWorkflow);
                    await _dbContext.SaveChangesAsync();

                    // Add to the observable collection to update UI
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Workflows.Add(newWorkflow);
                        SelectedWorkflow = newWorkflow; // Optionally select the new workflow
                        _mainViewModel.GetInputLogService().LogInfo($"WorkflowsViewModel: Successfully added workflow '{newWorkflow.Name}' to UI collection.");
                    });
                    _mainViewModel.GetInputLogService().LogInfo($"WorkflowsViewModel: Added and saved new workflow '{newWorkflow.Name}' to project '{_project.Name}'. TaskBots count: {newWorkflow.TaskBots?.Count ?? 0}");
                    if (newWorkflow.TaskBots != null)
                    {
                        foreach (var taskBot in newWorkflow.TaskBots)
                        {
                            _mainViewModel.GetInputLogService().LogInfo($"  - TaskBot ID: {taskBot.Id}, Description: {taskBot.Description}, ActionType: {taskBot.Type}, Delay: {taskBot.DelayBefore}, Duration: {taskBot.EstimatedDuration}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _mainViewModel.GetInputLogService().LogError($"WorkflowsViewModel: Error saving workflow '{newWorkflow.Name}': {ex.Message} - Inner: {ex.InnerException?.Message} - StackTrace: {ex.StackTrace}");
                    // Optionally, re-throw or handle more gracefully
                }

            }, "Adding and saving workflow...");
        }

        private async Task LoadTasksAsync()
        {
            if (SelectedWorkflow == null)
            {
                Tasks.Clear();
                return;
            }

            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    var tasks = await _dbContext.TaskBots
                        .Where(t => t.WorkflowId == SelectedWorkflow.Id)
                        .OrderBy(t => t.SequenceOrder)
                        .ToListAsync();

                    // Use dispatcher to update UI collection from background thread
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Tasks.Clear();
                        foreach (var task in tasks)
                        {
                            Tasks.Add(task);
                        }
                    });
                });
            }, "Loading tasks...");
        }

        private async Task CreateWorkflowAsync()
        {
            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    var workflow = new Workflow
                    {
                        Name = NewWorkflowName,
                        Description = "New workflow",
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now,
                        ProjectId = _project.Id,
                        Project = _project // Setting the required Project property
                    };

                    _dbContext.Workflows.Add(workflow);
                    await _dbContext.SaveChangesAsync();

                    // Update UI on main thread
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Workflows.Add(workflow);
                        SelectedWorkflow = workflow;
                        NewWorkflowName = string.Empty;
                    });
                });
            }, "Creating workflow...");
        }

        private bool CanCreateWorkflow()
        {
            return !string.IsNullOrWhiteSpace(NewWorkflowName);
        }

        private async Task DeleteWorkflowAsync()
        {
            if (SelectedWorkflow == null)
                return;

            Workflow workflowToDelete = SelectedWorkflow; // Capture for closure

            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    _dbContext.Workflows.Remove(workflowToDelete);
                    await _dbContext.SaveChangesAsync();

                    // Update UI on main thread
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Workflows.Remove(workflowToDelete);
                        SelectedWorkflow = null!;
                    });
                });
            }, "Deleting workflow...");
        }

        private bool CanDeleteWorkflow()
        {
            return SelectedWorkflow != null;
        }

        private async Task RunWorkflowAsync()
        {
            if (SelectedWorkflow == null)
                return;

            // Ensure workflow is loaded with its tasks
            await LoadTasksAsync();
            
            // Set up progress tracking
            IsRunningWorkflow = true;
            WorkflowProgressValue = 0;
            WorkflowProgressMax = 100;
            WorkflowProgress = "Preparing to run workflow...";
            
            // Create new workflow run record
            var workflowRun = new WorkflowRun
            {
                WorkflowId = SelectedWorkflow.Id,
                StartTime = DateTime.Now,
                Successful = false // Will update this later based on success
            };
            
            try
            {
                // Add the run record to the database but don't save yet
                _dbContext.WorkflowRuns.Add(workflowRun);
                await _dbContext.SaveChangesAsync();
                
                // Use a progress reporter that updates our progress properties
                var progress = new Progress<(int current, int total, string message)>(report =>
                {
                    var (current, total, message) = report;
                    WorkflowProgressValue = current;
                    WorkflowProgressMax = total;
                    WorkflowProgress = message;
                });
                
                // Load the workflow including all tasks from the database to ensure we have the latest data
                var workflow = await _dbContext.Workflows
                    .Include(w => w.TaskBots)
                    .FirstOrDefaultAsync(w => w.Id == SelectedWorkflow.Id);
                    
                if (workflow == null)
                {
                    WorkflowProgress = "Error: Workflow not found";
                    workflowRun.Notes = "Error: Workflow not found";
                    return;
                }
                
                // Execute the workflow with iteration and progress reporting
                await _mainViewModel.ExecuteWorkflowWithIterationAsync(workflow, progress);
                
                // Update workflow run record with success
                workflowRun.EndTime = DateTime.Now;
                workflowRun.Successful = true;
                workflowRun.Notes = "Workflow execution completed successfully";
                
                // Update workflow's last run date
                workflow.LastRunDate = workflowRun.EndTime;
                workflow.ModifiedDate = DateTime.Now;
                
                // Save changes to database
                await _dbContext.SaveChangesAsync();
                
                WorkflowProgress = "Workflow execution completed successfully";
            }
            catch (Exception ex)
            {
                WorkflowProgress = $"Error executing workflow: {ex.Message}";
                
                // Update workflow run record with failure
                workflowRun.EndTime = DateTime.Now;
                workflowRun.Successful = false;
                workflowRun.Notes = $"Error executing workflow: {ex.Message}";
                
                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                    Debug.WriteLine($"Failed to save workflow run: {dbEx.Message}");
                }
            }
            finally
            {
                IsRunningWorkflow = false;
            }
        }

        private bool CanRunWorkflow()
        {
            return SelectedWorkflow != null && !IsRunningWorkflow;
        }

        private void BackToProjects()
        {
            _mainViewModel.NavigateToProjectsCommand.Execute(null);
        }

        private async Task DeleteTaskAsync()
        {
            if (SelectedTask == null)
                return;

            TaskBot taskToDelete = SelectedTask; // Capture for closure

            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    _dbContext.TaskBots.Remove(taskToDelete);
                    
                    // Update sequence numbers for remaining tasks
                    var tasksToUpdate = Tasks
                        .Where(t => t.SequenceOrder > taskToDelete.SequenceOrder)
                        .ToList();
                    
                    foreach (var task in tasksToUpdate)
                    {
                        task.SequenceOrder--;
                    }
                    
                    await _dbContext.SaveChangesAsync();
                    
                    // Update UI on main thread
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Tasks.Remove(taskToDelete);
                        SelectedTask = null!;
                    });
                });
            }, "Deleting task...");
        }

        private bool CanDeleteTask()
        {
            return SelectedTask != null;
        }

        private async Task MoveTaskUpAsync()
        {
            if (SelectedTask == null || SelectedTask.SequenceOrder <= 0)
                return;

            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    // Find the task that's one position before the selected task
                    var previousTask = Tasks.FirstOrDefault(t => t.SequenceOrder == SelectedTask.SequenceOrder - 1);
                    if (previousTask != null)
                    {
                        // Swap sequence orders
                        previousTask.SequenceOrder++;
                        SelectedTask.SequenceOrder--;
                        
                        await _dbContext.SaveChangesAsync();
                        
                        // Update the collection to reflect the new order
                        await LoadTasksAsync();
                    }
                });
            }, "Moving task up...");
        }

        private bool CanMoveTaskUp()
        {
            return SelectedTask != null && SelectedTask.SequenceOrder > 0;
        }

        private async Task MoveTaskDownAsync()
        {
            if (SelectedTask == null || SelectedTask.SequenceOrder >= Tasks.Count - 1)
                return;

            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    // Find the task that's one position after the selected task
                    var nextTask = Tasks.FirstOrDefault(t => t.SequenceOrder == SelectedTask.SequenceOrder + 1);
                    if (nextTask != null)
                    {
                        // Swap sequence orders
                        nextTask.SequenceOrder--;
                        SelectedTask.SequenceOrder++;
                        
                        await _dbContext.SaveChangesAsync();
                        
                        // Update the collection to reflect the new order
                        await LoadTasksAsync();
                    }
                });
            }, "Moving task down...");
        }

        private bool CanMoveTaskDown()
        {
            return SelectedTask != null && Tasks.Count > 0 && SelectedTask.SequenceOrder < Tasks.Count - 1;
        }

        /// <summary>
        /// Adds recorded mouse and keyboard data as tasks to the current workflow
        /// </summary>
        public async Task AddRecordedData(MouseMovementData mouseData, KeyboardInputData keyboardData)
        {
            if (SelectedWorkflow == null || (mouseData.Actions.Count == 0 && keyboardData.Actions.Count == 0))
                return;

            // Fix: Converting async lambda to Func<Task>
            await ExecuteAsync(() => {
                return Task.Run(async () => {
                    int nextSequence = Tasks.Count > 0 ? Tasks.Max(t => t.SequenceOrder) + 1 : 0;
                    
                    // Add mouse movement task if there are mouse actions
                    if (mouseData.Actions.Count > 0)
                    {
                        var mouseTask = new TaskBot
                        {
                            Name = $"Mouse Actions ({mouseData.Actions.Count})",
                            Description = $"Recorded mouse movements with {mouseData.Actions.Count} actions",
                            Type = TaskType.MouseMovement,
                            CreatedDate = DateTime.Now,
                            ModifiedDate = DateTime.Now,
                            SequenceOrder = nextSequence++,
                            WorkflowId = SelectedWorkflow.Id,
                            InputData = JsonConvert.SerializeObject(mouseData),
                            Workflow = SelectedWorkflow // Setting the required Workflow property
                        };
                        
                        _dbContext.TaskBots.Add(mouseTask);
                    }
                    
                    // Add keyboard input task if there are keyboard actions
                    if (keyboardData.Actions.Count > 0)
                    {
                        var keyboardTask = new TaskBot
                        {
                            Name = $"Keyboard Actions ({keyboardData.Actions.Count})",
                            Description = $"Recorded keyboard inputs with {keyboardData.Actions.Count} actions",
                            Type = TaskType.KeyboardInput,
                            CreatedDate = DateTime.Now,
                            ModifiedDate = DateTime.Now,
                            SequenceOrder = nextSequence++,
                            WorkflowId = SelectedWorkflow.Id,
                            InputData = JsonConvert.SerializeObject(keyboardData),
                            Workflow = SelectedWorkflow // Setting the required Workflow property
                        };
                        
                        _dbContext.TaskBots.Add(keyboardTask);
                    }
                    
                    await _dbContext.SaveChangesAsync();
                    
                    // Refresh tasks list
                    await LoadTasksAsync();
                });
            }, "Adding recorded data...");
        }

        /// <summary>
        /// Saves a workflow run record to track execution history
        /// </summary>
        private async Task SaveWorkflowRunAsync(Workflow workflow, bool successful, string notes = "")
        {
            try
            {
                // Create the run record
                var workflowRun = new WorkflowRun
                {
                    WorkflowId = workflow.Id,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now, // For now, just use the same time
                    Successful = successful,
                    Notes = notes
                };
                
                // Add to database
                _dbContext.WorkflowRuns.Add(workflowRun);
                await _dbContext.SaveChangesAsync();
                
                // Update the workflow's last run time
                workflow.LastRunDate = workflowRun.StartTime;
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save workflow run: {ex.Message}");
            }
        }

        #endregion
    }
}