using DevHub.Sdk.Core.Charts;

namespace DevHub.Integration;

/// <summary>
/// The chart maker (the Charts plugin, when it's on): opens a chart's data in its tab, to restyle it, change the numbers
/// or the kind, and export it. Inject as <c>IChartMaker? chartMaker = null</c> (or <c>IEnumerable&lt;IChartMaker&gt;</c>)
/// and show "Open in Chart Maker" only when there is one.
/// </summary>
public interface IChartMaker
{
    /// <summary>Shows <paramref name="data"/> as <paramref name="kind"/> in the chart maker (replacing what it showed).</summary>
    void Open(ChartKind kind, ChartData data);
}
