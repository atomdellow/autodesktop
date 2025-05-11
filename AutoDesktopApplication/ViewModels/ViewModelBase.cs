using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;

namespace AutoDesktopApplication.ViewModels
{
    /// <summary>
    /// Base ViewModel class that all ViewModels should inherit from
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        private bool _isLoading;
        private string _loadingMessage = string.Empty;
        private string _errorMessage = string.Empty;

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Safely executes a task and handles exceptions
        /// </summary>
        protected async Task ExecuteAsync(Task task, string? loadingMessage = null)
        {
            try
            {
                IsLoading = true;
                LoadingMessage = loadingMessage ?? string.Empty;
                ErrorMessage = string.Empty;

                await task;
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
            }
        }

        /// <summary>
        /// Safely executes a Func that returns a Task and handles exceptions
        /// </summary>
        protected async Task ExecuteAsync(Func<Task> taskFunc, string? loadingMessage = null)
        {
            try
            {
                IsLoading = true;
                LoadingMessage = loadingMessage ?? string.Empty;
                ErrorMessage = string.Empty;

                await taskFunc();
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
            }
        }

        /// <summary>
        /// Safely executes a task with a return value and handles exceptions
        /// </summary>
        protected async Task<T?> ExecuteAsync<T>(Task<T> task, string? loadingMessage = null)
        {
            try
            {
                IsLoading = true;
                LoadingMessage = loadingMessage ?? string.Empty;
                ErrorMessage = string.Empty;

                return await task;
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
                return default;
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
            }
        }

        /// <summary>
        /// Safely executes a Func that returns a Task with a return value and handles exceptions
        /// </summary>
        protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> taskFunc, string? loadingMessage = null)
        {
            try
            {
                IsLoading = true;
                LoadingMessage = loadingMessage ?? string.Empty;
                ErrorMessage = string.Empty;

                return await taskFunc();
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
                return default;
            }
            finally
            {
                IsLoading = false;
                LoadingMessage = string.Empty;
            }
        }
    }
}