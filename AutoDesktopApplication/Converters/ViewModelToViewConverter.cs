using System;
using System.Globalization;
using AutoDesktopApplication.ViewModels;
using AutoDesktopApplication.Views;

namespace AutoDesktopApplication.Converters
{
    public class ViewModelToViewConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return null;
                
            // Map each ViewModel type to its corresponding View
            return value switch
            {
                ProjectsViewModel => new ProjectsView { BindingContext = value },
                WorkflowsViewModel => new WorkflowsView { BindingContext = value },
                SettingsViewModel => new SettingsView { BindingContext = value },
                _ => null
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // This converter doesn't support converting back
            return null;
        }
    }
}