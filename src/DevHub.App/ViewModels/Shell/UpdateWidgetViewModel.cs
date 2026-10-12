using CommunityToolkit.Mvvm.Input;
using DevHub.Services;

namespace DevHub.ViewModels.Shell;

/// <summary>
/// The status-bar note "DevHub 1.6.0 is available — Update and restart". Shown only when the update
/// check (on startup, or Check for Updates) found a newer version; nothing installs until it's clicked.
/// </summary>
public sealed partial class UpdateWidgetViewModel(UpdateService updates) : StatusBarWidgetViewModelBase
{
    public override int Order => -100; // leftmost: it's news

    public UpdateService Updates { get; } = updates;

    [RelayCommand]
    private Task UpdateAsync() => Updates.UpdateAndRestartAsync();
}
