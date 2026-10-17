using DevHub.Sdk.Core.Charts;
using DevHub.Sdk.Core.Tables;

namespace DevHub.Services;

/// <summary>
/// Getting charts and tables out of DevHub, for reports: save a chart as a PNG or copy it as an image, save a table as
/// CSV or copy it as cells (tab-separated: pastes as a table into Word, Excel, Google Docs). Images are light (they
/// print well) unless the user picks otherwise. Every method says how it went in the status bar.
/// </summary>
public interface IExportService
{
    /// <summary>Asks where, then saves the chart as a PNG. False when cancelled or it failed.</summary>
    Task<bool> SaveChartAsync(ChartKind kind, ChartData data, string suggestedName);

    /// <summary>Puts the chart on the clipboard as an image.</summary>
    Task CopyChartAsync(ChartKind kind, ChartData data);

    /// <summary>Asks where, then saves the table as CSV. False when cancelled or it failed.</summary>
    Task<bool> SaveTableAsync(TableData table, string suggestedName);

    /// <summary>Puts the table on the clipboard as tab-separated cells.</summary>
    Task CopyTableAsync(TableData table);

    /// <summary>Puts text on the clipboard.</summary>
    Task CopyTextAsync(string text);
}
