using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using DevHub.Sdk.Core.Charts;

namespace DevHub.Controls;

/// <summary>
/// What the Sdk's charts share: <see cref="Data"/> (labels + series, built by a plugin's .Core), the series colors
/// (Brush.Chart.1…6, following Dark / Light), text from the design tokens, a legend when there's more than one series,
/// and a tooltip with the values under the pointer. Drawn directly (no chart library). An empty chart shows
/// <see cref="EmptyText"/>.
/// </summary>
public abstract class ChartBase : Control
{
    public static readonly StyledProperty<ChartData?> DataProperty =
        AvaloniaProperty.Register<ChartBase, ChartData?>(nameof(Data));

    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ChartBase, string>(nameof(EmptyText), "No data yet");

    static ChartBase()
    {
        AffectsRender<ChartBase>(DataProperty, EmptyTextProperty);
        AffectsMeasure<ChartBase>(DataProperty);
    }

    public ChartData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    protected const double Gap = 6;

    protected ChartData Current => Data ?? ChartData.Empty;

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height);

    // Colors and fonts follow the theme: redraw when it switches (subscribed only while on screen).
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected IBrush Resource(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : fallback;

    /// <summary>The color of series (or slice) <paramref name="index"/>: Brush.Chart.1…6, then round again.</summary>
    protected IBrush SeriesBrush(int index) => Resource($"Brush.Chart.{index % 6 + 1}", Brushes.SteelBlue);

    protected IBrush TextBrush => Resource("Brush.Text.Secondary", Brushes.Gray);
    protected IBrush GridBrush => Resource("Brush.Separator", Brushes.LightGray);

    protected FormattedText Text(string text, IBrush? brush = null)
    {
        var family = this.TryFindResource("Font.UI", out var f) && f is FontFamily font ? font : TextElement.GetFontFamily(this);
        var size = this.TryFindResource("FontSize.Small", out var s) && s is double d ? d : 12;
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(family), size, brush ?? TextBrush);
    }

    public sealed override void Render(DrawingContext context)
    {
        var data = Current;
        if (Bounds.Width < 40 || Bounds.Height < 40)
            return;
        // A transparent background: the whole area takes the pointer (tooltips).
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (IsEmpty(data))
        {
            var empty = Text(EmptyText);
            context.DrawText(empty, new Point((Bounds.Width - empty.Width) / 2, (Bounds.Height - empty.Height) / 2));
            return;
        }
        var top = 0.0;
        if (ShowsLegend(data))
            top = DrawLegend(context, data);
        RenderChart(context, data, new Rect(0, top, Bounds.Width, Bounds.Height - top));
    }

    protected virtual bool IsEmpty(ChartData data) => data.Labels.Count == 0 || data.Range() is null;

    protected virtual bool ShowsLegend(ChartData data) => data.Series.Count > 1;

    /// <summary>Draws the chart in <paramref name="area"/> (below the legend).</summary>
    protected abstract void RenderChart(DrawingContext context, ChartData data, Rect area);

    /// <summary>The legend: a swatch and the name of each series, wrapped; returns its height.</summary>
    protected double DrawLegend(DrawingContext context, ChartData data) =>
        DrawLegend(context, data.Series.Select(s => s.Name).ToList(), new Rect(0, 0, Bounds.Width, Bounds.Height));

    protected double DrawLegend(DrawingContext context, IReadOnlyList<string> names, Rect area)
    {
        double x = area.X, y = area.Y, rowHeight = 0;
        for (var i = 0; i < names.Count; i++)
        {
            var text = Text(names[i], Resource("Brush.Text.Primary", Brushes.Black));
            var width = 10 + 4 + text.Width + 12;
            if (x > area.X && x + width > area.Right)
            {
                x = area.X;
                y += rowHeight + 2;
            }
            context.FillRectangle(SeriesBrush(i), new Rect(x, y + (text.Height - 10) / 2, 10, 10), 2);
            context.DrawText(text, new Point(x + 14, y));
            x += width;
            rowHeight = Math.Max(rowHeight, text.Height);
        }
        return y - area.Y + rowHeight + Gap;
    }

    // Tooltip: what's under the pointer (a label's values, a slice).
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var tip = Current is { } data && !IsEmpty(data) ? ToolTipAt(data, e.GetPosition(this)) : null;
        if (!Equals(ToolTip.GetTip(this), tip))
        {
            ToolTip.SetTip(this, tip);
            ToolTip.SetIsOpen(this, tip is not null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetIsOpen(this, false);
    }

    /// <summary>The tooltip text for a point, or null for none.</summary>
    protected abstract string? ToolTipAt(ChartData data, Point point);

    /// <summary>"Mon: Done 3 · Open 5" (single series: "Mon: 3").</summary>
    protected static string ValuesAt(ChartData data, int index) =>
        data.Labels[index] + ": " + string.Join(" · ", data.Series
            .Where(s => index < s.Values.Count && s.Values[index] is not null)
            .Select(s => (data.Series.Count > 1 ? s.Name + " " : "") + ChartMath.Short(s.Values[index]!.Value)));
}

/// <summary>An axis chart (lines or bars): a value axis on the left with grid lines, labels below.</summary>
public abstract class AxisChart : ChartBase
{
    /// <summary>The area the values are drawn in (after <see cref="RenderChart"/> ran), for hit tests.</summary>
    protected Rect Plot { get; private set; }

    protected AxisScale Scale { get; private set; } = new(0, 1, 1);

    protected virtual bool IncludeZero => true;

    protected sealed override void RenderChart(DrawingContext context, ChartData data, Rect area)
    {
        var (min, max) = data.Range()!.Value;
        Scale = ChartMath.NiceScale(min, max, Math.Max(2, (int)(area.Height / 40)), IncludeZero);
        var ticks = Scale.Ticks();
        var tickTexts = ticks.Select(t => Text(ChartMath.Short(t))).ToList();
        var left = tickTexts.Max(t => t.Width) + Gap;
        var labelHeight = Text("Ag").Height;
        // Room below for the labels, clear of the lowest tick's text (which is centered on the bottom line).
        Plot = new Rect(area.X + left, area.Y + labelHeight / 2, Math.Max(1, area.Width - left - Gap), Math.Max(1, area.Height - labelHeight * 2 - Gap));

        var grid = new Pen(GridBrush, 1);
        for (var i = 0; i < ticks.Count; i++)
        {
            var y = Math.Round(Y(ticks[i])) + 0.5;
            context.DrawLine(grid, new Point(Plot.Left, y), new Point(Plot.Right, y));
            context.DrawText(tickTexts[i], new Point(Plot.Left - Gap - tickTexts[i].Width, y - tickTexts[i].Height / 2));
        }

        // The labels below, thinned out so they don't overlap.
        var n = data.Labels.Count;
        var widest = data.Labels.Count == 0 ? 0 : data.Labels.Max(l => Text(l).Width) + Gap * 2;
        var every = Math.Max(1, (int)Math.Ceiling(widest * n / Plot.Width));
        for (var i = 0; i < n; i += every)
        {
            var text = Text(data.Labels[i]);
            var x = Math.Clamp(X(i, n) - text.Width / 2, area.X, area.Right - text.Width);
            context.DrawText(text, new Point(x, Plot.Bottom + labelHeight / 2 + 2));
        }

        RenderValues(context, data);
    }

    protected abstract void RenderValues(DrawingContext context, ChartData data);

    /// <summary>Where a value sits vertically.</summary>
    protected double Y(double value) => Plot.Bottom - Scale.Fraction(value) * Plot.Height;

    /// <summary>The center of label <paramref name="index"/> of <paramref name="count"/>.</summary>
    protected abstract double X(int index, int count);

    protected int? IndexAt(ChartData data, Point point)
    {
        var n = data.Labels.Count;
        if (n == 0 || !Plot.Inflate(Gap).Contains(point))
            return null;
        return Enumerable.Range(0, n).MinBy(i => Math.Abs(X(i, n) - point.X));
    }

    protected override string? ToolTipAt(ChartData data, Point point) => IndexAt(data, point) is { } i ? ValuesAt(data, i) : null;
}

/// <summary>
/// Lines over the labels, one per series (a burndown, run durations over time): <c>&lt;c:LineChart Data="{Binding Burndown}" /&gt;</c>.
/// A null value leaves a gap. Values are marked with dots when there's room for them.
/// </summary>
public class LineChart : AxisChart
{
    public static readonly StyledProperty<bool> IsZeroBasedProperty =
        AvaloniaProperty.Register<LineChart, bool>(nameof(IsZeroBased), true);

    static LineChart() => AffectsRender<LineChart>(IsZeroBasedProperty);

    /// <summary>True (default): the axis starts at 0. False: it hugs the data (e.g. durations around 5 minutes).</summary>
    public bool IsZeroBased { get => GetValue(IsZeroBasedProperty); set => SetValue(IsZeroBasedProperty, value); }

    protected override bool IncludeZero => IsZeroBased;

    protected override double X(int index, int count) =>
        count <= 1 ? Plot.Center.X : Plot.Left + index * Plot.Width / (count - 1);

    protected override void RenderValues(DrawingContext context, ChartData data)
    {
        var n = data.Labels.Count;
        var dots = Plot.Width / Math.Max(1, n) >= 12;
        for (var s = 0; s < data.Series.Count; s++)
        {
            var brush = SeriesBrush(s);
            var pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            Point? previous = null;
            var values = data.Series[s].Values;
            for (var i = 0; i < n; i++)
            {
                if (i >= values.Count || values[i] is not { } value || !double.IsFinite(value))
                {
                    previous = null; // a gap
                    continue;
                }
                var point = new Point(X(i, n), Y(value));
                if (previous is { } from)
                    context.DrawLine(pen, from, point);
                if (dots || n == 1)
                    context.DrawEllipse(brush, null, point, 3, 3);
                previous = point;
            }
        }
    }
}

/// <summary>
/// Bars per label, side by side for several series (velocity: committed vs done per sprint):
/// <c>&lt;c:BarChart Data="{Binding Velocity}" /&gt;</c>. Negative values go below the zero line.
/// </summary>
public class BarChart : AxisChart
{
    protected override double X(int index, int count) => Plot.Left + (index + 0.5) * Plot.Width / Math.Max(1, count);

    protected override void RenderValues(DrawingContext context, ChartData data)
    {
        var n = data.Labels.Count;
        var group = Plot.Width / Math.Max(1, n);
        var k = Math.Max(1, data.Series.Count);
        var bar = Math.Max(1, group * 0.7 / k);
        var zero = Y(Math.Clamp(0, Scale.Min, Scale.Max));
        for (var s = 0; s < data.Series.Count; s++)
        {
            var brush = SeriesBrush(s);
            var values = data.Series[s].Values;
            for (var i = 0; i < n && i < values.Count; i++)
            {
                if (values[i] is not { } value || !double.IsFinite(value))
                    continue;
                var x = X(i, n) - bar * k / 2 + s * bar;
                var y = Y(value);
                var rect = new Rect(x + 0.5, Math.Min(y, zero), Math.Max(1, bar - 1), Math.Max(1, Math.Abs(zero - y)));
                context.FillRectangle(brush, rect, (float)Math.Min(2, bar / 4));
            }
        }
    }
}

/// <summary>
/// Shares of a whole, from the first series (issues by status, time by category): <c>&lt;c:PieChart Data="{Binding ByStatus}" /&gt;</c>.
/// Non-positive values get no slice. A legend on the right names each slice with its share; <see cref="IsDonut"/> makes a ring.
/// </summary>
public class PieChart : ChartBase
{
    public static readonly StyledProperty<bool> IsDonutProperty =
        AvaloniaProperty.Register<PieChart, bool>(nameof(IsDonut));

    static PieChart() => AffectsRender<PieChart>(IsDonutProperty);

    public bool IsDonut { get => GetValue(IsDonutProperty); set => SetValue(IsDonutProperty, value); }

    private Point _center;
    private double _radius;
    private IReadOnlyList<PieSlice> _slices = [];

    protected override bool IsEmpty(ChartData data) => data.Series.Count == 0 || ChartMath.Pie(data.Series[0].Values).Count == 0;

    protected override bool ShowsLegend(ChartData data) => false; // its own legend, beside the pie

    protected override void RenderChart(DrawingContext context, ChartData data, Rect area)
    {
        _slices = ChartMath.Pie(data.Series[0].Values);
        var names = _slices.Select(s => $"{Label(data, s.Index)} — {ChartMath.Short(s.Value)} ({s.Fraction:P0})").ToList();
        var legendWidth = Math.Min(area.Width * 0.5, names.Max(n => Text(n).Width) + 14 + Gap * 2);
        var size = Math.Min(area.Width - legendWidth, area.Height);
        _radius = Math.Max(4, size / 2 - Gap);
        _center = new Point(area.X + size / 2, area.Y + area.Height / 2);

        var outline = new Pen(Resource("Brush.Bg.Editor", Brushes.White), 1.5);
        foreach (var slice in _slices)
        {
            var brush = SeriesBrush(slice.Index);
            if (slice.SweepAngle >= 359.999)
                context.DrawEllipse(brush, null, _center, _radius, _radius);
            else
                context.DrawGeometry(brush, outline, Wedge(slice.StartAngle, slice.SweepAngle));
        }
        if (IsDonut)
            context.DrawEllipse(Resource("Brush.Bg.Editor", Brushes.White), null, _center, _radius * 0.55, _radius * 0.55);

        // The legend, in slice order, in the slices' colors.
        var y = area.Y + Math.Max(0, (area.Height - names.Count * (Text("Ag").Height + 2)) / 2);
        for (var i = 0; i < _slices.Count; i++)
        {
            var text = Text(names[i], Resource("Brush.Text.Primary", Brushes.Black));
            var x = area.X + size + Gap;
            context.FillRectangle(SeriesBrush(_slices[i].Index), new Rect(x, y + (text.Height - 10) / 2, 10, 10), 2);
            context.DrawText(text, new Point(x + 14, y));
            y += text.Height + 2;
        }
    }

    private static string Label(ChartData data, int index) => index < data.Labels.Count ? data.Labels[index] : $"#{index + 1}";

    private Point OnCircle(double angle) =>
        new(_center.X + _radius * Math.Sin(angle * Math.PI / 180), _center.Y - _radius * Math.Cos(angle * Math.PI / 180));

    private StreamGeometry Wedge(double start, double sweep)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(_center, isFilled: true);
        g.LineTo(OnCircle(start));
        g.ArcTo(OnCircle(start + sweep), new Size(_radius, _radius), 0, sweep > 180, SweepDirection.Clockwise);
        g.EndFigure(true);
        return geometry;
    }

    protected override string? ToolTipAt(ChartData data, Point point)
    {
        var dx = point.X - _center.X;
        var dy = point.Y - _center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > _radius || (IsDonut && distance < _radius * 0.55))
            return null;
        var angle = (Math.Atan2(dx, -dy) * 180 / Math.PI + 360) % 360;
        return _slices.FirstOrDefault(s => angle >= s.StartAngle && angle < s.StartAngle + s.SweepAngle) is { } slice
            ? $"{Label(data, slice.Index)}: {ChartMath.Short(slice.Value)} ({slice.Fraction:P0})"
            : null;
    }
}
