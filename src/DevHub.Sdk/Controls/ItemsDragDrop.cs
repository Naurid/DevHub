using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DevHub.Controls;

/// <summary>
/// A drop asked of an <see cref="IItemsDropHandler"/>: <see cref="Item"/> (dragged from the list <see cref="SourceList"/>)
/// would go into <see cref="TargetList"/> at <see cref="Index"/>, counted as the target looks before the move
/// (<see cref="Sdk.Core.Collections.ListMoves.Move{T}"/> takes it as is). The lists are what <see cref="ItemsDragDrop.ListProperty"/>
/// says (by default each ItemsControl's ItemsSource).
/// </summary>
public sealed record ItemsDrop(object Item, object? SourceList, object TargetList, int Index);

/// <summary>What a list's drag and drop does (implemented by its ViewModel): may this item go there, and do it.</summary>
public interface IItemsDropHandler
{
    bool CanDrop(ItemsDrop drop);

    void Drop(ItemsDrop drop);
}

/// <summary>
/// Drag and drop between the items of lists (any ItemsControl: ListBox, ItemsControl…), decided by the ViewModel: reorder
/// a list, move items between lists, columns of a board along the board and its cards between columns:
/// <code>
/// &lt;ItemsControl ItemsSource="{Binding Columns}" c:ItemsDragDrop.Handler="{Binding}" c:ItemsDragDrop.Orientation="Horizontal"&gt;
///   … each column: &lt;ItemsControl ItemsSource="{Binding Cards}" c:ItemsDragDrop.Handler="{Binding $parent[ItemsControl;1].DataContext}" /&gt;
/// </code>
/// A drag starts when the pointer moves a few pixels with the left button down on an item (not in a text box); a line in
/// the accent color shows where it would go, and only where the handler allows it. Nested lists stay apart: pressing a card
/// drags the card, not its column. Items travel in-process only (never files from outside).
/// </summary>
public static class ItemsDragDrop
{
    public static readonly AttachedProperty<IItemsDropHandler?> HandlerProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, IItemsDropHandler?>("Handler", typeof(ItemsDragDrop));

    /// <summary>Which list this is, as the handler sees it (default: the ItemsSource), e.g. the column's ViewModel.</summary>
    public static readonly AttachedProperty<object?> ListProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, object?>("List", typeof(ItemsDragDrop));

    /// <summary>Vertical (default): before / after by height. Horizontal: by width (a row of columns).</summary>
    public static readonly AttachedProperty<Orientation> OrientationProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, Orientation>("Orientation", typeof(ItemsDragDrop), Orientation.Vertical);

    private sealed record Payload(object Item, object? SourceList);

    private static readonly DataFormat<Payload> ItemFormat = DataFormat.CreateInProcessFormat<Payload>("DevHub.ListItem");
    private const double StartDistance = 4;

    // One drag at a time in the whole app, so the pending press can live here.
    private static PointerPressedEventArgs? _pressed;
    private static Point _pressedAt;
    private static ItemsControl? _pressedList;
    private static object? _pressedItem;
    private static Control? _marked;
    private static InsertionLine? _line;

    static ItemsDragDrop()
    {
        HandlerProperty.Changed.AddClassHandler<ItemsControl>((list, e) =>
        {
            Detach(list);
            if (e.NewValue is not null)
                Attach(list);
        });
    }

    public static IItemsDropHandler? GetHandler(ItemsControl list) => list.GetValue(HandlerProperty);
    public static void SetHandler(ItemsControl list, IItemsDropHandler? value) => list.SetValue(HandlerProperty, value);
    public static object? GetList(ItemsControl list) => list.GetValue(ListProperty);
    public static void SetList(ItemsControl list, object? value) => list.SetValue(ListProperty, value);
    public static Orientation GetOrientation(ItemsControl list) => list.GetValue(OrientationProperty);
    public static void SetOrientation(ItemsControl list, Orientation value) => list.SetValue(OrientationProperty, value);

    private static object ListOf(ItemsControl list) => GetList(list) ?? (object?)list.ItemsSource ?? list;

    private static void Attach(ItemsControl list)
    {
        // handledEventsToo: an item may handle the press itself (selection); the drag still needs to see it.
        list.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        DragDrop.SetAllowDrop(list, true);
        DragDrop.AddDragOverHandler(list, OnDragOver);
        DragDrop.AddDragLeaveHandler(list, OnDragLeave);
        DragDrop.AddDropHandler(list, OnDrop);
    }

    private static void Detach(ItemsControl list)
    {
        list.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        list.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        list.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        DragDrop.SetAllowDrop(list, false);
        DragDrop.RemoveDragOverHandler(list, OnDragOver);
        DragDrop.RemoveDragLeaveHandler(list, OnDragLeave);
        DragDrop.RemoveDropHandler(list, OnDrop);
    }

    /// <summary>
    /// The item container of <paramref name="list"/> that <paramref name="source"/> is in, or null: also when it's in an
    /// item of a nested list that has its own drag and drop (that list deals with it).
    /// </summary>
    private static Control? ContainerOf(ItemsControl list, object? source)
    {
        for (var visual = source as Visual; visual is not null && !ReferenceEquals(visual, list); visual = visual.GetVisualParent())
        {
            if (visual is TextBox)
                return null; // selecting text, not dragging
            if (visual is Control control && ItemsControl.ItemsControlFromItemContainer(control) is { } owner)
            {
                if (ReferenceEquals(owner, list))
                    return control;
                if (GetHandler(owner) is not null)
                    return null;
            }
        }
        return null;
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ItemsControl list || !e.GetCurrentPoint(list).Properties.IsLeftButtonPressed || ContainerOf(list, e.Source) is not { } container)
            return;
        var index = list.IndexFromContainer(container);
        if (index < 0 || list.Items.Count <= index || list.Items[index] is not { } item)
            return;
        _pressed = e;
        _pressedAt = e.GetPosition(list);
        _pressedList = list;
        _pressedItem = item;
    }

    private static async void OnMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not ItemsControl list || !ReferenceEquals(list, _pressedList) || _pressed is not { } pressed || _pressedItem is not { } item)
            return;
        if (!e.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
        {
            Clear();
            return;
        }
        var delta = e.GetPosition(list) - _pressedAt;
        if (Math.Abs(delta.X) < StartDistance && Math.Abs(delta.Y) < StartDistance)
            return;

        Clear(); // one drag per press
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(ItemFormat, new Payload(item, ListOf(list))));
        await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Move);
        Mark(null, false, Orientation.Vertical);
    }

    private static void OnReleased(object? sender, PointerReleasedEventArgs e) => Clear();

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        if (Target(sender, e) is not { } target)
        {
            e.DragEffects = DragDropEffects.None; // a list further out may still take it: not handled
            return;
        }
        e.DragEffects = DragDropEffects.Move;
        Mark(target.Marker, target.After, GetOrientation(target.List));
        e.Handled = true;
    }

    private static void OnDragLeave(object? sender, DragEventArgs e) => Mark(null, false, Orientation.Vertical);

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        Mark(null, false, Orientation.Vertical);
        if (Target(sender, e) is not { } target)
            return;
        GetHandler(target.List)!.Drop(target.Drop);
        e.Handled = true;
    }

    private sealed record DropTarget(ItemsControl List, ItemsDrop Drop, Control Marker, bool After);

    /// <summary>Where the drop would go in this list, if its handler allows it.</summary>
    private static DropTarget? Target(object? sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list || GetHandler(list) is not { } handler || e.DataTransfer.TryGetValue(ItemFormat) is not { } payload)
            return null;

        // Before or after the item under the pointer, by its middle; past the last one: at the end.
        var vertical = GetOrientation(list) == Orientation.Vertical;
        var point = e.GetPosition(list);
        var containers = list.GetRealizedContainers().OrderBy(list.IndexFromContainer).ToList();
        int index = list.ItemCount;
        Control marker = list;
        var after = false;
        foreach (var container in containers)
        {
            if (container.TranslatePoint(new Point(0, 0), list) is not { } at)
                continue;
            var middle = vertical ? at.Y + container.Bounds.Height / 2 : at.X + container.Bounds.Width / 2;
            if ((vertical ? point.Y : point.X) < middle)
            {
                index = list.IndexFromContainer(container);
                marker = container;
                after = false;
                break;
            }
            marker = container;
            after = true;
        }

        var drop = new ItemsDrop(payload.Item, payload.SourceList, ListOf(list), index);
        return handler.CanDrop(drop) ? new DropTarget(list, drop, marker, after && !ReferenceEquals(marker, list)) : null;
    }

    /// <summary>The insertion line: before or after <paramref name="target"/> (an item), or around the list when it's empty.</summary>
    private static void Mark(Control? target, bool after, Orientation orientation)
    {
        if (ReferenceEquals(target, _marked) && _line is { } same && same.After == after)
            return;
        if (_marked is not null)
            AdornerLayer.SetAdorner(_marked, null);
        _marked = target;
        _line = target is null ? null : new InsertionLine(after, orientation, target is ItemsControl);
        if (target is not null)
            AdornerLayer.SetAdorner(target, _line);
    }

    private static void Clear()
    {
        _pressed = null;
        _pressedList = null;
        _pressedItem = null;
    }

    /// <summary>A 2-pixel line in the accent color on one edge of an item (or a frame around an empty list).</summary>
    private sealed class InsertionLine : Control
    {
        public InsertionLine(bool after, Orientation orientation, bool wholeList)
        {
            After = after;
            _orientation = orientation;
            _wholeList = wholeList;
            IsHitTestVisible = false;
        }

        public bool After { get; }
        private readonly Orientation _orientation;
        private readonly bool _wholeList;

        public override void Render(DrawingContext context)
        {
            var brush = this.TryFindResource("Brush.Accent", ActualThemeVariant, out var value) && value is IBrush b ? b : Brushes.DodgerBlue;
            var size = Bounds.Size;
            if (_wholeList)
            {
                context.DrawRectangle(null, new Pen(brush, 2), new Rect(size).Deflate(1), 4, 4);
                return;
            }
            var rect = _orientation == Orientation.Vertical
                ? new Rect(0, After ? size.Height - 2 : 0, size.Width, 2)
                : new Rect(After ? size.Width - 2 : 0, 0, 2, size.Height);
            context.FillRectangle(brush, rect, 1);
        }
    }
}
