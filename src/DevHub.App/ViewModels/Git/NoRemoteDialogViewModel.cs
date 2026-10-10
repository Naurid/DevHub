using CommunityToolkit.Mvvm.Input;
using DevHub.ViewModels.Dialogs;

namespace DevHub.ViewModels.Git;

/// <summary>What the user picked in <see cref="NoRemoteDialogViewModel"/>.</summary>
public enum NoRemoteChoice
{
    /// <summary>Create a repository on GitHub and push there.</summary>
    PublishToGitHub,

    /// <summary>Add an existing remote by URL and push there.</summary>
    AddRemote,
}

/// <summary>
/// Push with no remote at all: instead of a dead end, offer the two ways forward. Publish to
/// GitHub is the main button (the usual case); adding an existing remote by URL is the other.
/// </summary>
public sealed partial class NoRemoteDialogViewModel(string branch) : DialogViewModelBase
{
    public override string Title => "No Remote to Push To";
    public override string OkText => "Publish to GitHub…";
    public override bool FitsContent => true;
    public override double Width => 460;
    public override double MinWidth => 380;
    public override double MinHeight => 0;

    public string Message { get; } =
        $"This repository has no remote yet, so there's nowhere to push {branch}. Create a repository on your GitHub " +
        "account and push there, or add a remote that already exists (GitHub, GitLab, a server, a folder) by its URL.";

    /// <summary>The choice; Publish unless "Add a remote by URL…" was clicked.</summary>
    public NoRemoteChoice Choice { get; private set; } = NoRemoteChoice.PublishToGitHub;

    /// <summary>The secondary option: picks it and closes the dialog with OK.</summary>
    [RelayCommand]
    private Task AddRemoteAsync()
    {
        Choice = NoRemoteChoice.AddRemote;
        return OkCommand.ExecuteAsync(null);
    }
}
