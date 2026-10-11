using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DevHub.ViewModels;

namespace DevHub;

/// <summary>
/// Resolves a View for a given ViewModel by naming convention:
/// <c>DevHub.ViewModels.Documents.TemplatesViewModel</c> → <c>DevHub.Views.Documents.TemplatesView</c>.
/// Avalonia asks this template to build the control whenever a ViewModel is set as content.
/// The View is looked up in the ViewModel's own assembly, so a plugin (or DevHub.Sdk, e.g. the
/// confirm dialog) keeps each View next to its ViewModel.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is null)
            return new TextBlock { Text = "null" };

        var vmType = param.GetType();
        var name = vmType.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        // Type.GetType(name) would only search this assembly (and the core library).
        var type = vmType.Assembly.GetType(name);

        if (type is null)
            return new TextBlock { Text = $"View not found: {name}" };

        return (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
