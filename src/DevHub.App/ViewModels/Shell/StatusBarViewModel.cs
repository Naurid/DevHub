using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Services;

namespace DevHub.ViewModels.Shell;

/// <summary>
/// The status bar at the bottom of the window: a message on the left (set through
/// <see cref="IStatusService"/>), the running operation with progress and Cancel (if any), and
/// small widgets on the right showing the current theme and density. Clicking a widget runs
/// the matching action (wired in the View).
/// </summary>
/// <remarks>
/// It is also the <see cref="IStatusService"/> implementation: one singleton, registered
/// under both types, so whatever a page shows ends up in <see cref="Message"/>.
/// It must not depend on the ActionRegistry: the registry's providers depend on
/// MainWindowViewModel, which depends on this (that would be a DI cycle).
/// </remarks>
public partial class StatusBarViewModel : ViewModelBase, IStatusService, IDisposable
{
    private readonly IThemeService _theme;

    /// <summary>Running operations, oldest first. The bar shows the newest (two repositories can push at once).</summary>
    private readonly ObservableCollection<StatusTaskViewModel> _tasks = new();

    [ObservableProperty] private string? _message;
    [ObservableProperty] private string _themeLabel = "";
    [ObservableProperty] private string _densityLabel = "";

    /// <summary>The operation shown in the bar, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTask))]
    private StatusTaskViewModel? _task;

    public bool HasTask => Task is not null;

    public StatusBarViewModel(IThemeService theme)
    {
        _theme = theme;
        UpdateAppearance();
        // Unsubscribed in Dispose (the DI container disposes singletons at shutdown).
        _theme.Changed += OnThemeChanged;
    }

    public void Show(string? message) => Message = string.IsNullOrWhiteSpace(message) ? null : message;

    public IStatusTask StartTask(string title, Action? cancel = null)
    {
        var task = new StatusTaskViewModel(title, cancel, Remove);
        _tasks.Add(task);
        Task = task;
        return task;
    }

    private void Remove(StatusTaskViewModel task)
    {
        _tasks.Remove(task);
        Task = _tasks.LastOrDefault();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateAppearance();

    private void UpdateAppearance()
    {
        ThemeLabel = _theme.IsDark ? "Dark" : "Light";
        DensityLabel = _theme.IsCompact ? "Compact" : "Normal";
    }

    public void Dispose() => _theme.Changed -= OnThemeChanged;
}

/// <summary>A running operation in the status bar: title, current step, progress, Cancel.</summary>
public sealed partial class StatusTaskViewModel : ObservableObject, IStatusTask
{
    private readonly Action<StatusTaskViewModel> _remove;
    private bool _disposed;

    public StatusTaskViewModel(string title, Action? cancel, Action<StatusTaskViewModel> remove)
    {
        Title = title;
        _remove = remove;
        CancelCommand = new RelayCommand(() =>
        {
            Step = "Cancelling…";
            cancel?.Invoke();
        }, () => cancel is not null);
    }

    public string Title { get; }

    /// <summary>"Receiving objects 45%", or null before git reports anything.</summary>
    [ObservableProperty] private string? _step;

    /// <summary>0–100.</summary>
    [ObservableProperty] private double _percent;

    /// <summary>No percentage known: the bar animates instead.</summary>
    [ObservableProperty] private bool _isIndeterminate = true;

    public RelayCommand CancelCommand { get; }
    public bool CanCancel => CancelCommand.CanExecute(null);

    // Git reports from its reader thread; the bar's properties change on the UI thread.
    public void Report(string? step, double? percent) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
                return;
            Step = step is null ? null : percent is { } p ? $"{step} {p:0}%" : step;
            IsIndeterminate = percent is null;
            Percent = percent ?? 0;
        });

    public void Dispose() =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
                return;
            _disposed = true;
            _remove(this);
        });

    public override string ToString() => Step is null ? Title : $"{Title} {Step}";
}
