using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using DevHub.Sdk.Core.Charts;

namespace DevHub.Controls;

/// <summary>
/// What the Sdk's charts share: <see cref="Data"/> (labels + series, built by a plugin's .Core), the series colors
/// (Brush.Chart.1…6, following Dark / Light, or a series' own <see cref="SeriesStyle.Color"/>), text from the design
/// tokens, a legend when there's more than one series, and a tooltip with the values under the pointer. Drawn directly
/// (no chart library). An empty chart shows <see cref="EmptyText"/>. For images (<see cref="ChartImage"/>) a chart can
/// be drawn off screen in a given <see cref="ExportTheme"/>, with its title and a background.
/// </summary>
public abstract class ChartBase : Control
{
    public static readonly StyledProperty<ChartData?> DataProperty =
        AvaloniaProperty.Register<ChartBase, ChartData?>(nameof(Data));

    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ChartBase, string>(nameof(EmptyText), "No data yet");

    /// <summary>Draw <see cref="ChartData.Title"/> above the chart (images do; pages usually have their own heading).</summary>
    public static readonly StyledProperty<bool> ShowTitleProperty =
        AvaloniaProperty.Register<ChartBase, bool>(nameof(ShowTitle));

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ChartBase, IBrush?>(nameof(Background));

    static ChartBase()
    {
        AffectsRender<ChartBase>(DataProperty, EmptyTextProperty, ShowTitleProperty, BackgroundProperty);
        AffectsMeasure<ChartBase>(DataProperty);
    }

    public ChartData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public bool ShowTitle { get => GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>Off screen: the theme to take colors from (the chart isn't in a window that has one).</summary>
    public ThemeVariant? ExportTheme { get; set; }

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

    private ThemeVariant ColorTheme => ExportTheme ?? ActualThemeVariant;

    private bool TryResource(string key, out object? value) =>
        this.TryFindResource(key, ColorTheme, out value) || Application.Current?.TryGetResource(key, ColorTheme, out value) == true;

    protected IBrush Resource(string key, IBrush fallback)
    {
        // Off screen the brushes would follow the app's theme: build them from the theme's colors instead.
        if (ExportTheme is not null && key.StartsWith("Brush.", StringComparison.Ordinal)
            && TryResource("Color." + key["Brush.".Length..], out var c) && c is Color color)
            return new SolidColorBrush(color);
        return TryResource(key, out var value) && value is IBrush brush ? brush : fallback;
    }

    /// <summary>The color of series (or slice) <paramref name="index"/>: its own color, else Brush.Chart.1…6, then round again.</summary>
    protected IBrush SeriesBrush(int index, SeriesStyle? style = null) =>
        Resource($"Brush.Chart.{(style?.Color ?? index) % 6 + 1}", Brushes.SteelBlue);

    protected IBrush SeriesBrush(ChartData data, int index) =>
        SeriesBrush(index, index < data.Series.Count ? data.Series[index].Style : null);

    protected IBrush TextBrush => Resource("Brush.Text.Secondary", Brushes.Gray);
    protected IBrush StrongTextBrush => Resource("Brush.Text.Primary", Brushes.Black);
    protected IBrush GridBrush => Resource("Brush.Separator", Brushes.LightGray);

    protected FormattedText Text(string text, IBrush? brush = null, bool bold = false, double sizeFactor = 1)
    {
        var family = TryResource("Font.UI", out var f) && f is FontFamily font ? font : TextElement.GetFontFamily(this);
        var size = TryResource("FontSize.Small", out var s) && s is double d ? d : 12;
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(family, weight: bold ? FontWeight.SemiBold : FontWeight.Normal), size * sizeFactor, brush ?? TextBrush);
    }

    /// <summary>A pen for a series' lines, dashed or dotted when its style says so.</summary>
    protected static Pen LinePen(IBrush brush, SeriesStyle style, double thickness = 2) => new(brush, thickness,
        style.Dash switch
        {
            LineDash.Dashed => new DashStyle([4, 3], 0),
            LineDash.Dotted => new DashStyle([1, 2.5], 0),
            _ => null,
        },
        lineCap: style.Dash == LineDash.Dotted ? PenLineCap.Round : PenLineCap.Flat, lineJoin: PenLineJoin.Round);

    public sealed override void Render(DrawingContext context)
    {
        var data = Current;
        if (Background is { } background)
            context.FillRectangle(background, new Rect(Bounds.Size));
        if (Bounds.Width < 40 || Bounds.Height < 40)
            return;
        // A transparent background: the whole area takes the pointer (tooltips).
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var top = 0.0;
        if (ShowTitle && !string.IsNullOrWhiteSpace(data.Title))
        {
            var title = Text(data.Title, StrongTextBrush, bold: true, sizeFactor: 1.25);
            context.DrawText(title, new Point(Math.Max(0, (Bounds.Width - title.Width) / 2), 0));
            top = title.Height + Gap;
        }
        if (IsEmpty(data))
        {
            var empty = Text(EmptyText);
            context.DrawText(empty, new Point((Bounds.Width - empty.Width) / 2, (Bounds.Height - empty.Height) / 2));
            return;
        }
        if (ShowsLegend(data))
            top += DrawLegend(context, data.Series.Select(s => s.Name).Concat(data.Bands.Select(b => b.Name)).ToList(), new Rect(0, top, Bounds.Width, Bounds.Height - top), data);
        RenderChart(context, data, new Rect(0, top, Bounds.Width, Math.Max(1, Bounds.Height - top)));
    }

    protected virtual bool IsEmpty(ChartData data) => data.Labels.Count == 0 || data.Range() is null;

    protected virtual bool ShowsLegend(ChartData data) => data.Series.Count + data.Bands.Count > 1;

    /// <summary>Draws the chart in <paramref name="area"/> (below the title and legend).</summary>
    protected abstract void RenderChart(DrawingContext context, ChartData data, Rect area);

    /// <summary>The legend: a swatch and the name of each series (then each band), wrapped; returns its height.</summary>
    protected double DrawLegend(DrawingContext context, IReadOnlyList<string> names, Rect area, ChartData? data = null)
    {
        double x = area.X, y = area.Y, rowHeight = 0;
        for (var i = 0; i < names.Count; i++)
        {
            var text = Text(names[i], StrongTextBrush);
            var width = 10 + 4 + text.Width + 12;
            if (x > area.X && x + width > area.Right)
            {
                x = area.X;
                y += rowHeight + 2;
            }
            var isBand = data is not null && i >= data.Series.Count;
            var brush = data is null ? SeriesBrush(i) : isBand ? BandBrush(i - data.Series.Count) : SeriesBrush(data, i);
            var swatch = new Rect(x, y + (text.Height - 10) / 2, 10, 10);
            if (data is not null && !isBand && data.Series[i].Style.Dash != LineDash.Solid)
                context.DrawLine(LinePen(brush, data.Series[i].Style), swatch.Center.WithX(swatch.Left), swatch.Center.WithX(swatch.Right));
            else
                context.FillRectangle(brush, swatch, 2);
            context.DrawText(text, new Point(x + 14, y));
            x += width;
            rowHeight = Math.Max(rowHeight, text.Height);
        }
        return y - area.Y + rowHeight + Gap;
    }

    /// <summary>A band's fill: a light tint of the palette, after the series' colors.</summary>
    protected IBrush BandBrush(int index) =>
        Resource($"Brush.Chart.{(index + 2) % 6 + 1}", Brushes.LightSteelBlue) is ISolidColorBrush solid
            ? new SolidColorBrush(solid.Color, 0.22)
            : Brushes.LightSteelBlue;

    // Tooltip: what's under the pointer (a label's values, a slice, a cell).
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
            .Select(s => (data.Series.Count > 1 ? s.Name + " " : "") + data.Format(s.Values[index]!.Value)));
}

/// <summary>
/// An axis chart (lines, areas, bars, combos): a value axis on the left with grid lines and an optional title, labels
/// below, shaded <see cref="ChartData.Bands"/> behind the values and <see cref="ChartData.References"/> lines over them.
/// </summary>
public abstract class AxisChart : ChartBase
{
    /// <summary>The area the values are drawn in (after <see cref="RenderChart"/> ran), for hit tests.</summary>
    protected Rect Plot { get; private set; }

    protected AxisScale Scale { get; private set; } = new(0, 1, 1);

    protected virtual bool IncludeZero => true;

    /// <summary>Series add up per label (stacked areas and bars): the axis covers the totals.</summary>
    protected virtual bool Stacked => false;

    protected sealed override void RenderChart(DrawingContext context, ChartData data, Rect area)
    {
        var (min, max) = (Stacked ? data.StackedRange() : data.Range()) ?? (0, 1);
        var labelHeight = Text("Ag").Height;
        var yTitle = string.IsNullOrWhiteSpace(data.YTitle) ? null : Text(data.YTitle);
        var xTitle = string.IsNullOrWhiteSpace(data.XTitle) ? null : Text(data.XTitle);
        var titleLeft = yTitle is null ? 0 : yTitle.Height + Gap;
        var bottom = labelHeight * 2 + Gap + (xTitle is null ? 0 : xTitle.Height + Gap);

        Scale = ChartMath.NiceScale(min, max, Math.Max(2, (int)((area.Height - bottom) / 40)), IncludeZero);
        var ticks = Scale.Ticks();
        var tickTexts = ticks.Select(t => Text(ChartMath.Short(t))).ToList();
        var left = titleLeft + tickTexts.Max(t => t.Width) + Gap;
        // Room below for the labels, clear of the lowest tick's text (which is centered on the bottom line).
        Plot = new Rect(area.X + left, area.Y + labelHeight / 2, Math.Max(1, area.Width - left - Gap), Math.Max(1, area.Height - bottom));

        var grid = new Pen(GridBrush, 1);
        for (var i = 0; i < ticks.Count; i++)
        {
            var y = Math.Round(Y(ticks[i])) + 0.5;
            context.DrawLine(grid, new Point(Plot.Left, y), new Point(Plot.Right, y));
            context.DrawText(tickTexts[i], new Point(Plot.Left - Gap - tickTexts[i].Width, y - tickTexts[i].Height / 2));
        }
        if (yTitle is not null)
        {
            // Turned a quarter left, centered on the plot.
            var at = new Point(area.X, Plot.Center.Y + yTitle.Width / 2);
            using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(at.X, at.Y)))
                context.DrawText(yTitle, new Point(0, 0));
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
        if (xTitle is not null)
            context.DrawText(xTitle, new Point(Plot.Center.X - xTitle.Width / 2, Plot.Bottom + labelHeight * 1.5 + Gap));

        for (var b = 0; b < data.Bands.Count; b++)
            DrawBand(context, data.Bands[b], BandBrush(b), n);
        RenderValues(context, data);
        foreach (var reference in data.References)
            DrawReference(context, reference, data);
    }

    private void DrawBand(DrawingContext context, ChartBand band, IBrush brush, int n)
    {
        // One filled shape per run of labels that have both ends.
        var run = new List<int>();
        for (var i = 0; i <= n; i++)
        {
            if (i < n && i < band.Low.Count && i < band.High.Count && band.Low[i] is { } && band.High[i] is { })
            {
                run.Add(i);
                continue;
            }
            if (run.Count > 0)
            {
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(X(run[0], n), Y(band.High[run[0]]!.Value)), true);
                    foreach (var j in run.Skip(1))
                        g.LineTo(new Point(X(j, n), Y(band.High[j]!.Value)));
                    foreach (var j in Enumerable.Reverse(run))
                        g.LineTo(new Point(X(j, n), Y(band.Low[j]!.Value)));
                    g.EndFigure(true);
                }
                context.DrawGeometry(brush, null, geometry);
                run.Clear();
            }
        }
    }

    private void DrawReference(DrawingContext context, ChartReference reference, ChartData data)
    {
        var y = Math.Round(Y(reference.Value)) + 0.5;
        var brush = StrongTextBrush;
        context.DrawLine(new Pen(brush, 1, new DashStyle([5, 3], 0)), new Point(Plot.Left, y), new Point(Plot.Right, y));
        var text = Text($"{reference.Name} {data.Format(reference.Value)}", brush);
        // At the left end: value labels sit at the right of the last bars and points.
        context.DrawText(text, new Point(Plot.Left + 2, y - text.Height - 1));
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

    /// <summary>A value's text above (or below, for negative) <paramref name="at"/>.</summary>
    protected void DrawValueLabel(DrawingContext context, ChartData data, double value, Point at)
    {
        var text = Text(data.Format(value), StrongTextBrush);
        var y = value >= 0 ? at.Y - text.Height - 2 : at.Y + 2;
        context.DrawText(text, new Point(at.X - text.Width / 2, Math.Max(Plot.Top - text.Height, y)));
    }

    /// <summary>Running totals per label for stacked kinds: the bottom and top of each series' part.</summary>
    protected static (double?[] Bottom, double?[] Top)[] StackParts(ChartData data)
    {
        var n = data.Labels.Count;
        var up = new double[n];
        var down = new double[n];
        var parts = new (double?[], double?[])[data.Series.Count];
        for (var s = 0; s < data.Series.Count; s++)
        {
            var bottom = new double?[n];
            var top = new double?[n];
            for (var i = 0; i < n; i++)
            {
                if (i >= data.Series[s].Values.Count || data.Series[s].Values[i] is not { } v || !double.IsFinite(v))
                    continue;
                if (v >= 0)
                {
                    bottom[i] = up[i];
                    up[i] += v;
                    top[i] = up[i];
                }
                else
                {
                    top[i] = down[i];
                    down[i] += v;
                    bottom[i] = down[i];
                }
            }
            parts[s] = (bottom, top);
        }
        return parts;
    }
}

/// <summary>
/// Lines over the labels, one per series (a burndown, run durations over time): <c>&lt;c:LineChart Data="{Binding Burndown}" /&gt;</c>.
/// A null value leaves a gap. Values are marked with dots when there's room (or when a series asks for markers); a series
/// can be dashed or dotted and show its values. <see cref="IsArea"/> fills under the lines, <see cref="IsStacked"/>
/// piles the series up.
/// </summary>
public class LineChart : AxisChart
{
    public static readonly StyledProperty<bool> IsZeroBasedProperty =
        AvaloniaProperty.Register<LineChart, bool>(nameof(IsZeroBased), true);

    public static readonly StyledProperty<bool> IsAreaProperty =
        AvaloniaProperty.Register<LineChart, bool>(nameof(IsArea));

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<LineChart, bool>(nameof(IsStacked));

    static LineChart() => AffectsRender<LineChart>(IsZeroBasedProperty, IsAreaProperty, IsStackedProperty);

    /// <summary>True (default): the axis starts at 0. False: it hugs the data (e.g. durations around 5 minutes).</summary>
    public bool IsZeroBased { get => GetValue(IsZeroBasedProperty); set => SetValue(IsZeroBasedProperty, value); }

    /// <summary>Fill the area under each line.</summary>
    public bool IsArea { get => GetValue(IsAreaProperty); set => SetValue(IsAreaProperty, value); }

    /// <summary>Each series sits on top of the ones before it (with <see cref="IsArea"/>: a stacked area chart).</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    protected override bool IncludeZero => IsZeroBased || IsArea || IsStacked;
    protected override bool Stacked => IsStacked;

    protected override double X(int index, int count) =>
        count <= 1 ? Plot.Center.X : Plot.Left + index * Plot.Width / (count - 1);

    protected override void RenderValues(DrawingContext context, ChartData data)
    {
        var n = data.Labels.Count;
        var dots = Plot.Width / Math.Max(1, n) >= 12;
        var stack = IsStacked ? StackParts(data) : null;
        var zero = Y(Math.Clamp(0, Scale.Min, Scale.Max));

        // Areas first (back to front), then the lines on top.
        if (IsArea)
        {
            for (var s = data.Series.Count - 1; s >= 0; s--)
            {
                var brush = SeriesBrush(data, s);
                var fill = brush is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, IsStacked ? 0.55 : 0.25) : brush;
                double? Top(int i) => stack is null ? (i < data.Series[s].Values.Count ? data.Series[s].Values[i] : null) : stack[s].Top[i];
                double? Bottom(int i) => stack?[s].Bottom[i];
                DrawArea(context, fill, n, Top, Bottom, zero);
            }
        }
        for (var s = 0; s < data.Series.Count; s++)
        {
            var style = data.Series[s].Style;
            var brush = SeriesBrush(data, s);
            var pen = LinePen(brush, style);
            Point? previous = null;
            var values = data.Series[s].Values;
            for (var i = 0; i < n; i++)
            {
                var value = stack is not null ? (values.Count > i && values[i] is not null ? stack[s].Top[i] : null) : i < values.Count ? values[i] : null;
                if (value is not { } v || !double.IsFinite(v))
                {
                    previous = null; // a gap
                    continue;
                }
                var point = new Point(X(i, n), Y(v));
                if (previous is { } from)
                    context.DrawLine(pen, from, point);
                if (style.Markers || (dots && !IsArea) || n == 1)
                    context.DrawEllipse(brush, null, point, style.Markers ? 3.5 : 3, style.Markers ? 3.5 : 3);
                if (style.ValueLabels && values[i] is { } own)
                    DrawValueLabel(context, data, own, point);
                previous = point;
            }
        }
    }

    private void DrawArea(DrawingContext context, IBrush fill, int n, Func<int, double?> top, Func<int, double?> bottom, double zero)
    {
        var run = new List<int>();
        for (var i = 0; i <= n; i++)
        {
            if (i < n && top(i) is { } t && double.IsFinite(t))
            {
                run.Add(i);
                continue;
            }
            if (run.Count > 0)
            {
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(X(run[0], n), Y(top(run[0])!.Value)), true);
                    foreach (var j in run.Skip(1))
                        g.LineTo(new Point(X(j, n), Y(top(j)!.Value)));
                    foreach (var j in Enumerable.Reverse(run))
                        g.LineTo(new Point(X(j, n), bottom(j) is { } b ? Y(b) : zero));
                    g.EndFigure(true);
                }
                context.DrawGeometry(fill, null, geometry);
                run.Clear();
            }
        }
    }
}

/// <summary>A <see cref="LineChart"/> filled under its lines: <c>&lt;c:AreaChart Data="…" IsStacked="True" /&gt;</c>.</summary>
public class AreaChart : LineChart
{
    public AreaChart() => IsArea = true;
}

/// <summary>
/// Bars per label, side by side for several series (velocity: committed vs done per sprint), or piled up with
/// <see cref="IsStacked"/>: <c>&lt;c:BarChart Data="{Binding Velocity}" /&gt;</c>. Negative values go below the zero line.
/// </summary>
public class BarChart : AxisChart
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<BarChart, bool>(nameof(IsStacked));

    static BarChart() => AffectsRender<BarChart>(IsStackedProperty);

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    protected override bool Stacked => IsStacked;

    protected override double X(int index, int count) => Plot.Left + (index + 0.5) * Plot.Width / Math.Max(1, count);

    protected override void RenderValues(DrawingContext context, ChartData data) =>
        DrawBars(context, data, Enumerable.Range(0, data.Series.Count).ToList(), IsStacked);

    /// <summary>Bars for the given series (all of them, or a combo chart's bar series).</summary>
    protected void DrawBars(DrawingContext context, ChartData data, IReadOnlyList<int> series, bool stacked)
    {
        var n = data.Labels.Count;
        var group = Plot.Width / Math.Max(1, n);
        var k = stacked ? 1 : Math.Max(1, series.Count);
        var bar = Math.Max(1, group * 0.7 / k);
        var zero = Y(Math.Clamp(0, Scale.Min, Scale.Max));
        var stack = stacked ? StackParts(data) : null;
        for (var slot = 0; slot < series.Count; slot++)
        {
            var s = series[slot];
            var brush = SeriesBrush(data, s);
            var values = data.Series[s].Values;
            for (var i = 0; i < n && i < values.Count; i++)
            {
                if (values[i] is not { } value || !double.IsFinite(value))
                    continue;
                var x = X(i, n) - bar * k / 2 + (stacked ? 0 : slot) * bar;
                double from = stack is null ? zero : Y(stack[s].Bottom[i]!.Value), to = stack is null ? Y(value) : Y(stack[s].Top[i]!.Value);
                var rect = new Rect(x + 0.5, Math.Min(from, to), Math.Max(1, bar - 1), Math.Max(1, Math.Abs(from - to)));
                context.FillRectangle(brush, rect, (float)Math.Min(2, bar / 4));
                if (data.Series[s].Style.ValueLabels)
                    DrawValueLabel(context, data, value, new Point(rect.Center.X, value >= 0 ? rect.Top : rect.Bottom));
            }
        }
    }
}

/// <summary>
/// Bars and lines on one axis (capacity as bars, committed and completed as lines): each series' <see cref="SeriesStyle.Kind"/>
/// says which. <c>&lt;c:ComboChart Data="…" /&gt;</c>.
/// </summary>
public class ComboChart : BarChart
{
    protected override void RenderValues(DrawingContext context, ChartData data)
    {
        var n = data.Labels.Count;
        var bars = Enumerable.Range(0, data.Series.Count).Where(s => data.Series[s].Style.Kind == SeriesKind.Bar).ToList();
        var lines = Enumerable.Range(0, data.Series.Count).Where(s => data.Series[s].Style.Kind != SeriesKind.Bar).ToList();
        if (bars.Count == 0 && lines.Count > 0)
        {
            bars.Add(lines[0]); // a combo needs something to be bars: the first series
            lines.RemoveAt(0);
        }
        DrawBars(context, data, bars, stacked: false);
        var zero = Y(Math.Clamp(0, Scale.Min, Scale.Max));
        foreach (var s in lines)
        {
            var style = data.Series[s].Style;
            var brush = SeriesBrush(data, s);
            var values = data.Series[s].Values;
            if (style.Kind == SeriesKind.Area && brush is ISolidColorBrush solid)
            {
                var points = Enumerable.Range(0, Math.Min(n, values.Count)).Where(i => values[i] is not null).Select(i => new Point(X(i, n), Y(values[i]!.Value))).ToList();
                if (points.Count > 1)
                {
                    var geometry = new StreamGeometry();
                    using (var g = geometry.Open())
                    {
                        g.BeginFigure(new Point(points[0].X, zero), true);
                        foreach (var p in points)
                            g.LineTo(p);
                        g.LineTo(new Point(points[^1].X, zero));
                        g.EndFigure(true);
                    }
                    context.DrawGeometry(new SolidColorBrush(solid.Color, 0.25), null, geometry);
                }
            }
            var pen = LinePen(brush, style);
            Point? previous = null;
            for (var i = 0; i < n; i++)
            {
                if (i >= values.Count || values[i] is not { } v || !double.IsFinite(v))
                {
                    previous = null;
                    continue;
                }
                var point = new Point(X(i, n), Y(v));
                if (previous is { } from)
                    context.DrawLine(pen, from, point);
                context.DrawEllipse(brush, null, point, 3, 3);
                if (style.ValueLabels)
                    DrawValueLabel(context, data, v, point);
                previous = point;
            }
        }
    }
}

/// <summary>
/// Bars lying down, a row per label (long names: epics, people), side by side or <see cref="IsStacked"/>:
/// <c>&lt;c:HorizontalBarChart Data="…" /&gt;</c>.
/// </summary>
public class HorizontalBarChart : ChartBase
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<HorizontalBarChart, bool>(nameof(IsStacked));

    static HorizontalBarChart() => AffectsRender<HorizontalBarChart>(IsStackedProperty);

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    private Rect _plot;
    private int _rows;

    protected override void RenderChart(DrawingContext context, ChartData data, Rect area)
    {
        var (min, max) = (IsStacked ? data.StackedRange() : data.Range()) ?? (0, 1);
        var labels = data.Labels.Select(l => Text(l)).ToList();
        var left = Math.Min(area.Width * 0.4, labels.Max(l => l.Width)) + Gap;
        var tickHeight = Text("Ag").Height;
        var xTitle = string.IsNullOrWhiteSpace(data.YTitle) ? null : Text(data.YTitle);
        _plot = new Rect(area.X + left, area.Y, Math.Max(1, area.Width - left - Gap * 2), Math.Max(1, area.Height - tickHeight - Gap - (xTitle is null ? 0 : xTitle.Height + Gap)));
        var scale = ChartMath.NiceScale(min, max, Math.Max(2, (int)(_plot.Width / 70)));
        double X(double v) => _plot.Left + scale.Fraction(v) * _plot.Width;

        var grid = new Pen(GridBrush, 1);
        foreach (var t in scale.Ticks())
        {
            var x = Math.Round(X(t)) + 0.5;
            context.DrawLine(grid, new Point(x, _plot.Top), new Point(x, _plot.Bottom));
            var text = Text(ChartMath.Short(t));
            context.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, area.X, area.Right - text.Width), _plot.Bottom + Gap / 2));
        }
        if (xTitle is not null)
            context.DrawText(xTitle, new Point(_plot.Center.X - xTitle.Width / 2, _plot.Bottom + tickHeight + Gap));

        _rows = data.Labels.Count;
        var row = _plot.Height / Math.Max(1, _rows);
        var k = IsStacked ? 1 : Math.Max(1, data.Series.Count);
        var bar = Math.Max(1, row * 0.7 / k);
        var stack = IsStacked ? AxisChartStack(data) : null;
        var zero = X(Math.Clamp(0, scale.Min, scale.Max));
        for (var i = 0; i < _rows; i++)
        {
            var center = _plot.Top + (i + 0.5) * row;
            var label = labels[i];
            using (context.PushClip(new Rect(area.X, center - label.Height / 2, left - Gap, label.Height)))
                context.DrawText(label, new Point(area.X + left - Gap - label.Width, center - label.Height / 2));
            for (var s = 0; s < data.Series.Count; s++)
            {
                var values = data.Series[s].Values;
                if (i >= values.Count || values[i] is not { } v || !double.IsFinite(v))
                    continue;
                var y = center - bar * k / 2 + (IsStacked ? 0 : s) * bar;
                double from = stack is null ? zero : X(stack[s].Bottom[i]!.Value), to = stack is null ? X(v) : X(stack[s].Top[i]!.Value);
                var rect = new Rect(Math.Min(from, to), y + 0.5, Math.Max(1, Math.Abs(to - from)), Math.Max(1, bar - 1));
                context.FillRectangle(SeriesBrush(data, s), rect, (float)Math.Min(2, bar / 4));
                if (data.Series[s].Style.ValueLabels)
                {
                    var text = Text(data.Format(v), StrongTextBrush);
                    context.DrawText(text, new Point(rect.Right + 3, rect.Center.Y - text.Height / 2));
                }
            }
        }
    }

    private static (double?[] Bottom, double?[] Top)[] AxisChartStack(ChartData data)
    {
        var n = data.Labels.Count;
        var up = new double[n];
        var parts = new (double?[], double?[])[data.Series.Count];
        for (var s = 0; s < data.Series.Count; s++)
        {
            var bottom = new double?[n];
            var top = new double?[n];
            for (var i = 0; i < n; i++)
            {
                if (i >= data.Series[s].Values.Count || data.Series[s].Values[i] is not { } v || !double.IsFinite(v))
                    continue;
                bottom[i] = up[i];
                up[i] += v;
                top[i] = up[i];
            }
            parts[s] = (bottom, top);
        }
        return parts;
    }

    protected override string? ToolTipAt(ChartData data, Point point)
    {
        if (_rows == 0 || !_plot.Inflate(Gap).Contains(point) && point.X > _plot.Right)
            return null;
        var i = (int)((point.Y - _plot.Top) / (_plot.Height / _rows));
        return i >= 0 && i < _rows ? ValuesAt(data, i) : null;
    }
}

/// <summary>
/// Points on two number axes (estimate vs time spent, points vs cycle time): X from <see cref="ChartData.X"/>, else
/// the labels when they're numbers, else 1, 2, 3…; one color per series. <c>&lt;c:ScatterChart Data="…" /&gt;</c>.
/// </summary>
public class ScatterChart : ChartBase
{
    private readonly List<(Point At, string Tip)> _points = [];

    protected override void RenderChart(DrawingContext context, ChartData data, Rect area)
    {
        var xs = XValues(data);
        var (minY, maxY) = data.Range() ?? (0, 1);
        var yScale = ChartMath.NiceScale(minY, maxY, Math.Max(2, (int)(area.Height / 50)), includeZero: false);
        var xScale = ChartMath.NiceScale(xs.Min(), xs.Max(), Math.Max(2, (int)(area.Width / 70)), includeZero: false);
        var yTicks = yScale.Ticks().Select(t => (t, Text(ChartMath.Short(t)))).ToList();
        var labelHeight = Text("Ag").Height;
        var left = yTicks.Max(t => t.Item2.Width) + Gap;
        var xTitle = string.IsNullOrWhiteSpace(data.XTitle) ? null : Text(data.XTitle);
        var plot = new Rect(area.X + left, area.Y + labelHeight / 2, Math.Max(1, area.Width - left - Gap),
            Math.Max(1, area.Height - labelHeight * 2 - Gap - (xTitle is null ? 0 : xTitle.Height + Gap)));
        double X(double v) => plot.Left + xScale.Fraction(v) * plot.Width;
        double Y(double v) => plot.Bottom - yScale.Fraction(v) * plot.Height;

        var grid = new Pen(GridBrush, 1);
        foreach (var (t, text) in yTicks)
        {
            var y = Math.Round(Y(t)) + 0.5;
            context.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            context.DrawText(text, new Point(plot.Left - Gap - text.Width, y - text.Height / 2));
        }
        foreach (var t in xScale.Ticks())
        {
            var x = Math.Round(X(t)) + 0.5;
            context.DrawLine(grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var text = Text(ChartMath.Short(t));
            context.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, area.X, area.Right - text.Width), plot.Bottom + labelHeight / 2));
        }
        if (xTitle is not null)
            context.DrawText(xTitle, new Point(plot.Center.X - xTitle.Width / 2, plot.Bottom + labelHeight * 1.5 + Gap));

        _points.Clear();
        for (var s = 0; s < data.Series.Count; s++)
        {
            var brush = SeriesBrush(data, s);
            var values = data.Series[s].Values;
            for (var i = 0; i < xs.Count && i < values.Count; i++)
            {
                if (values[i] is not { } v || !double.IsFinite(v))
                    continue;
                var at = new Point(X(xs[i]), Y(v));
                context.DrawEllipse(brush, null, at, 4, 4);
                var name = i < data.Labels.Count && !double.TryParse(data.Labels[i], NumberStyles.Float, CultureInfo.CurrentCulture, out _) ? data.Labels[i] + ": " : "";
                _points.Add((at, $"{name}{(data.Series.Count > 1 ? data.Series[s].Name + " " : "")}({ChartMath.Short(xs[i])}, {data.Format(v)})"));
            }
        }
    }

    private static IReadOnlyList<double> XValues(ChartData data)
    {
        if (data.X is { Count: > 0 } x)
            return x;
        var parsed = data.Labels.Select(l => double.TryParse(l, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) ? v : (double?)null).ToList();
        return parsed.All(p => p is not null) ? parsed.Select(p => p!.Value).ToList() : Enumerable.Range(1, data.Labels.Count).Select(i => (double)i).ToList();
    }

    protected override string? ToolTipAt(ChartData data, Point point)
    {
        if (_points.Count == 0)
            return null;
        var nearest = _points.MinBy(p => Point.Distance(p.At, point));
        return Point.Distance(nearest.At, point) <= 10 ? nearest.Tip : null;
    }
}

/// <summary>How a heatmap colors its cells.</summary>
public enum HeatmapColors
{
    /// <summary>Light to strong shades of the first chart color, by value.</summary>
    Shades,

    /// <summary>Green, amber, red by value (low to high): a risk matrix.</summary>
    Traffic,
}

/// <summary>
/// A grid of colored cells: a row per series (its name on the left), a column per label, colored by value, with the
/// value or <see cref="ChartData.CellText"/> written in it (a probability × impact risk matrix, activity by day and
/// hour). <c>&lt;c:HeatmapChart Data="…" Colors="Traffic" /&gt;</c>. The first series is the top row.
/// </summary>
public class HeatmapChart : ChartBase
{
    public static readonly StyledProperty<HeatmapColors> ColorsProperty =
        AvaloniaProperty.Register<HeatmapChart, HeatmapColors>(nameof(Colors));

    static HeatmapChart() => AffectsRender<HeatmapChart>(ColorsProperty);

    public HeatmapColors Colors { get => GetValue(ColorsProperty); set => SetValue(ColorsProperty, value); }

    private Rect _grid;

    protected override bool ShowsLegend(ChartData data) => false;

    protected override void RenderChart(DrawingContext context, ChartData data, Rect area)
    {
        var rows = data.Series.Count;
        var cols = data.Labels.Count;
        var rowNames = data.Series.Select(s => Text(s.Name)).ToList();
        var labelHeight = Text("Ag").Height;
        var left = Math.Min(area.Width * 0.3, rowNames.Max(r => r.Width)) + Gap;
        var xTitle = string.IsNullOrWhiteSpace(data.XTitle) ? null : Text(data.XTitle);
        var yTitle = string.IsNullOrWhiteSpace(data.YTitle) ? null : Text(data.YTitle);
        var titleLeft = yTitle is null ? 0 : yTitle.Height + Gap;
        _grid = new Rect(area.X + titleLeft + left, area.Y, Math.Max(1, area.Width - titleLeft - left - Gap),
            Math.Max(1, area.Height - labelHeight - Gap - (xTitle is null ? 0 : xTitle.Height + Gap)));
        var cw = _grid.Width / Math.Max(1, cols);
        var ch = _grid.Height / Math.Max(1, rows);
        var (min, max) = data.Range() ?? (0, 1);
        var line = Resource("Brush.Bg.Editor", Brushes.White);

        for (var r = 0; r < rows; r++)
        {
            var name = rowNames[r];
            context.DrawText(name, new Point(_grid.Left - Gap - name.Width, _grid.Top + (r + 0.5) * ch - name.Height / 2));
            for (var c = 0; c < cols; c++)
            {
                var value = c < data.Series[r].Values.Count ? data.Series[r].Values[c] : null;
                var cell = new Rect(_grid.Left + c * cw, _grid.Top + r * ch, cw, ch);
                var fill = value is { } v ? CellBrush(max > min ? (v - min) / (max - min) : 1) : Resource("Brush.Bg.Hover", Brushes.WhiteSmoke);
                context.FillRectangle(fill, cell.Deflate(1), 3);
                context.DrawRectangle(null, new Pen(line, 1), cell);
                var written = data.CellText is { } texts && r < texts.Count && c < texts[r].Count ? texts[r][c] : value is { } w ? data.Format(w) : null;
                if (!string.IsNullOrEmpty(written))
                {
                    var text = Text(written, StrongTextBrush);
                    using (context.PushClip(cell.Deflate(2)))
                        context.DrawText(text, new Point(cell.Center.X - Math.Min(text.Width, cell.Width - 4) / 2, cell.Center.Y - text.Height / 2));
                }
            }
        }
        for (var c = 0; c < cols; c++)
        {
            var text = Text(data.Labels[c]);
            using (context.PushClip(new Rect(_grid.Left + c * cw, _grid.Bottom, cw, labelHeight + Gap)))
                context.DrawText(text, new Point(_grid.Left + (c + 0.5) * cw - text.Width / 2, _grid.Bottom + Gap / 2));
        }
        if (xTitle is not null)
            context.DrawText(xTitle, new Point(_grid.Center.X - xTitle.Width / 2, _grid.Bottom + labelHeight + Gap));
        if (yTitle is not null)
        {
            var at = new Point(area.X, _grid.Center.Y + yTitle.Width / 2);
            using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(at.X, at.Y)))
                context.DrawText(yTitle, new Point(0, 0));
        }
    }

    private IBrush CellBrush(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (Colors == HeatmapColors.Traffic)
        {
            var (from, to, t) = fraction < 0.5
                ? (Resource("Brush.Status.Success", Brushes.Green), Resource("Brush.Status.Warning", Brushes.Orange), fraction * 2)
                : (Resource("Brush.Status.Warning", Brushes.Orange), Resource("Brush.Status.Error", Brushes.Red), (fraction - 0.5) * 2);
            return new SolidColorBrush(Mix(Solid(from), Solid(to), t), 0.75);
        }
        return new SolidColorBrush(Solid(SeriesBrush(0)), 0.15 + 0.75 * fraction);
    }

    private static Color Solid(IBrush brush) => brush is ISolidColorBrush s ? s.Color : Avalonia.Media.Colors.Gray;

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    protected override string? ToolTipAt(ChartData data, Point point)
    {
        if (!_grid.Contains(point) || data.Labels.Count == 0 || data.Series.Count == 0)
            return null;
        var c = (int)((point.X - _grid.Left) / (_grid.Width / data.Labels.Count));
        var r = (int)((point.Y - _grid.Top) / (_grid.Height / data.Series.Count));
        if (r < 0 || r >= data.Series.Count || c < 0 || c >= data.Labels.Count)
            return null;
        var value = c < data.Series[r].Values.Count ? data.Series[r].Values[c] : null;
        var text = data.CellText is { } texts && r < texts.Count && c < texts[r].Count ? texts[r][c] : null;
        return $"{data.Series[r].Name} · {data.Labels[c]}: {(value is { } v ? data.Format(v) : "—")}{(string.IsNullOrEmpty(text) ? "" : "\n" + text)}";
    }
}

/// <summary>
/// One value out of a whole as a ring (sprint progress, capacity used): the first value of the first series out of
/// <see cref="ChartData.Max"/> (default 100), the value written in the middle. <c>&lt;c:GaugeChart Data="…" /&gt;</c>.
/// </summary>
public class GaugeChart : ChartBase
{
    protected override bool ShowsLegend(ChartData data) => false;

    protected override bool IsEmpty(ChartData data) => data.Series.Count == 0 || data.Series[0].Values.FirstOrDefault() is null;

    protected override void RenderChart(DrawingContext context, ChartData data, Rect area)
    {
        var value = data.Series[0].Values[0]!.Value;
        var max = data.Max is > 0 and var m ? m : 100;
        var fraction = Math.Clamp(value / max, 0, 1);
        var caption = Text(data.Labels.FirstOrDefault() ?? data.Series[0].Name);
        var radius = Math.Max(8, Math.Min(area.Width, area.Height - caption.Height - Gap) / 2 - Gap);
        var center = new Point(area.Center.X, area.Y + Gap + radius);
        var thickness = Math.Max(6, radius * 0.18);
        const double start = 135, sweep = 270; // from bottom left round to bottom right

        context.DrawGeometry(null, new Pen(Resource("Brush.Bg.Hover", Brushes.WhiteSmoke), thickness, lineCap: PenLineCap.Round), Arc(center, radius, start, sweep));
        if (fraction > 0)
            context.DrawGeometry(null, new Pen(SeriesBrush(data, 0), thickness, lineCap: PenLineCap.Round), Arc(center, radius, start, sweep * fraction));

        var big = Text(data.Format(value), StrongTextBrush, bold: true, sizeFactor: Math.Clamp(radius / 30, 1.2, 3));
        context.DrawText(big, new Point(center.X - big.Width / 2, center.Y - big.Height / 2));
        var of = Text($"of {data.Format(max)}");
        context.DrawText(of, new Point(center.X - of.Width / 2, center.Y + big.Height / 2));
        context.DrawText(caption, new Point(center.X - caption.Width / 2, center.Y + radius * 0.85));
    }

    // Angles clockwise from 12 o'clock, like the pie.
    private static StreamGeometry Arc(Point center, double radius, double start, double sweep)
    {
        Point At(double angle) => new(center.X + radius * Math.Sin(angle * Math.PI / 180), center.Y - radius * Math.Cos(angle * Math.PI / 180));
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(At(start), isFilled: false);
        g.ArcTo(At(start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise);
        g.EndFigure(false);
        return geometry;
    }

    protected override string? ToolTipAt(ChartData data, Point point) =>
        $"{data.Series[0].Name}: {data.Format(data.Series[0].Values[0]!.Value)} of {data.Format(data.Max ?? 100)}";
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
        var names = _slices.Select(s => $"{Label(data, s.Index)} — {data.Format(s.Value)} ({s.Fraction:P0})").ToList();
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
            context.DrawEllipse(Background ?? Resource("Brush.Bg.Editor", Brushes.White), null, _center, _radius * 0.55, _radius * 0.55);

        // The legend, in slice order, in the slices' colors.
        var y = area.Y + Math.Max(0, (area.Height - names.Count * (Text("Ag").Height + 2)) / 2);
        for (var i = 0; i < _slices.Count; i++)
        {
            var text = Text(names[i], StrongTextBrush);
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
            ? $"{Label(data, slice.Index)}: {data.Format(slice.Value)} ({slice.Fraction:P0})"
            : null;
    }
}

/// <summary>
/// Any chart by <see cref="Kind"/>, so a page can switch between kinds (a report's "chart type" picker, the chart
/// maker): <c>&lt;c:Chart Kind="{Binding Kind}" Data="{Binding Data}" /&gt;</c>. Holds the chart for that kind.
/// </summary>
public class Chart : Decorator
{
    public static readonly StyledProperty<ChartKind> KindProperty =
        AvaloniaProperty.Register<Chart, ChartKind>(nameof(Kind));

    public static readonly StyledProperty<ChartData?> DataProperty =
        AvaloniaProperty.Register<Chart, ChartData?>(nameof(Data));

    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<Chart, string>(nameof(EmptyText), "No data yet");

    public static readonly StyledProperty<HeatmapColors> HeatmapColorsProperty =
        AvaloniaProperty.Register<Chart, HeatmapColors>(nameof(HeatmapColors));

    public ChartKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public ChartData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public HeatmapColors HeatmapColors { get => GetValue(HeatmapColorsProperty); set => SetValue(HeatmapColorsProperty, value); }

    public Chart() => Child = Create(Kind, HeatmapColors.Shades);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == HeatmapColorsProperty)
            Child = Create(Kind, HeatmapColors);
        if (Child is ChartBase chart)
        {
            chart.Data = Data;
            chart.EmptyText = EmptyText;
        }
    }

    /// <summary>The chart control for a kind (also used for images).</summary>
    public static ChartBase Create(ChartKind kind, HeatmapColors heatmapColors = HeatmapColors.Shades) => kind switch
    {
        ChartKind.Area => new AreaChart(),
        ChartKind.StackedArea => new AreaChart { IsStacked = true },
        ChartKind.Bar => new BarChart(),
        ChartKind.StackedBar => new BarChart { IsStacked = true },
        ChartKind.HorizontalBar => new HorizontalBarChart(),
        ChartKind.StackedHorizontalBar => new HorizontalBarChart { IsStacked = true },
        ChartKind.Combo => new ComboChart(),
        ChartKind.Scatter => new ScatterChart(),
        ChartKind.Pie => new PieChart(),
        ChartKind.Donut => new PieChart { IsDonut = true },
        ChartKind.Heatmap => new HeatmapChart { Colors = heatmapColors },
        ChartKind.Gauge => new GaugeChart(),
        _ => new LineChart(),
    };
}
