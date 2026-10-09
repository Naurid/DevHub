using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DevHub.ViewModels;

namespace DevHub;

/// <summary>
/// Resolves a View for a given ViewModel by naming convention:
/// <c>DevHub.ViewModels.Pages.TemplatesViewModel</c> → <c>DevHub.Views.Pages.TemplatesView</c>.
/// Avalonia asks this template to build the control whenever a ViewModel is set as content.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is null)
            return new TextBlock { Text = "null" };

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is null)
            return new TextBlock { Text = $"View not found: {name}" };

        return (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
