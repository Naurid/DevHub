using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Sdk.Core.Settings;

namespace DevHub.ViewModels.ToolWindows;

/// <summary>
/// Owns all tool windows and the three slots around the center area: which one is open in
/// each slot (at most one per slot), how big each slot is, and saving/restoring that layout.
/// The main window binds its stripes and slots to this.
/// </summary>
/// <remarks>
/// Tool windows are registered in DI as <c>ToolWindowViewModelBase</c> (several registrations,
/// received here as an <c>IEnumerable</c>, like the action providers).
/// </remarks>
public partial class ToolWindowManager : ViewModelBase
{
    private readonly ISettingsStore _store;

    /// <summary>Every tool window, by <see cref="ToolWindowViewModelBase.Order"/> (then registration order).</summary>
    public IReadOnlyList<ToolWindowViewModelBase> All { get; }

    /// <summary>Stripe contents: left-stripe top, left-stripe bottom (bottom slot), right stripe.</summary>
    public IReadOnlyList<ToolWindowViewModelBase> LeftStripe { get; }
    public IReadOnlyList<ToolWindowViewModelBase> BottomStripe { get; }
    public IReadOnlyList<ToolWindowViewModelBase> RightStripe { get; }

    /// <summary>True when there is at least one right-docked tool window (else the right stripe is hidden).</summary>
    public bool HasRightStripe => RightStripe.Count > 0;

    // What's open in each slot (null = closed).
    [ObservableProperty] private ToolWindowViewModelBase? _left;
    [ObservableProperty] private ToolWindowViewModelBase? _right;
    [ObservableProperty] private ToolWindowViewModelBase? _bottom;

    // Slot sizes, two-way bound to the SplitterThumbs.
    [ObservableProperty] private double _leftWidth = ToolWindowLayout.DefaultSideWidth;
    [ObservableProperty] private double _rightWidth = ToolWindowLayout.DefaultSideWidth;
    [ObservableProperty] private double _bottomHeight = ToolWindowLayout.DefaultBottomHeight;

    public ToolWindowManager(IEnumerable<ToolWindowViewModelBase> toolWindows, ISettingsStore store)
    {
        _store = store;
        All = toolWindows.OrderBy(t => t.Order).ToList();

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var toolWindow in All)
        {
            if (!ids.Add(toolWindow.Id))
                throw new InvalidOperationException($"Duplicate tool window id '{toolWindow.Id}'.");

            var captured = toolWindow;
            toolWindow.ToggleCommand = new RelayCommand(() => Toggle(captured));
        }

        LeftStripe = All.Where(t => t.Slot == ToolWindowSlot.Left).ToList();
        BottomStripe = All.Where(t => t.Slot == ToolWindowSlot.Bottom).ToList();
        RightStripe = All.Where(t => t.Slot == ToolWindowSlot.Right).ToList();

        Restore(_store.Load().ToolWindows);
    }

    /// <summary>Opens the tool window in its slot (replacing whatever was there), or closes it if it's already open.</summary>
    public void Toggle(ToolWindowViewModelBase toolWindow)
    {
        var isOpenNow = GetSlot(toolWindow.Slot) == toolWindow;
        SetSlot(toolWindow.Slot, isOpenNow ? null : toolWindow);
    }

    /// <summary>Closes every slot (Ctrl+Shift+F12, like JetBrains' "Hide All Tool Windows").</summary>
    public void HideAll()
    {
        Left = null;
        Right = null;
        Bottom = null;
    }

    /// <summary>Saves what's open and the sizes. Called when the main window closes.</summary>
    public void Save()
    {
        var layout = new ToolWindowLayout
        {
            OpenLeft = Left?.Id,
            OpenRight = Right?.Id,
            OpenBottom = Bottom?.Id,
            LeftWidth = LeftWidth,
            RightWidth = RightWidth,
            BottomHeight = BottomHeight,
        };
        _store.Update(s => s.ToolWindows = layout);
    }

    private void Restore(ToolWindowLayout? layout)
    {
        // First launch: show the first left tool window (Repositories), like Rider shows Project.
        layout ??= new ToolWindowLayout { OpenLeft = LeftStripe.FirstOrDefault()?.Id };

        LeftWidth = layout.LeftWidth;
        RightWidth = layout.RightWidth;
        BottomHeight = layout.BottomHeight;

        // Unknown ids (a tool window that no longer exists) or ids in the wrong slot are ignored.
        Left = Find(layout.OpenLeft, ToolWindowSlot.Left);
        Right = Find(layout.OpenRight, ToolWindowSlot.Right);
        Bottom = Find(layout.OpenBottom, ToolWindowSlot.Bottom);
    }

    private ToolWindowViewModelBase? Find(string? id, ToolWindowSlot slot) =>
        All.FirstOrDefault(t => t.Id == id && t.Slot == slot);

    private ToolWindowViewModelBase? GetSlot(ToolWindowSlot slot) => slot switch
    {
        ToolWindowSlot.Left => Left,
        ToolWindowSlot.Right => Right,
        _ => Bottom,
    };

    private void SetSlot(ToolWindowSlot slot, ToolWindowViewModelBase? toolWindow)
    {
        switch (slot)
        {
            case ToolWindowSlot.Left: Left = toolWindow; break;
            case ToolWindowSlot.Right: Right = toolWindow; break;
            default: Bottom = toolWindow; break;
        }
    }

    // Keep each tool window's IsOpen flag (stripe button highlight) in sync with the slots.
    // [ObservableProperty] generates these "changed" hooks with the old and new values.
    partial void OnLeftChanged(ToolWindowViewModelBase? oldValue, ToolWindowViewModelBase? newValue) => Swap(oldValue, newValue);
    partial void OnRightChanged(ToolWindowViewModelBase? oldValue, ToolWindowViewModelBase? newValue) => Swap(oldValue, newValue);
    partial void OnBottomChanged(ToolWindowViewModelBase? oldValue, ToolWindowViewModelBase? newValue) => Swap(oldValue, newValue);

    private static void Swap(ToolWindowViewModelBase? closed, ToolWindowViewModelBase? opened)
    {
        if (closed is not null)
            closed.IsOpen = false;
        if (opened is not null)
            opened.IsOpen = true;
    }
}
