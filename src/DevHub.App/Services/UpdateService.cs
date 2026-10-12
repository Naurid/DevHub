using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using Velopack;
using Velopack.Sources;

namespace DevHub.Services;

/// <summary>
/// DevHub's own updates (docs/RELEASING.md): new versions are the GitHub Releases of
/// <see cref="RepositoryUrl"/>, made by the release workflow with Velopack. Checking never changes
/// anything; <see cref="UpdateAndRestartAsync"/> downloads (with progress in the status bar), then
/// closes DevHub the normal way (tabs saved, unsaved changes asked about) and Velopack swaps the
/// version and starts the new one. Only an installed copy updates: under <c>dotnet run</c> or the
/// portable zip, <see cref="CanUpdate"/> is false.
/// </summary>
public sealed partial class UpdateService(IStatusService status) : ObservableObject
{
    public const string RepositoryUrl = "https://github.com/Naurid/DevHub";
    public const string ReleasesUrl = RepositoryUrl + "/releases";

    // DEVHUB_UPDATE_SOURCE (a folder of vpk output, or a URL) replaces GitHub: to rehearse an update
    // locally before releasing (docs/RELEASING.md).
    private readonly UpdateManager _manager =
        Environment.GetEnvironmentVariable("DEVHUB_UPDATE_SOURCE") is { Length: > 0 } source
            ? new UpdateManager(source)
            : new UpdateManager(new GithubSource(RepositoryUrl, null, false));
    private UpdateInfo? _update;

    /// <summary>This DevHub's version: "1.4.0", or "0.0.0-dev" for a local build.</summary>
    public static string CurrentVersion { get; } =
        (typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0]; // without the "+<commit>" the SDK adds

    /// <summary>Installed with the installer (so it can update itself).</summary>
    public bool CanUpdate => _manager.IsInstalled && !_manager.IsPortable;

    /// <summary>The newer version found by the last check, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private string? _availableVersion;

    public bool HasUpdate => AvailableVersion is not null;

    [ObservableProperty] private bool _isBusy;

    /// <summary>Asks GitHub for a newer version. Returns a sentence about the result (for Check now).</summary>
    public async Task<string> CheckAsync()
    {
        if (!CanUpdate)
            return "Updates work in an installed DevHub (from the installer); this one is a development build.";
        if (IsBusy)
            return "Already checking.";
        IsBusy = true;
        try
        {
            _update = await _manager.CheckForUpdatesAsync();
            AvailableVersion = _update?.TargetFullRelease.Version.ToString();
            return AvailableVersion is { } version ? $"DevHub {version} is available." : $"DevHub {CurrentVersion} is the latest version.";
        }
        catch (Exception ex)
        {
            return "Couldn't check for updates: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Downloads the new version, then closes DevHub normally; Velopack applies it and starts the new one.</summary>
    public async Task UpdateAndRestartAsync()
    {
        if (_update is not { } update || IsBusy)
            return;
        IsBusy = true;
        using var task = status.StartTask($"Downloading DevHub {AvailableVersion}…");
        try
        {
            await _manager.DownloadUpdatesAsync(update, percent => task.Report("Downloading", percent));
        }
        catch (Exception ex)
        {
            status.Show("Couldn't download the update: " + ex.Message);
            IsBusy = false;
            return;
        }
        // Velopack waits for this process to end, applies the update and starts DevHub again.
        _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: false, restart: true, []);
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    /// <summary>Opens the release notes (the GitHub Releases page) in the browser.</summary>
    public static void OpenReleaseNotes() => Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true })?.Dispose();
}
