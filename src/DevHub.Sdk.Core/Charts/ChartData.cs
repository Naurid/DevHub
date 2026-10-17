namespace DevHub.Sdk.Core.Charts;

/// <summary>One named series of values, one per label of its <see cref="ChartData"/> (null: no value there, a gap).</summary>
public sealed record ChartSeries(string Name, IReadOnlyList<double?> Values)
{
    /// <summary>How it's drawn: line dashes, markers, value labels, a fixed color, bars or a line in a combo chart.</summary>
    public SeriesStyle Style { get; init; } = SeriesStyle.Default;
}

/// <summary>Solid, dashed or dotted lines (a forecast is often dashed).</summary>
public enum LineDash
{
    Solid,
    Dashed,
    Dotted,
}

/// <summary>What a series is in a <see cref="ChartKind.Combo"/> chart (other kinds draw every series their way).</summary>
public enum SeriesKind
{
    Line,
    Bar,
    Area,
}

/// <summary>How one series looks. <see cref="Color"/> is a palette index (0–5: Brush.Chart.1…6); null = by position.</summary>
public sealed record SeriesStyle
{
    public static SeriesStyle Default { get; } = new();

    public LineDash Dash { get; init; } = LineDash.Solid;
    public bool Markers { get; init; }
    public bool ValueLabels { get; init; }
    public int? Color { get; init; }
    public SeriesKind Kind { get; init; } = SeriesKind.Line;
}

/// <summary>A shaded range between two values per label (a forecast between pessimistic and optimistic).</summary>
public sealed record ChartBand(string Name, IReadOnlyList<double?> Low, IReadOnlyList<double?> High);

/// <summary>A horizontal line across an axis chart at <see cref="Value"/> (a target, an average, a capacity).</summary>
public sealed record ChartReference(string Name, double Value);

/// <summary>
/// What a chart shows (the Sdk's charts: <c>Chart</c> with a <see cref="ChartKind"/>, or LineChart, BarChart, PieChart…):
/// labels along the X axis (or the slices of a pie, the columns of a heatmap) and one or more series with a value per
/// label (a heatmap's rows). Built by a plugin's .Core (e.g. a burndown from issue history), so it has no UI types.
/// A pie and a gauge use the first series. Everything after <see cref="Series"/> is optional.
/// </summary>
public sealed record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series)
{
    public static ChartData Empty { get; } = new([], []);

    /// <summary>A title above the chart (exported images show it; on screen the page usually has its own heading).</summary>
    public string? Title { get; init; }

    /// <summary>The name of the label axis ("Sprint") and of the value axis ("Story points").</summary>
    public string? XTitle { get; init; }
    public string? YTitle { get; init; }

    /// <summary>After every value in labels and tooltips ("%", " pts", " d").</summary>
    public string ValueSuffix { get; init; } = "";

    /// <summary>Shaded ranges (axis charts).</summary>
    public IReadOnlyList<ChartBand> Bands { get; init; } = [];

    /// <summary>Horizontal lines (axis charts).</summary>
    public IReadOnlyList<ChartReference> References { get; init; } = [];

    /// <summary>A number per label for a scatter chart's X axis (null: 1, 2, 3… or the labels when they're numbers).</summary>
    public IReadOnlyList<double>? X { get; init; }

    /// <summary>A heatmap's text per cell, by series (row) then label (column), e.g. the risks in a risk matrix cell.</summary>
    public IReadOnlyList<IReadOnlyList<string?>>? CellText { get; init; }

    /// <summary>A gauge's full scale (the first value out of this); null = 100.</summary>
    public double? Max { get; init; }

    /// <summary>A value with <see cref="ValueSuffix"/>, short ("1.2k pts").</summary>
    public string Format(double value) => ChartMath.Short(value) + ValueSuffix;

    /// <summary>One series: label → value.</summary>
    public static ChartData Of(string name, IEnumerable<(string Label, double Value)> points)
    {
        var list = points.ToList();
        return new ChartData(list.Select(p => p.Label).ToList(), [new ChartSeries(name, list.Select(p => (double?)p.Value).ToList())]);
    }

    /// <summary>The smallest and largest value of every series, band and reference line (null when there are none).</summary>
    public (double Min, double Max)? Range()
    {
        var values = Series.SelectMany(s => s.Values)
            .Concat(Bands.SelectMany(b => b.Low.Concat(b.High)))
            .OfType<double>()
            .Concat(References.Select(r => r.Value))
            .Where(double.IsFinite).ToList();
        return values.Count == 0 ? null : (values.Min(), values.Max());
    }

    /// <summary>The range when series are stacked: the lowest and highest running total per label (stacked bars and areas).</summary>
    public (double Min, double Max)? StackedRange()
    {
        if (Labels.Count == 0 || Series.Count == 0)
            return null;
        double min = 0, max = 0;
        var any = false;
        for (var i = 0; i < Labels.Count; i++)
        {
            double up = 0, down = 0;
            foreach (var s in Series)
            {
                if (i >= s.Values.Count || s.Values[i] is not { } v || !double.IsFinite(v))
                    continue;
                any = true;
                if (v >= 0) up += v; else down += v;
            }
            max = Math.Max(max, up);
            min = Math.Min(min, down);
        }
        foreach (var r in References)
        {
            max = Math.Max(max, r.Value);
            min = Math.Min(min, r.Value);
        }
        return any ? (min, max) : null;
    }

    /// <summary>The chart as a table: a column of labels, then a column per series (for CSV and copying).</summary>
    public Tables.TableData ToTable(string labelHeader = "")
    {
        var headers = new List<string> { string.IsNullOrEmpty(labelHeader) ? XTitle ?? "" : labelHeader };
        headers.AddRange(Series.Select(s => s.Name));
        var rows = Labels.Select((label, i) => (IReadOnlyList<string>)
            [label, .. Series.Select(s => i < s.Values.Count && s.Values[i] is { } v ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : "")]).ToList();
        return new Tables.TableData(headers, rows) { Title = Title };
    }
}

/// <summary>The kinds of chart the Sdk draws (the <c>Chart</c> control switches between them).</summary>
public enum ChartKind
{
    Line,
    Area,
    StackedArea,
    Bar,
    StackedBar,
    HorizontalBar,
    StackedHorizontalBar,
    Combo,
    Scatter,
    Pie,
    Donut,
    Heatmap,
    Gauge,
}

/// <summary>Names and fitting kinds, for a "chart type" picker.</summary>
public static class ChartKinds
{
    public static string Name(ChartKind kind) => kind switch
    {
        ChartKind.Line => "Line",
        ChartKind.Area => "Area",
        ChartKind.StackedArea => "Stacked area",
        ChartKind.Bar => "Bars",
        ChartKind.StackedBar => "Stacked bars",
        ChartKind.HorizontalBar => "Horizontal bars",
        ChartKind.StackedHorizontalBar => "Stacked horizontal bars",
        ChartKind.Combo => "Bars and lines",
        ChartKind.Scatter => "Scatter",
        ChartKind.Pie => "Pie",
        ChartKind.Donut => "Donut",
        ChartKind.Heatmap => "Heatmap",
        ChartKind.Gauge => "Gauge",
        _ => kind.ToString(),
    };

    /// <summary>
    /// The kinds that can show <paramref name="data"/>: axis kinds always; stacked ones with more than one series;
    /// pie and donut with one series of positive values; gauge with a single value; heatmap with several rows or columns.
    /// </summary>
    public static IReadOnlyList<ChartKind> Fitting(ChartData data)
    {
        var kinds = new List<ChartKind> { ChartKind.Line, ChartKind.Area, ChartKind.Bar, ChartKind.HorizontalBar };
        if (data.Series.Count > 1)
            kinds.AddRange([ChartKind.StackedArea, ChartKind.StackedBar, ChartKind.StackedHorizontalBar, ChartKind.Combo]);
        kinds.Add(ChartKind.Scatter);
        if (data.Series.Count >= 1 && data.Series[0].Values.Any(v => v > 0))
            kinds.AddRange([ChartKind.Pie, ChartKind.Donut]);
        if (data.Series.Count > 1 || data.Labels.Count > 1)
            kinds.Add(ChartKind.Heatmap);
        if (data.Series.Count >= 1 && data.Series[0].Values.Count >= 1)
            kinds.Add(ChartKind.Gauge);
        return kinds;
    }
}

/// <summary>An axis from <see cref="Min"/> to <see cref="Max"/> with a tick every <see cref="Step"/>.</summary>
public sealed record AxisScale(double Min, double Max, double Step)
{
    /// <summary>The tick values, Min to Max.</summary>
    public IReadOnlyList<double> Ticks()
    {
        var ticks = new List<double>();
        var count = (int)Math.Round((Max - Min) / Step);
        for (var i = 0; i <= count; i++)
            ticks.Add(Math.Round(Min + i * Step, 10)); // no 0.30000000000000004
        return ticks;
    }

    /// <summary>Where <paramref name="value"/> falls on the axis: 0 at Min, 1 at Max.</summary>
    public double Fraction(double value) => Max > Min ? (value - Min) / (Max - Min) : 0;
}

/// <summary>One slice of a pie, angles in degrees, clockwise from 12 o'clock.</summary>
public sealed record PieSlice(int Index, double Value, double Fraction, double StartAngle, double SweepAngle);

/// <summary>Chart math, shared by the chart controls and tested without them.</summary>
public static class ChartMath
{
    /// <summary>
    /// A readable axis covering <paramref name="min"/>..<paramref name="max"/>: steps of 1, 2, 2.5 or 5 times a power of
    /// ten, at most about <paramref name="maxTicks"/> ticks, from a multiple of the step. Counting charts (all values ≥ 0)
    /// start at 0. A flat range (all values equal) still gets an axis around it.
    /// </summary>
    public static AxisScale NiceScale(double min, double max, int maxTicks = 6, bool includeZero = true)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max))
            (min, max) = (0, 1);
        if (min > max)
            (min, max) = (max, min);
        if (includeZero)
        {
            min = Math.Min(min, 0);
            max = Math.Max(max, 0);
        }
        if (max - min < 1e-12)
        {
            var pad = Math.Abs(max) > 1e-12 ? Math.Abs(max) * 0.5 : 1;
            (min, max) = includeZero && min >= 0 ? (0, max + pad) : (min - pad, max + pad);
        }

        var step = NiceNumber((max - min) / Math.Max(1, maxTicks - 1));
        var niceMin = Math.Floor(min / step) * step;
        var niceMax = Math.Ceiling(max / step) * step;
        return new AxisScale(Math.Round(niceMin, 10), Math.Round(niceMax, 10), step);
    }

    /// <summary>The nearest of 1, 2, 2.5, 5, 10 × 10ⁿ at or above <paramref name="rough"/>.</summary>
    private static double NiceNumber(double rough)
    {
        var exponent = Math.Floor(Math.Log10(rough));
        var power = Math.Pow(10, exponent);
        var fraction = rough / power;
        var nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10;
        return nice * power;
    }

    /// <summary>The slices for these values: positive values only (others get no slice), in order, adding up to 360°.</summary>
    public static IReadOnlyList<PieSlice> Pie(IReadOnlyList<double?> values)
    {
        var total = values.Sum(v => v is > 0 and var x && double.IsFinite(x) ? x : 0);
        var slices = new List<PieSlice>();
        if (total <= 0)
            return slices;
        var angle = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not (> 0 and var value) || !double.IsFinite(value))
                continue;
            var fraction = value / total;
            slices.Add(new PieSlice(i, value, fraction, angle, fraction * 360));
            angle += fraction * 360;
        }
        return slices;
    }

    /// <summary>
    /// A histogram of <paramref name="values"/>: <paramref name="bins"/> equal ranges from the smallest to the largest
    /// value, labelled "2–4" (the last range includes its upper end), with a count per range.
    /// </summary>
    public static ChartData Histogram(string name, IEnumerable<double> values, int bins = 10)
    {
        var list = values.Where(double.IsFinite).ToList();
        if (list.Count == 0 || bins < 1)
            return ChartData.Empty;
        var min = list.Min();
        var max = list.Max();
        if (max - min < 1e-12)
            return ChartData.Of(name, [(Short(min), list.Count)]);
        var width = (max - min) / bins;
        var counts = new double[bins];
        foreach (var v in list)
            counts[Math.Min(bins - 1, (int)((v - min) / width))]++;
        var labels = Enumerable.Range(0, bins).Select(i => $"{Short(min + i * width)}–{Short(min + (i + 1) * width)}").ToList();
        return new ChartData(labels, [new ChartSeries(name, counts.Select(c => (double?)c).ToList())]);
    }

    /// <summary>"1,250", "12.5", "3.2k", "1.5M": short labels for ticks and values, in the user's culture by default.</summary>
    public static string Short(double value, IFormatProvider? format = null)
    {
        var abs = Math.Abs(value);
        return abs >= 1_000_000 ? (value / 1_000_000).ToString("0.#", format) + "M"
            : abs >= 10_000 ? (value / 1_000).ToString("0.#", format) + "k"
            : value.ToString(abs >= 100 || Math.Round(value) == value ? "#,0" : "0.##", format);
    }
}
