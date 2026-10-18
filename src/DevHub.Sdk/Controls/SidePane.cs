using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Controls;

/// <summary>
/// A side panel next to the main content that adapts to the room it gets:
/// <c>&lt;c:SidePane PaneWidth="{Binding Layout.SidebarWidth}" MinContentWidth="420" PaneTitle="Branches"&gt;
/// &lt;c:SidePane.Pane&gt;…&lt;/c:SidePane.Pane&gt; …content… &lt;/c:SidePane&gt;</c>.
/// </summary>
/// <remarks>
/// Wide enough (pane at its minimum + content at its minimum fit): the pane is docked with a splitter, its width
/// two-way bound to <see cref="PaneWidth"/> and kept so the content never gets less than <see cref="MinContentWidth"/>.
/// Narrower: the pane folds into a rail (an icon button with <see cref="PaneTitle"/> as tooltip) and opens over the
/// content when asked; it closes on Escape, a click on the content, or a click that selects something inside it.
/// Like a responsive web sidebar that becomes a drawer: the content keeps a readable width instead of being
/// squeezed into one character per line.
/// </remarks>
public class SidePane : Control
{
    public static readonly StyledProperty<Control?> PaneProperty =
        AvaloniaProperty.Register<SidePane, Control?>(nameof(Pane));

    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<SidePane, Control?>(nameof(Content));

    public static readonly StyledProperty<double> PaneWidthProperty =
        AvaloniaProperty.Register<SidePane, double>(nameof(PaneWidth), 260, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinPaneWidthProperty =
        AvaloniaProperty.Register<SidePane, double>(nameof(MinPaneWidth), 160);

    public static readonly StyledProperty<double> MaxPaneFractionProperty =
        AvaloniaProperty.Register<SidePane, double>(nameof(MaxPaneFraction), 0.45);

    public static readonly StyledProperty<double> MinContentWidthProperty =
        AvaloniaProperty.Register<SidePane, double>(nameof(MinContentWidth), 400);

    public static readonly StyledProperty<bool> IsPaneOpenProperty =
        AvaloniaProperty.Register<SidePane, bool>(nameof(IsPaneOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsRightProperty =
        AvaloniaProperty.Register<SidePane, bool>(nameof(IsRight));

    public static readonly StyledProperty<string?> PaneTitleProperty =
        AvaloniaProperty.Register<SidePane, string?>(nameof(PaneTitle));

    public static readonly StyledProperty<string?> PaneIconProperty =
        AvaloniaProperty.Register<SidePane, string?>(nameof(PaneIcon), Controls.IconKind.Menu);

    public static readonly StyledProperty<bool> CloseOnSelectionProperty =
        AvaloniaProperty.Register<SidePane, bool>(nameof(CloseOnSelection), true);

    public static readonly StyledProperty<bool> CloseOnContentClickProperty =
        AvaloniaProperty.Register<SidePane, bool>(nameof(CloseOnContentClick), true);

    public static readonly DirectProperty<SidePane, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<SidePane, bool>(nameof(IsCompact), o => o.IsCompact);

    public static readonly StyledProperty<bool> HideContentWhenNarrowProperty =
        AvaloniaProperty.Register<SidePane, bool>(nameof(HideContentWhenNarrow));

    public static readonly DirectProperty<SidePane, bool> IsContentHiddenProperty =
        AvaloniaProperty.RegisterDirect<SidePane, bool>(nameof(IsContentHidden), o => o.IsContentHidden);

    private readonly Border _paneHost = new() { ClipToBounds = true };
    private readonly SplitterThumb _splitter = new();
    private readonly Border _rail = new();
    private readonly ActionButton _railButton = new() { VerticalAlignment = VerticalAlignment.Top };
    private bool _isCompact;
    private bool _isContentHidden;
    private double _railWidth;
    private IDisposable? _shadow;
    private IDisposable? _overlayBackground;

    public SidePane()
    {
        _rail.Child = _railButton;
        _railButton.Click += OnRailClick;
        _splitter.Value = PaneWidth;
        _splitter.PropertyChanged += OnSplitterPropertyChanged;

        BindResource(_rail, Border.BackgroundProperty, "Brush.Bg.Panel");
        BindResource(_rail, Border.BorderBrushProperty, "Brush.Border");
        BindResource(_rail, Border.PaddingProperty, "Padding.Popup");
        BindResource(_paneHost, Border.BorderBrushProperty, "Brush.Border");

        // Order = drawing order: the open overlay pane is drawn over the content.
        foreach (var part in new Control[] { _rail, _splitter, _paneHost })
        {
            LogicalChildren.Add(part);
            VisualChildren.Add(part);
        }
        UpdateRail();
        UpdateParts();

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _paneHost.AddHandler(SelectingItemsControl.SelectionChangedEvent, OnPaneSelectionChanged);
    }

    /// <summary>The side panel (a sidebar, a list, a details panel).</summary>
    public Control? Pane
    {
        get => GetValue(PaneProperty);
        set => SetValue(PaneProperty, value);
    }

    /// <summary>The main content; it always keeps at least <see cref="MinContentWidth"/>.</summary>
    [Content]
    public Control? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>The pane's chosen width (saved by the ViewModel); dragging the splitter changes it.</summary>
    public double PaneWidth
    {
        get => GetValue(PaneWidthProperty);
        set => SetValue(PaneWidthProperty, value);
    }

    public double MinPaneWidth
    {
        get => GetValue(MinPaneWidthProperty);
        set => SetValue(MinPaneWidthProperty, value);
    }

    /// <summary>Largest share of the width the docked pane may take (default 0.45).</summary>
    public double MaxPaneFraction
    {
        get => GetValue(MaxPaneFractionProperty);
        set => SetValue(MaxPaneFractionProperty, value);
    }

    /// <summary>Width the content never goes below while the pane is docked; under it, the pane folds into the rail.</summary>
    public double MinContentWidth
    {
        get => GetValue(MinContentWidthProperty);
        set => SetValue(MinContentWidthProperty, value);
    }

    /// <summary>Whether the folded pane is shown over the content (only used while <see cref="IsCompact"/>).</summary>
    public bool IsPaneOpen
    {
        get => GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    /// <summary>True when the pane sits on the right of the content.</summary>
    public bool IsRight
    {
        get => GetValue(IsRightProperty);
        set => SetValue(IsRightProperty, value);
    }

    /// <summary>The rail button's tooltip and accessible name ("Branches").</summary>
    public string? PaneTitle
    {
        get => GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

    /// <summary>The rail button's icon; one of <see cref="Controls.IconKind"/>.</summary>
    public string? PaneIcon
    {
        get => GetValue(PaneIconProperty);
        set => SetValue(PaneIconProperty, value);
    }

    /// <summary>Close the open overlay when something is picked in it with the mouse (default true).</summary>
    public bool CloseOnSelection
    {
        get => GetValue(CloseOnSelectionProperty);
        set => SetValue(CloseOnSelectionProperty, value);
    }

    /// <summary>
    /// Close the open overlay when the content is clicked (default true). Off for a details panel that follows what
    /// is clicked in the content (an issue panel): clicking another issue should show it, not hide the panel.
    /// </summary>
    public bool CloseOnContentClick
    {
        get => GetValue(CloseOnContentClickProperty);
        set => SetValue(CloseOnContentClickProperty, value);
    }

    /// <summary>
    /// When there's no room for both: hide the content and give the pane the whole width, instead of folding the pane
    /// into a rail. For panes that must stay usable (a repository's sidebar and changes list, with the diff as the
    /// content): two folded panes side by side would cover each other's rail. Pair it with a minimum window width
    /// (<c>DocumentViewModelBase.MinWidth</c>) so the panes themselves always fit.
    /// </summary>
    public bool HideContentWhenNarrow
    {
        get => GetValue(HideContentWhenNarrowProperty);
        set => SetValue(HideContentWhenNarrowProperty, value);
    }

    /// <summary>True while the content is hidden for lack of room (<see cref="HideContentWhenNarrow"/>; also <c>:contentHidden</c>).</summary>
    public bool IsContentHidden
    {
        get => _isContentHidden;
        private set
        {
            if (SetAndRaise(IsContentHiddenProperty, ref _isContentHidden, value))
            {
                PseudoClasses.Set(":contentHidden", value);
                if (Content is { } content)
                    content.IsHitTestVisible = !value;
                UpdateParts();
            }
        }
    }

    /// <summary>True while the pane is folded into the rail (also the <c>:compact</c> pseudo-class).</summary>
    public bool IsCompact
    {
        get => _isCompact;
        private set
        {
            if (SetAndRaise(IsCompactProperty, ref _isCompact, value))
            {
                PseudoClasses.Set(":compact", value);
                if (!value)
                    IsPaneOpen = false;
                UpdateParts();
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PaneProperty)
        {
            if (change.OldValue is Control oldPane)
                oldPane.PropertyChanged -= OnPanePropertyChanged;
            if (change.NewValue is Control pane)
                pane.PropertyChanged += OnPanePropertyChanged;
            _paneHost.Child = change.GetNewValue<Control?>();
            UpdateParts();
            InvalidateMeasure();
        }
        else if (change.Property == ContentProperty)
        {
            if (change.OldValue is Control old)
            {
                LogicalChildren.Remove(old);
                VisualChildren.Remove(old);
            }
            if (change.NewValue is Control content)
            {
                // Below the rail, splitter and pane, so the open pane covers it.
                LogicalChildren.Insert(0, content);
                VisualChildren.Insert(0, content);
            }
            InvalidateMeasure();
        }
        else if (change.Property == IsPaneOpenProperty || change.Property == IsRightProperty)
        {
            UpdateParts();
            InvalidateMeasure();
        }
        else if (change.Property == PaneTitleProperty || change.Property == PaneIconProperty)
        {
            UpdateRail();
        }
        else if (change.Property == PaneWidthProperty || change.Property == MinPaneWidthProperty
                 || change.Property == MinContentWidthProperty || change.Property == MaxPaneFractionProperty
                 || change.Property == HideContentWhenNarrowProperty)
        {
            _splitter.Value = PaneWidth;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var narrow = !double.IsInfinity(width) && width < MinPaneWidth + MinContentWidth;
        IsCompact = narrow && !HideContentWhenNarrow;
        IsContentHidden = narrow && HideContentWhenNarrow && HasPane;

        if (!HasPane)
        {
            Content?.Measure(availableSize);
            return Content?.DesiredSize ?? default;
        }

        if (IsContentHidden)
        {
            // The pane takes the whole width (never less than its minimum); the content waits for room.
            var all = Math.Max(MinPaneWidth, width);
            _paneHost.Measure(availableSize.WithWidth(all));
            Content?.Measure(default);
            return new Size(all, _paneHost.DesiredSize.Height);
        }

        if (IsCompact)
        {
            _rail.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            _railWidth = _rail.DesiredSize.Width;
            Content?.Measure(availableSize.WithWidth(Math.Max(0, width - _railWidth)));
            if (IsPaneOpen)
                _paneHost.Measure(availableSize.WithWidth(OverlayWidth(width)));
            return Content is null ? new Size(_railWidth, 0) : Content.DesiredSize.WithWidth(width);
        }

        var pane = DockedWidth(width);
        _paneHost.Measure(availableSize.WithWidth(pane));
        _splitter.Measure(availableSize);
        Content?.Measure(availableSize.WithWidth(double.IsInfinity(width) ? width : width - pane));
        var height = Math.Max(_paneHost.DesiredSize.Height, Content?.DesiredSize.Height ?? 0);
        return new Size(double.IsInfinity(width) ? pane + (Content?.DesiredSize.Width ?? 0) : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (w, h) = (finalSize.Width, finalSize.Height);
        if (!HasPane)
        {
            Content?.Arrange(new Rect(finalSize));
            _rail.Arrange(default);
            _splitter.Arrange(default);
            _paneHost.Arrange(default);
            return finalSize;
        }

        if (IsContentHidden)
        {
            _paneHost.Arrange(new Rect(0, 0, w, h));
            Content?.Arrange(default);
            _rail.Arrange(default);
            _splitter.Arrange(default);
            return finalSize;
        }

        if (IsCompact)
        {
            var rail = _railWidth;
            var overlay = OverlayWidth(w);
            if (IsRight)
            {
                Content?.Arrange(new Rect(0, 0, Math.Max(0, w - rail), h));
                _rail.Arrange(new Rect(w - rail, 0, rail, h));
                _paneHost.Arrange(IsPaneOpen ? new Rect(w - rail - overlay, 0, overlay, h) : default);
            }
            else
            {
                _rail.Arrange(new Rect(0, 0, rail, h));
                Content?.Arrange(new Rect(rail, 0, Math.Max(0, w - rail), h));
                _paneHost.Arrange(IsPaneOpen ? new Rect(rail, 0, overlay, h) : default);
            }
            _splitter.Arrange(default);
            return finalSize;
        }

        var pane = DockedWidth(w);
        // The splitter's 1px line sits at the pane's edge; its theme's negative margin widens the grab area.
        _splitter.Minimum = MinPaneWidth;
        _splitter.Maximum = Math.Max(MinPaneWidth, w - MinContentWidth);
        _splitter.MaximumFraction = MaxPaneFraction;
        if (IsRight)
        {
            Content?.Arrange(new Rect(0, 0, w - pane, h));
            _splitter.Arrange(new Rect(w - pane - 1, 0, 1, h));
            _paneHost.Arrange(new Rect(w - pane, 0, pane, h));
        }
        else
        {
            _paneHost.Arrange(new Rect(0, 0, pane, h));
            _splitter.Arrange(new Rect(pane, 0, 1, h));
            Content?.Arrange(new Rect(pane, 0, w - pane, h));
        }
        return finalSize;
    }

    private double DockedWidth(double available)
    {
        var max = double.IsInfinity(available)
            ? PaneWidth
            : Math.Min(available * MaxPaneFraction, available - MinContentWidth);
        return Math.Max(MinPaneWidth, Math.Min(PaneWidth, max));
    }

    // The open overlay: its chosen width, but always leaving a strip of content visible to click away on.
    private double OverlayWidth(double available) =>
        Math.Max(0, Math.Min(Math.Max(PaneWidth, MinPaneWidth), available - _railWidth - 48));

    // A pane that is hidden (an issue panel with no issue open) leaves the whole width to the content.
    private bool HasPane => Pane is { IsVisible: true };

    private void UpdateParts()
    {
        var overlay = IsCompact && IsPaneOpen;
        _rail.IsVisible = IsCompact && HasPane;
        _splitter.IsVisible = !IsCompact && !IsContentHidden && HasPane;
        _splitter.Invert = IsRight;
        _paneHost.IsVisible = HasPane && (!IsCompact || IsPaneOpen);
        _paneHost.BorderThickness = overlay
            ? (IsRight ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0))
            : default;
        // The shadow only while it lies over the content.
        _shadow?.Dispose();
        _shadow = overlay ? BindResource(_paneHost, Border.BoxShadowProperty, "Shadow.Overlay") : null;
        // Docked, the pane keeps its own look; over the content it needs an opaque background.
        _overlayBackground?.Dispose();
        _overlayBackground = overlay ? BindResource(_paneHost, Border.BackgroundProperty, "Brush.Bg.Panel") : null;
        _paneHost.ZIndex = IsCompact ? 2 : 0;
        _rail.BorderThickness = IsRight ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
        _railButton.Classes.Set("selected", IsPaneOpen);
    }

    private void UpdateRail()
    {
        _railButton.IconKind = PaneIcon;
        ToolTip.SetTip(_railButton, PaneTitle);
        AutomationProperties.SetName(_railButton, PaneTitle ?? L.T("Side panel"));
    }

    private void OnRailClick(object? sender, RoutedEventArgs e) => IsPaneOpen = !IsPaneOpen;

    // A pane that appears (an issue was opened) is shown, also when folded.
    private void OnPanePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty)
            return;
        if (IsCompact && e.GetNewValue<bool>())
            IsPaneOpen = true;
        UpdateParts();
        InvalidateMeasure();
    }

    private void OnSplitterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SplitterThumb.ValueProperty && !IsCompact)
            PaneWidth = (double)e.NewValue!;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsCompact && IsPaneOpen)
        {
            IsPaneOpen = false;
            _railButton.Focus();
            e.Handled = true;
        }
    }

    // A click on the content (not on the open pane or the rail) puts the pane away; the click still goes through.
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CloseOnContentClick || !IsCompact || !IsPaneOpen || e.Source is not Visual source)
            return;
        if (!_paneHost.IsVisualAncestorOf(source) && source != _paneHost
            && !_rail.IsVisualAncestorOf(source) && source != _rail)
            IsPaneOpen = false;
    }

    // Picking a branch or a file in the open pane = done with it. Only for mouse picks (keyboard browsing keeps it
    // open) and not for drop-downs, whose list lives in a popup.
    private void OnPaneSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CloseOnSelection && IsCompact && IsPaneOpen && _paneHost.IsPointerOver && e.Source is not ComboBox)
            IsPaneOpen = false;
    }

    private static IDisposable BindResource(Control target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
