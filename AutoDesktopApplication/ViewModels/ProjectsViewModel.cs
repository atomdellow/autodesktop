using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoDesktopApplication.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace AutoDesktopApplication.ViewModels
{
    /// <summary>
    /// ViewModel for the projects list view
    /// </summary>
    public class ProjectsViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly AppDbContext _dbContext;
        
        private string _newProjectName = string.Empty;
        private Project _selectedProject = null!;

        public ProjectsViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _dbContext = mainViewModel.GetDbContext();
            
            // Initialize commands
            CreateProjectCommand = new AsyncRelayCommand(CreateProjectAsync, CanCreateProject);
            DeleteProjectCommand = new AsyncRelayCommand(DeleteProjectAsync, CanDeleteProject);
            OpenProjectCommand = new RelayCommand<Project>(OpenProject);
            RefreshCommand = new AsyncRelayCommand(RefreshProjectsAsync);
            
            // Load projects on startup
            _ = RefreshProjectsAsync();
        }
        
        #region Properties

        public ObservableCollection<Project> Projects => _mainViewModel.Projects;

        public string NewProjectName
        {
            get => _newProjectName;
            set
            {
                if (SetProperty(ref _newProjectName, value))
                {
                    ((AsyncRelayCommand)CreateProjectCommand).NotifyCanExecuteChanged();
                }
            }
        }

        public Project SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (SetProperty(ref _selectedProject, value))
                {
                    _mainViewModel.SelectedProject = value;
                    ((AsyncRelayCommand)DeleteProjectCommand).NotifyCanExecuteChanged();
                }
            }
        }
        
        public string Title => "Projects";

        #endregion

        #region Commands
        
        public ICommand CreateProjectCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region Methods
        
        private async Task RefreshProjectsAsync()
        {
            await _mainViewModel.LoadProjectsAsync();
        }

        private async Task CreateProjectAsync()
        {
            if (string.IsNullOrWhiteSpace(NewProjectName))
                return;

            // Fix: Cast to AsyncRelayCommand to use ExecuteAsync
            await ((AsyncRelayCommand<string>)_mainViewModel.CreateProjectCommand).ExecuteAsync(NewProjectName);
            NewProjectName = string.Empty;
        }

        private bool CanCreateProject()
        {
            return !string.IsNullOrWhiteSpace(NewProjectName);
        }

        private async Task DeleteProjectAsync()
        {
            if (SelectedProject == null)
                return;

            // Fix: Cast to AsyncRelayCommand to use ExecuteAsync
            await ((AsyncRelayCommand<Project>)_mainViewModel.DeleteProjectCommand).ExecuteAsync(SelectedProject);
        }

        private bool CanDeleteProject()
        {
            return SelectedProject != null;
        }

        private void OpenProject(Project? project) // CS8622: Made project parameter nullable
        {
            if (project == null)
                return;
                
            _mainViewModel.NavigateToWorkflowsCommand.Execute(project);
        }

        #endregion
    }
}