using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// Something opened as a tab in the editor area (the center of the window), like a file in
/// Rider: Templates, New Project (Generate), Import, Settings… The <see cref="DocumentManager"/>
/// owns the tab list; the ViewLocator turns the active ViewModel into its View.
/// </summary>
/// <remarks>
/// Each document is a DI singleton, so closing a tab and reopening it keeps its state
/// (e.g. a half-filled form). Only one tab per document.
/// </remarks>
public abstract partial class DocumentViewModelBase : ViewModelBase
{
    /// <summary>Stable id, saved in settings to reopen tabs next launch. Defaults to the class name minus "ViewModel".</summary>
    public virtual string Id => GetType().Name.Replace("ViewModel", "", StringComparison.Ordinal);

    /// <summary>Tab label.</summary>
    public abstract string Title { get; }

    /// <summary>Tab icon; one of <see cref="DevHub.Controls.IconKind"/>.</summary>
    public abstract string Icon { get; }

    /// <summary>True for the selected tab. Set by <see cref="DocumentManager"/> only.</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>Closes this tab (the × on the tab). Assigned by <see cref="DocumentManager"/>.</summary>
    public ICommand? CloseCommand { get; internal set; }

    /// <summary>Accessible name of the tab (screen readers announce ToString otherwise).</summary>
    public override string ToString() => Title;
}
