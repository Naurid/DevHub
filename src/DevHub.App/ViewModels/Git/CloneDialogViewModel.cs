using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Sdk.Core.Formatting;
using DevHub.Git.Core;
using DevHub.GitHub.Core;
using DevHub.Sdk.Core.Search;
using DevHub.Services;
using DevHub.ViewModels.Dialogs;

namespace DevHub.ViewModels.Git;

/// <summary>
/// Clone (JetBrains' Get from VCS): pick one of your GitHub repositories from a searchable list,
/// or type any URL; choose the parent folder and the folder name. OK only checks the input; the
/// caller clones (in the status bar, with progress and Cancel).
/// </summary>
public sealed partial class CloneDialogViewModel : DialogViewModelBase
{
    private readonly GitHubSession? _session;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<GitHubRepositoryRowViewModel> _allRepositories = [];
    private CancellationTokenSource? _loadCts;

    /// <param name="session">The GitHub account; null when the GitHub plugin is off (then only a URL can be cloned).</param>
    /// <param name="folder">Where the clone goes by default (a scan folder).</param>
    public CloneDialogViewModel(GitHubSession? session, IDialogService dialogs, string folder)
    {
        _session = session;
        _dialogs = dialogs;
        _folder = folder;
        _ = LoadAccountsAsync();
    }

    public override string Title => "Clone Repository";
    public override string OkText => "Clone";
    public override double Width => 640;
    public override double Height => 560;
    public override double MinHeight => 420;

    // ---- Source -------------------------------------------------------------------------------

    /// <summary>From the GitHub list (else from <see cref="Url"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFromUrl))]
    private bool _isFromGitHub = true;

    public bool IsFromUrl
    {
        get => !IsFromGitHub;
        set => IsFromGitHub = !value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccounts), nameof(HasSeveralAccounts))]
    private IReadOnlyList<string> _accounts = [];

    public bool HasAccounts => Accounts.Count > 0;
    public bool HasSeveralAccounts => Accounts.Count > 1;

    [ObservableProperty] private string? _account;

    [ObservableProperty] private IReadOnlyList<GitHubRepositoryRowViewModel> _repositories = [];

    [ObservableProperty] private string _filter = "";

    [ObservableProperty] private GitHubRepositoryRowViewModel? _selectedRepository;

    /// <summary>"Loading your repositories…", "No repositories match.", a GitHub error, or null.</summary>
    [ObservableProperty] private string? _listMessage = "Loading…";

    [ObservableProperty] private string _url = "";

    // ---- Target -------------------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetText))]
    private string _folder;

    /// <summary>The new folder's name (the repository's name unless changed).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetText))]
    private string _folderName = "";

    public string TargetPath => Path.Combine(Folder, FolderName.Trim());
    public string TargetText => FolderName.Trim().Length == 0 ? "" : "Clones into " + TargetPath;

    [ObservableProperty] private string? _error;

    /// <summary>What to clone (set when OK succeeded).</summary>
    public string CloneUrl { get; private set; } = "";

    // ---- Loading ------------------------------------------------------------------------------

    private async Task LoadAccountsAsync()
    {
        if (_session is null)
        {
            ListMessage = "The GitHub plugin is switched off (Settings → Plugins): clone from a URL.";
            IsFromGitHub = false;
            return;
        }
        Accounts = await _session.ListAccountsAsync();
        if (Accounts.Count == 0)
        {
            ListMessage = _session.IsAvailable
                ? "Not signed in to GitHub: Sign In to see your repositories, or clone from a URL."
                : "Git is not installed.";
            IsFromGitHub = false;
            return;
        }
        Account = Accounts[0]; // loads the list
    }

    partial void OnAccountChanged(string? value) => _ = LoadRepositoriesAsync(value);

    private async Task LoadRepositoriesAsync(string? account)
    {
        _loadCts?.Cancel();
        _allRepositories = [];
        Repositories = [];
        if (account is null || _session?.GitHub is null)
            return;

        var cts = _loadCts = new CancellationTokenSource();
        ListMessage = "Loading your repositories…";
        try
        {
            var repositories = await _session.GitHub.ListRepositoriesAsync(account, cts.Token);
            if (cts.IsCancellationRequested)
                return;
            var now = DateTimeOffset.Now;
            _allRepositories = repositories.Select(r => new GitHubRepositoryRowViewModel(r, now)).ToList();
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ListMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (_session is not null && await _session.SignInAsync())
        {
            IsFromGitHub = true;
            await LoadAccountsAsync();
        }
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var text = Filter.Trim();
        Repositories = text.Length == 0
            ? _allRepositories
            : _allRepositories
                .Select(r => (Row: r, Match: FuzzyMatcher.Match(text, r.FullName)))
                .Where(x => x.Match is not null)
                .OrderByDescending(x => x.Match!.Score)
                .Select(x => x.Row)
                .ToList();
        ListMessage = Repositories.Count > 0 ? null
            : _allRepositories.Count == 0 ? "No repositories on this account yet."
            : "No repository matches.";
        if (SelectedRepository is null || !Repositories.Contains(SelectedRepository))
            SelectedRepository = Repositories.FirstOrDefault();
    }

    // The folder name follows the source until the user types their own.
    partial void OnSelectedRepositoryChanged(GitHubRepositoryRowViewModel? value)
    {
        if (IsFromGitHub && value is not null)
            FolderName = value.Name;
    }

    partial void OnUrlChanged(string value)
    {
        if (!IsFromGitHub)
            FolderName = GitCloner.FolderNameFor(value) ?? "";
    }

    partial void OnIsFromGitHubChanged(bool value) =>
        FolderName = value ? SelectedRepository?.Name ?? "" : GitCloner.FolderNameFor(Url) ?? "";

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _dialogs.PickFolderAsync("Clone into which folder?") is { } folder)
            Folder = folder;
    }

    protected override Task<bool> ApplyChangesAsync()
    {
        Error = null;
        CloneUrl = IsFromGitHub ? SelectedRepository?.Repository.CloneUrl ?? "" : Url.Trim();
        var name = FolderName.Trim();
        if (CloneUrl.Length == 0)
            Error = IsFromGitHub ? "Select a repository." : "Enter the repository's URL.";
        else if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            Error = "Enter a valid folder name.";
        else if (!Directory.Exists(Folder))
            Error = $"The folder {Folder} doesn't exist.";
        else if (Directory.Exists(TargetPath) && Directory.EnumerateFileSystemEntries(TargetPath).Any())
            Error = $"{TargetPath} already exists and isn't empty: pick another folder name.";
        return Task.FromResult(Error is null);
    }
}

/// <summary>A repository in the Clone list: name, private badge, description, last push.</summary>
public sealed class GitHubRepositoryRowViewModel(GitHubRepository repository, DateTimeOffset now)
{
    public GitHubRepository Repository { get; } = repository;
    public string Name => Repository.Name;
    public string FullName => Repository.FullName;
    public string? Description => Repository.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Repository.Description);
    public bool IsPrivate => Repository.IsPrivate;
    public string Updated { get; } = repository.PushedAt is { } at ? RelativeTime.Format(at, now) : "";

    public override string ToString() => IsPrivate ? $"{FullName} (private)" : FullName;
}
