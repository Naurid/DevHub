using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;

namespace DevHub.Controls;

/// <summary>
/// A thin drag handle that resizes a panel by changing a bound number:
/// <c>&lt;c:SplitterThumb Value="{Binding LeftWidth}" Orientation="Vertical" /&gt;</c>.
/// Used between tool windows and the center area instead of <c>GridSplitter</c>, because the
/// size lives in a ViewModel (so it can be saved) and slots can be closed (size 0) or open.
/// </summary>
/// <remarks>
/// Thumb reports the pointer's movement relative to the thumb itself. As the panel grows, the
/// thumb moves along with the pointer, so each delta is "how much further since last time",
/// which we add to <see cref="Value"/>.
/// </remarks>
public class SplitterThumb : Thumb
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SplitterThumb, double>(nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<SplitterThumb, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<bool> InvertProperty =
        AvaloniaProperty.Register<SplitterThumb, bool>(nameof(Invert));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<SplitterThumb, double>(nameof(Minimum), 120);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SplitterThumb, double>(nameof(Maximum), 1200);

    public static readonly StyledProperty<double> MaximumFractionProperty =
        AvaloniaProperty.Register<SplitterThumb, double>(nameof(MaximumFraction), 0.45);

    /// <summary>The size being changed (width or height of the panel). Two-way by default.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Vertical = a vertical bar dragged left/right (changes a width);
    /// Horizontal = a horizontal bar dragged up/down (changes a height).
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// True when dragging left/up should make the panel bigger, i.e. the panel is to the right
    /// of or below the thumb (right and bottom tool windows).
    /// </summary>
    public bool Invert
    {
        get => GetValue(InvertProperty);
        set => SetValue(InvertProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Largest share of the parent panel's width (or height) the panel may take, so dragging
    /// can never squeeze the rest of the window to nothing. Default 0.45 (45%). Keep it in sync
    /// with the slot's MaxWidth/MaxHeight in MainWindow.axaml, which applies the same limit
    /// when the window itself gets smaller.
    /// </summary>
    public double MaximumFraction
    {
        get => GetValue(MaximumFractionProperty);
        set => SetValue(MaximumFractionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OrientationProperty)
        {
            Cursor = new Cursor(Orientation == Orientation.Vertical
                ? StandardCursorType.SizeWestEast
                : StandardCursorType.SizeNorthSouth);
        }
    }

    protected override void OnDragDelta(VectorEventArgs e)
    {
        base.OnDragDelta(e);

        var delta = Orientation == Orientation.Vertical ? e.Vector.X : e.Vector.Y;
        if (Invert)
            delta = -delta;

        // Limit = the smaller of Maximum and a share of the container we sit in.
        var max = Maximum;
        if (Parent is Layoutable container)
        {
            var available = Orientation == Orientation.Vertical ? container.Bounds.Width : container.Bounds.Height;
            if (available > 0)
                max = Math.Min(max, available * MaximumFraction);
        }
        max = Math.Max(max, Minimum);

        // Start from the clamped value: if a saved size was larger than the limit, the panel is
        // already shown at the limit, and dragging should respond from there (no dead zone).
        Value = Math.Clamp(Math.Min(Value, max) + delta, Minimum, max);
        e.Handled = true;
    }
}
