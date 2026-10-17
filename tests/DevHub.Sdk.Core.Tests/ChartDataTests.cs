using DevHub.Sdk.Core.Charts;
using DevHub.Sdk.Core.Tables;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class ChartDataTests
{
    private static ChartData TwoSeries => new(["S1", "S2", "S3"],
    [
        new ChartSeries("Done", [3, 5, null]),
        new ChartSeries("Left", [-2, 4, 6]),
    ]);

    [Fact]
    public void The_range_covers_bands_and_reference_lines()
    {
        var data = TwoSeries with
        {
            Bands = [new ChartBand("Forecast", [null, 1, 2], [null, 9, 14])],
            References = [new ChartReference("Target", 20)],
        };

        Assert.Equal((-2, 20), data.Range());
    }

    [Fact]
    public void Stacked_range_adds_positives_and_negatives_apart()
    {
        // S2: 5 + 4 = 9 up; S1: -2 down; S3: 6.
        Assert.Equal((-2, 9), TwoSeries.StackedRange());
        Assert.Null(ChartData.Empty.StackedRange());
    }

    [Fact]
    public void A_histogram_counts_values_in_equal_ranges()
    {
        var data = ChartMath.Histogram("Cycle time", [1, 2, 2, 3, 9, 10], bins: 3);

        Assert.Equal(3, data.Labels.Count);
        Assert.Equal([4.0, 0, 2], data.Series[0].Values.Select(v => v!.Value));   // 1–4, 4–7, 7–10 (10 in the last)
        Assert.Equal(["5"], ChartMath.Histogram("x", [5, 5, 5]).Labels);
        Assert.Same(ChartData.Empty, ChartMath.Histogram("x", []));
    }

    [Fact]
    public void Fitting_kinds_depend_on_the_data()
    {
        var one = ChartData.Of("Points", [("A", 3), ("B", 5)]);
        var kinds = ChartKinds.Fitting(one);
        Assert.Contains(ChartKind.Pie, kinds);
        Assert.DoesNotContain(ChartKind.StackedBar, kinds);

        var two = ChartKinds.Fitting(TwoSeries);
        Assert.Contains(ChartKind.StackedBar, two);
        Assert.Contains(ChartKind.Combo, two);
        Assert.Equal("Stacked bars", ChartKinds.Name(ChartKind.StackedBar));
    }

    [Fact]
    public void A_chart_becomes_a_table_and_values_get_their_suffix()
    {
        var data = TwoSeries with { XTitle = "Sprint", ValueSuffix = " pts" };

        var table = data.ToTable();
        Assert.Equal(["Sprint", "Done", "Left"], table.Headers);
        Assert.Equal(["S3", "", "6"], table.Rows[2]);
        Assert.Equal("12 pts", data.Format(12));
    }

    [Fact]
    public void Tables_write_csv_and_tab_separated_text()
    {
        var table = new TableData(["Key", "Summary"], [["DEV-1", "Login, form"], ["DEV-2", "Say \"hi\"\nthen go"]]);

        Assert.Equal("Key,Summary\r\nDEV-1,\"Login, form\"\r\nDEV-2,\"Say \"\"hi\"\"\nthen go\"\r\n", table.ToCsv());
        Assert.Equal("Key\tSummary\r\nDEV-1\tLogin, form\r\nDEV-2\tSay \"hi\" then go\r\n", table.ToTsv());
    }
}
