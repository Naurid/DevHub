using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevHub.ViewModels.ToolWindows;

/// <summary>Where a tool window lives. Fixed per tool window for now (no drag-to-move).</summary>
public enum ToolWindowSlot
{
    Left,
    Right,
    Bottom,
}

/// <summary>
/// A panel docked to an edge of the main window (like Rider's Project or Git windows),
/// shown or hidden by clicking its icon in a stripe. The ViewLocator turns the ViewModel into
/// its View as usual; the View normally wraps itself in a <c>ToolWindowFrame</c>.
/// </summary>
public abstract partial class ToolWindowViewModelBase : ViewModelBase
{
    /// <summary>Stable id, saved in settings ("repositories"). Don't rename casually.</summary>
    public abstract string Id { get; }

    /// <summary>Name shown in the stripe tooltip and the frame header.</summary>
    public abstract string Title { get; }

    /// <summary>Stripe icon; one of <c>DevHub.Controls.IconKind</c>.</summary>
    public abstract string IconKind { get; }

    /// <summary>Which edge it docks to.</summary>
    public abstract ToolWindowSlot Slot { get; }

    /// <summary>
    /// Position in the stripes, the View menu and the Alt+1…9 shortcuts (lowest first), so they
    /// don't depend on which plugins load or in what order. The Projects tool window is 0 (Alt+1).
    /// </summary>
    public virtual int Order => 100;

    /// <summary>Position of its show/hide action in the Welcome view; null = not there.</summary>
    public virtual int? WelcomeOrder => null;

    /// <summary>Whether it's currently shown. Set by <c>ToolWindowManager</c> (the shell) only.</summary>
    [ObservableProperty] private bool _isOpen;

    /// <summary>Shows/hides this tool window. Assigned by <c>ToolWindowManager</c> (the shell).</summary>
    public ICommand? ToggleCommand { get; internal set; }

    /// <summary>Stripe tooltip, e.g. "Repositories (Alt+1)". The shortcut part is filled in by the action provider.</summary>
    public string ToolTip => Shortcut is null ? Title : $"{Title} ({Shortcut})";

    /// <summary>Keyboard shortcut that toggles it, if any (display only).</summary>
    public string? Shortcut { get; internal set; }

    /// <summary>Accessible name (screen readers announce ToString for items without one).</summary>
    public override string ToString() => Title;
}
