using System.Text;

namespace DevHub.Sdk.Core.Tables;

/// <summary>
/// A table to export or copy: headers and rows of text (a report's numbers, a backlog). <see cref="ToCsv"/> for files
/// (RFC 4180, opens in Excel and Sheets), <see cref="ToTsv"/> for the clipboard (pastes as cells in Word, Excel, Sheets).
/// </summary>
public sealed record TableData(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    public static TableData Empty { get; } = new([], []);

    /// <summary>A name for the file or the copied table's heading.</summary>
    public string? Title { get; init; }

    /// <summary>Comma-separated, quoted where needed, CRLF line ends.</summary>
    public string ToCsv() => Write(',', Quote);

    /// <summary>Tab-separated; tabs and line breaks inside a cell become spaces.</summary>
    public string ToTsv() => Write('\t', cell => cell.Replace('\t', ' ').Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' '));

    private string Write(char separator, Func<string, string> cell)
    {
        var text = new StringBuilder();
        foreach (var row in Rows.Prepend(Headers))
            text.Append(string.Join(separator, row.Select(c => cell(c ?? "")))).Append("\r\n");
        return text.ToString();
    }

    private static string Quote(string cell) =>
        cell.IndexOfAny([',', '"', '\n', '\r']) >= 0 || cell.StartsWith(' ') || cell.EndsWith(' ')
            ? "\"" + cell.Replace("\"", "\"\"") + "\""
            : cell;
}
