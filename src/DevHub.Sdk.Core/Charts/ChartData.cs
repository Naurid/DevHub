namespace DevHub.Sdk.Core.Charts;

/// <summary>One named series of values, one per label of its <see cref="ChartData"/> (null: no value there, a gap).</summary>
public sealed record ChartSeries(string Name, IReadOnlyList<double?> Values);

/// <summary>
/// What a chart shows (the Sdk's LineChart, BarChart and PieChart): labels along the X axis (or the slices of a pie)
/// and one or more series with a value per label. Built by a plugin's .Core (e.g. a burndown from issue history),
/// so it has no UI types. A pie uses the first series.
/// </summary>
public sealed record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series)
{
    public static ChartData Empty { get; } = new([], []);

    /// <summary>One series: label → value.</summary>
    public static ChartData Of(string name, IEnumerable<(string Label, double Value)> points)
    {
        var list = points.ToList();
        return new ChartData(list.Select(p => p.Label).ToList(), [new ChartSeries(name, list.Select(p => (double?)p.Value).ToList())]);
    }

    /// <summary>The smallest and largest value of every series (null when there are none).</summary>
    public (double Min, double Max)? Range()
    {
        var values = Series.SelectMany(s => s.Values).OfType<double>().Where(double.IsFinite).ToList();
        return values.Count == 0 ? null : (values.Min(), values.Max());
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

    /// <summary>"1,250", "12.5", "3.2k", "1.5M": short labels for ticks and values, in the user's culture by default.</summary>
    public static string Short(double value, IFormatProvider? format = null)
    {
        var abs = Math.Abs(value);
        return abs >= 1_000_000 ? (value / 1_000_000).ToString("0.#", format) + "M"
            : abs >= 10_000 ? (value / 1_000).ToString("0.#", format) + "k"
            : value.ToString(abs >= 100 || Math.Round(value) == value ? "#,0" : "0.##", format);
    }
}
