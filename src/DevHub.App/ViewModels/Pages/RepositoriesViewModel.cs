using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Core.Projects;
using DevHub.Core.Repositories;
using DevHub.Services;
using DevHub.Controls;

namespace DevHub.ViewModels.Pages;

/// <summary>
/// Lets the user designate folders and scan them for git repositories. All the file-system
/// work lives in <see cref="IRepositoryScanner"/> (Core); this just drives it and exposes the
/// results for the View to bind to.
/// </summary>
public partial class RepositoriesViewModel : PageViewModelBase
{
    private readonly IRepositoryScanner _scanner;
    private readonly IDialogService _dialogs;
    private readonly IProjectLauncher _launcher;

    public override string Title => "Repositories";
    public override string Icon => IconKind.Folder;

    /// <summary>Folders the user has chosen to scan (in-memory for now).</summary>
    public ObservableCollection<string> ScanFolders { get; } = new();

    /// <summary>Repositories found by the last scan.</summary>
    public ObservableCollection<GitRepository> Repositories { get; } = new();

    /// <summary>True when at least one folder is designated (drives the empty-state hint).</summary>
    public bool HasFolders => ScanFolders.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private string? _statusMessage;

    public RepositoriesViewModel(IRepositoryScanner scanner, IDialogService dialogs, IProjectLauncher launcher)
    {
        _scanner = scanner;
        _dialogs = dialogs;
        _launcher = launcher;

        // Adding/removing a folder changes whether Scan can run and the empty-state hint.
        ScanFolders.CollectionChanged += (_, _) =>
        {
            ScanCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasFolders));
        };
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Choose a folder to scan");
        if (!string.IsNullOrEmpty(folder) &&
            !ScanFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            ScanFolders.Add(folder);
        }
    }

    [RelayCommand]
    private void RemoveFolder(string folder) => ScanFolders.Remove(folder);

    private bool CanScan() => !IsScanning && ScanFolders.Count > 0;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning…";
        try
        {
            var results = await _scanner.ScanAsync(ScanFolders.ToList());

            Repositories.Clear();
            foreach (var repo in results)
                Repositories.Add(repo);

            StatusMessage = Repositories.Count switch
            {
                0 => "No git repositories found.",
                1 => "Found 1 repository.",
                _ => $"Found {Repositories.Count} repositories."
            };
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void Open(GitRepository repo)
    {
        try
        {
            _launcher.Open(repo);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't open {repo.Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Reveal(GitRepository repo)
    {
        try
        {
            _launcher.RevealInFileManager(repo);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't reveal {repo.Name}: {ex.Message}";
        }
    }
}
