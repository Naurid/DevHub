using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Sdk.Core.Collections;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;
using DevHub.Sdk.Core.Localization;

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

    /// <summary>The operation shown in the bar, or null. (Not named "Task": that clashes with System.Threading.Tasks.Task.)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTask))]
    private StatusTaskViewModel? _currentTask;

    public bool HasTask => CurrentTask is not null;

    /// <summary>Widgets from the loaded plugins, left of the theme / density widgets. Set once by the main window ViewModel.</summary>
    public IReadOnlyList<StatusBarWidgetViewModelBase> Widgets { get; internal set; } = [];

    public StatusBarViewModel(IThemeService theme, ISettingsStore settings)
    {
        _theme = theme;
        History = new RecentItems<StatusMessage>((settings.Load().StatusBar ?? new StatusBarSettings()).Size);
        UpdateAppearance();
        // Unsubscribed in Dispose (the DI container disposes singletons at shutdown).
        _theme.Changed += OnThemeChanged;
    }

    /// <summary>The latest messages, newest first (Settings → Appearance → Status messages kept), shown when the message is clicked.</summary>
    public RecentItems<StatusMessage> History { get; }

    public bool HasHistory => History.Count > 0;

    public void Show(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? null : message;
        // Plugins may report from a background thread; the bar and its history change on the UI thread.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(text));
            return;
        }
        Message = text;
        if (text is null)
            return;
        if (History.Count > 0 && History[0].Text == text)
            History[0] = History[0] with { At = DateTime.Now };   // the same message again: just its time
        else
            History.Push(new StatusMessage(DateTime.Now, text));
        OnPropertyChanged(nameof(HasHistory));
    }

    /// <summary>Settings → Appearance changed how many messages are kept (fewer: the oldest go now).</summary>
    public void SetHistorySize(int size) => History.Capacity = size;

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
        OnPropertyChanged(nameof(HasHistory));
    }

    public IStatusTask StartTask(string title, Action? cancel = null)
    {
        var task = new StatusTaskViewModel(title, cancel, Remove);
        _tasks.Add(task);
        CurrentTask = task;
        return task;
    }

    private void Remove(StatusTaskViewModel task)
    {
        _tasks.Remove(task);
        CurrentTask = _tasks.LastOrDefault();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateAppearance();

    private void UpdateAppearance()
    {
        ThemeLabel = _theme.IsDark ? L.T("Dark") : L.T("Light");
        DensityLabel = _theme.IsCompact ? L.T("Compact") : L.T("Normal");
    }

    public void Dispose() => _theme.Changed -= OnThemeChanged;
}

/// <summary>A message the status bar showed, with when.</summary>
public sealed record StatusMessage(DateTime At, string Text)
{
    /// <summary>"14:02".</summary>
    public string Time => At.ToString("t", System.Globalization.CultureInfo.CurrentCulture);

    public override string ToString() => $"{Time} {Text}";
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
            Step = L.T("Cancelling…");
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
