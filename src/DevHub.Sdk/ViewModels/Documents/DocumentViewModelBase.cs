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

    /// <summary>
    /// False (default): the editor area puts <c>Padding.Document</c> around the view (forms, pages).
    /// True: the view fills the whole area and lays out its own spacing, e.g. a repository tab
    /// whose sidebar runs edge to edge.
    /// </summary>
    public virtual bool FillsEditorArea => false;

    /// <summary>
    /// The repository this tab belongs to (a repository tab, or a diff / blame tab of one of its
    /// files), or null. Git actions use it, so they also work while a diff or blame tab is in front.
    /// </summary>
    public virtual string? RepositoryPath => null;

    /// <summary>Has unsaved / uncommitted changes: the tab shows a dot (e.g. a repository with local changes).</summary>
    [ObservableProperty] private bool _isDirty;

    /// <summary>True for the selected tab. Set by <see cref="DocumentManager"/> only.</summary>
    [ObservableProperty] private bool _isActive;

    // The generated "changed" hook can only be implemented in this class (partial methods
    // live where the property is declared), so pass it on to subclasses as a virtual method.
    partial void OnIsActiveChanged(bool value) => OnActivationChanged(value);

    /// <summary>Called when the tab becomes active (true) or inactive (false), e.g. to refresh.</summary>
    protected virtual void OnActivationChanged(bool isActive)
    {
    }

    /// <summary>Closes this tab (the × on the tab). Assigned by <see cref="DocumentManager"/>.</summary>
    public ICommand? CloseCommand { get; internal set; }

    /// <summary>
    /// Opens another document by id, e.g. a diff tab from a repository tab (same as
    /// <see cref="DocumentManager.OpenById"/>; documents can't take the manager itself, it's
    /// built from them). Assigned by <see cref="DocumentManager"/>.
    /// </summary>
    public Func<string, bool>? OpenDocument { get; internal set; }

    /// <summary>Accessible name of the tab (screen readers announce ToString otherwise).</summary>
    public override string ToString() => Title;
}
