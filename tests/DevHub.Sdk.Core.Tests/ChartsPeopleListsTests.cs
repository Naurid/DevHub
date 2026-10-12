using System.Collections.ObjectModel;
using DevHub.Sdk.Core.Charts;
using DevHub.Sdk.Core.Collections;
using DevHub.Sdk.Core.People;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class ChartMathTests
{
    [Theory]
    [InlineData(0, 37, 0, 40, 10)]        // counts: from 0, steps of 10
    [InlineData(3, 7, 0, 8, 2)]           // starts at 0 even when the data doesn't
    [InlineData(0, 1, 0, 1, 0.2)]
    [InlineData(0, 1234, 0, 1250, 250)]
    [InlineData(-12, 30, -20, 30, 10)]    // negatives: both sides
    public void Nice_scales(double min, double max, double niceMin, double niceMax, double step)
    {
        var scale = ChartMath.NiceScale(min, max);
        Assert.Equal((niceMin, niceMax, step), (scale.Min, scale.Max, scale.Step));
    }

    [Fact]
    public void Without_zero_the_axis_hugs_the_data()
    {
        var scale = ChartMath.NiceScale(98.2, 101.7, includeZero: false);
        Assert.True(scale.Min <= 98.2 && scale.Min > 90 && scale.Max >= 101.7 && scale.Max < 110);
    }

    [Fact]
    public void A_flat_or_empty_range_still_gets_an_axis()
    {
        Assert.Equal(0, ChartMath.NiceScale(5, 5).Min);
        Assert.Equal(5, ChartMath.NiceScale(5, 5).Max);                       // 0..5: the values sit on the top line
        Assert.True(ChartMath.NiceScale(5, 5, includeZero: false).Max > 5);  // without zero: an axis around 5
        Assert.True(ChartMath.NiceScale(0, 0).Max > 0);
        Assert.True(ChartMath.NiceScale(double.NaN, double.NaN).Max > 0);
    }

    [Fact]
    public void Ticks_are_exact_and_cover_the_axis()
    {
        Assert.Equal([0, 0.2, 0.4, 0.6, 0.8, 1.0], ChartMath.NiceScale(0, 1).Ticks());
        Assert.Equal(0.5, new AxisScale(0, 10, 2).Fraction(5));
    }

    [Fact]
    public void Pie_slices_add_up_and_skip_non_positive_values()
    {
        var slices = ChartMath.Pie([1, null, 3, 0, -2, 4]);
        Assert.Equal([0, 2, 5], slices.Select(s => s.Index));
        Assert.Equal(360, slices.Sum(s => s.SweepAngle), 6);
        Assert.Equal([0, 45, 180], slices.Select(s => Math.Round(s.StartAngle, 6)));
        Assert.Equal(0.5, slices[2].Fraction);
        Assert.Empty(ChartMath.Pie([0, null]));
    }

    [Theory]
    [InlineData(1250, "1,250")]
    [InlineData(12.5, "12.5")]
    [InlineData(32000, "32k")]
    [InlineData(1_500_000, "1.5M")]
    [InlineData(7, "7")]
    public void Short_numbers(double value, string text) =>
        Assert.Equal(text, ChartMath.Short(value, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Data_range_and_one_series_helper()
    {
        var data = ChartData.Of("Done", [("Mon", 3), ("Tue", 7)]);
        Assert.Equal(["Mon", "Tue"], data.Labels);
        Assert.Equal((3.0, 7.0), data.Range());
        Assert.Null(ChartData.Empty.Range());
    }
}

public class PeopleSearchTests
{
    private static readonly Person Ada = new("ada", "Ada Lovelace", "ada@example.com");
    private static readonly Person Grace = new("ghopper", "Grace Hopper");
    private static readonly Person Linus = new("torvalds", "Linus Torvalds");
    private static readonly Person[] Everyone = [Linus, Grace, Ada];

    [Fact]
    public void An_empty_query_lists_everyone_by_name() =>
        Assert.Equal([Ada, Grace, Linus], PeopleSearch.Find(Everyone, ""));

    [Fact]
    public void Matches_name_id_or_email_fuzzily()
    {
        Assert.Equal([Grace], PeopleSearch.Find(Everyone, "hopper"));
        Assert.Equal([Linus], PeopleSearch.Find(Everyone, "torv"));
        Assert.Equal([Ada], PeopleSearch.Find(Everyone, "example.com"));
        Assert.Contains(Ada, PeopleSearch.Find(Everyone, "al"));
    }

    [Fact]
    public void Picked_people_and_duplicates_are_left_out()
    {
        Assert.Equal([Grace, Linus], PeopleSearch.Find([.. Everyone, Ada with { Name = "Ada again" }], null, exclude: [Ada]));
    }

    [Theory]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("grace", "G")]
    [InlineData("jean-luc picard", "JP")]
    [InlineData("", "X")]
    public void Initials(string name, string initials) => Assert.Equal(initials, new Person("x", name).Initials);
}

public class ListMovesTests
{
    [Theory]
    [InlineData("b", 0, "b,a,c,d")]   // to the top
    [InlineData("a", 4, "b,c,d,a")]   // to the end (after the last)
    [InlineData("a", 2, "b,a,c,d")]   // "before c": the index as it was before the move
    [InlineData("d", 1, "a,d,b,c")]
    [InlineData("b", 1, "a,b,c,d")]   // right where it is
    [InlineData("b", 2, "a,b,c,d")]   // just after itself: also where it is
    public void Within_one_list(string item, int toIndex, string expected)
    {
        var list = new ObservableCollection<string>(["a", "b", "c", "d"]);
        ListMoves.Move(list, item, list, toIndex);
        Assert.Equal(expected, string.Join(",", list));
    }

    [Fact]
    public void Between_lists()
    {
        var todo = new List<string> { "a", "b" };
        var done = new List<string> { "x" };
        Assert.Equal(0, ListMoves.Move(todo, "b", done, 0));
        Assert.Equal(["a"], todo);
        Assert.Equal(["b", "x"], done);
        Assert.Equal(2, ListMoves.Move(todo, "a", done, 99)); // clamped to the end
        Assert.Equal(-1, ListMoves.Move(todo, "zzz", done, 0));
    }

    [Fact]
    public void No_op_detection()
    {
        var list = new List<string> { "a", "b", "c" };
        Assert.True(ListMoves.IsNoOp(list, "b", list, 1));
        Assert.True(ListMoves.IsNoOp(list, "b", list, 2));
        Assert.False(ListMoves.IsNoOp(list, "b", list, 0));
        Assert.False(ListMoves.IsNoOp(list, "b", new List<string>(), 0));
    }
}
