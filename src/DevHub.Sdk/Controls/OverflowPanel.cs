using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Controls;

/// <summary>
/// One row that never wraps or clips: what doesn't fit moves into a « More » menu at the end of the row.
/// As the items panel of a tab strip (<c>&lt;ItemsPanelTemplate&gt;&lt;c:OverflowPanel /&gt;&lt;/ItemsPanelTemplate&gt;</c>)
/// the selected item always stays in the row and the menu picks an item; around buttons
/// (<c>&lt;c:OverflowPanel&gt;&lt;Button …/&gt;…&lt;/c:OverflowPanel&gt;</c>) the menu runs the hidden buttons' commands,
/// named by their text, accessible name or tooltip (a toggle becomes a check item).
/// </summary>
/// <remarks>
/// Instead of a <c>WrapPanel</c>, whose second row pushes everything below down, or a <c>StackPanel</c>, which
/// cuts the last items off. The menu button gets the <c>overflowMore</c> class for styling.
/// </remarks>
public class OverflowPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<OverflowPanel, double>(nameof(Spacing));

    public static readonly StyledProperty<object?> MoreContentProperty =
        AvaloniaProperty.Register<OverflowPanel, object?>(nameof(MoreContent));

    public static readonly DirectProperty<OverflowPanel, bool> HasOverflowProperty =
        AvaloniaProperty.RegisterDirect<OverflowPanel, bool>(nameof(HasOverflow), o => o.HasOverflow);

    private readonly Button _more = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly MenuFlyout _menu = new() { Placement = PlacementMode.BottomEdgeAlignedRight };
    private readonly List<Control> _hidden = [];
    private SelectingItemsControl? _owner;
    private bool _hasOverflow;

    static OverflowPanel()
    {
        AffectsMeasure<OverflowPanel>(SpacingProperty, MoreContentProperty);
    }

    public OverflowPanel()
    {
        ClipToBounds = true;
        _more.Classes.Add("flat");
        _more.Classes.Add("overflowMore");
        // Filled on click, then opened: the hidden items change with the width.
        _more.Click += OnMoreClick;
        AutomationProperties.SetName(_more, L.T("More"));
        // Last in both lists: Panel inserts its children by index, so they stay in front of it.
        LogicalChildren.Add(_more);
        VisualChildren.Add(_more);
        UpdateMoreContent();
    }

    /// <summary>Space between items.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>What the menu button shows; by default « More ▾ » for a tab strip and « ⋯ » for buttons.</summary>
    public object? MoreContent
    {
        get => GetValue(MoreContentProperty);
        set => SetValue(MoreContentProperty, value);
    }

    /// <summary>True when some items are in the menu.</summary>
    public bool HasOverflow
    {
        get => _hasOverflow;
        private set => SetAndRaise(HasOverflowProperty, ref _hasOverflow, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MoreContentProperty)
            UpdateMoreContent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = IsItemsHost ? this.FindAncestorOfType<SelectingItemsControl>() : null;
        if (_owner is not null)
            _owner.SelectionChanged += OnOwnerSelectionChanged;
        UpdateMoreContent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_owner is not null)
            _owner.SelectionChanged -= OnOwnerSelectionChanged;
        _owner = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var row = new Size(double.PositiveInfinity, availableSize.Height);
        double total = 0, height = 0;
        var shown = 0;
        foreach (var child in Children)
        {
            child.Measure(row);
            if (!child.IsVisible)
                continue;
            shown++;
            total += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        total += Spacing * Math.Max(0, shown - 1);

        _hidden.Clear();
        if (total <= availableSize.Width)
        {
            HasOverflow = false;
            _more.Measure(default);
            return new Size(total, height);
        }

        _more.Measure(row);
        height = Math.Max(height, _more.DesiredSize.Height);
        var room = availableSize.Width - _more.DesiredSize.Width - Spacing;

        // Keep items from the start while they fit; the selected one always stays, after the others if need be.
        var selected = Children.FirstOrDefault(IsSelected);
        var reserved = selected is null ? 0 : selected.DesiredSize.Width + Spacing;
        double used = 0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            var width = child.DesiredSize.Width + Spacing;
            if (child == selected)
            {
                if (used + width <= room)
                {
                    used += width;
                    reserved = 0;
                }
                continue;
            }
            if (_hidden.Count == 0 && used + width + reserved <= room)
                used += width;
            else
                _hidden.Add(child);
        }

        HasOverflow = _hidden.Count > 0;
        return new Size(availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
                continue;
            if (_hidden.Contains(child))
            {
                // Outside the clip: not drawn, not clickable.
                child.Arrange(new Rect(finalSize.Width + 1, 0, child.DesiredSize.Width, finalSize.Height));
                continue;
            }
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + Spacing;
        }

        _more.IsVisible = HasOverflow;
        if (HasOverflow)
            _more.Arrange(new Rect(x, 0, _more.DesiredSize.Width, finalSize.Height));
        return finalSize;
    }

    private void OnOwnerSelectionChanged(object? sender, SelectionChangedEventArgs e) => InvalidateMeasure();

    private void UpdateMoreContent() =>
        _more.Content = MoreContent ?? (_owner is not null ? L.T("More") + " ▾" : "⋯");

    private void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        var items = new List<MenuItem>();
        foreach (var child in _hidden)
        {
            if (_owner is { } owner)
            {
                var item = owner.ItemFromContainer(child);
                var entry = new MenuItem { Header = item, HeaderTemplate = owner.ItemTemplate };
                AutomationProperties.SetName(entry, item?.ToString());   // list items name themselves with ToString()
                entry.Click += (_, _) => owner.SelectedItem = item;
                items.Add(entry);
            }
            else if (Inner(child) is ToggleButton { IsVisible: true } toggle)
            {
                var entry = new MenuItem
                {
                    Header = NameOf(toggle),
                    ToggleType = MenuItemToggleType.CheckBox,
                    IsChecked = toggle.IsChecked == true,
                    IsEnabled = toggle.IsEffectivelyEnabled,
                };
                entry.Click += (_, _) => toggle.IsChecked = toggle.IsChecked != true;
                items.Add(entry);
            }
            else if (Inner(child) is Button { IsVisible: true } button)
            {
                var entry = new MenuItem
                {
                    Header = NameOf(button),
                    Command = button.Command,
                    CommandParameter = button.CommandParameter,
                    IsEnabled = button.IsEffectivelyEnabled,
                };
                if (button.Flyout is { } flyout)
                    entry.Click += (_, _) => flyout.ShowAt(_more);
                items.Add(entry);
            }
        }
        _menu.ItemsSource = items;
        if (items.Count > 0)
            _menu.ShowAt(_more);
    }

    // A toolbar's items sit in ContentPresenters (an ItemsControl's containers).
    private static Control Inner(Control child) => child is ContentPresenter { Child: { } inner } ? inner : child;

    // The short name first (a button's text, an icon button's accessible name); the tooltip is often a sentence.
    private static string? NameOf(Button button) =>
        button.Content as string
        ?? AutomationProperties.GetName(button)
        ?? ToolTip.GetTip(button) as string;

    private static bool IsSelected(Control child) => child is ISelectable { IsSelected: true };
}
