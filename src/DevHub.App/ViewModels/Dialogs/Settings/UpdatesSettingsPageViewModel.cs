using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.Sdk.Core.Localization;

namespace DevHub.ViewModels.Dialogs.Settings;

/// <summary>Settings → Updates: this version, Check now / Update and restart, check on startup, release notes.</summary>
public sealed partial class UpdatesSettingsPageViewModel : SettingsPageViewModelBase
{
    private readonly ISettingsStore _store;

    public UpdatesSettingsPageViewModel(ISettingsStore store, UpdateService updates)
    {
        _store = store;
        Updates = updates;
        _checkOnStartup = store.Load().Updates?.CheckOnStartup ?? true;
    }

    public override string Title => L.T("Updates");
    public override int Order => 950;
    public override string Keywords => L.T("update version upgrade release new check download install about");

    public UpdateService Updates { get; }
    public string Version => UpdateService.CurrentVersion;
    public string NotInstalledNote => Updates.CanUpdate ? "" : L.T("This is a development build: updates work in a DevHub installed with the installer.");
    public bool ShowNotInstalledNote => !Updates.CanUpdate;

    [ObservableProperty] private bool _checkOnStartup;
    [ObservableProperty] private string? _result;

    partial void OnCheckOnStartupChanged(bool value) => MarkModified();

    [RelayCommand]
    private async Task CheckNowAsync() => Result = await Updates.CheckAsync();

    [RelayCommand]
    private Task UpdateAsync() => Updates.UpdateAndRestartAsync();

    [RelayCommand]
    private static void ReleaseNotes() => UpdateService.OpenReleaseNotes();

    public override void Apply()
    {
        _store.Update(s => s.Updates = new UpdateSettings { CheckOnStartup = CheckOnStartup });
        IsModified = false;
    }
}
