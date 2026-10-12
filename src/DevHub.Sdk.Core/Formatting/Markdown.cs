using System.Text;

namespace DevHub.Sdk.Core.Formatting;

/// <summary>Inline styles of a <see cref="MarkdownRun"/> (combinable).</summary>
[Flags]
public enum MarkdownStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Code = 4,
    Strikethrough = 8,
}

/// <summary>A piece of text with one style; <paramref name="Link"/> when it's (part of) a link. "\n" is a hard line break.</summary>
public sealed record MarkdownRun(string Text, MarkdownStyle Style = MarkdownStyle.None, string? Link = null);

/// <summary>A block of a Markdown document (<see cref="Markdown.Parse"/>).</summary>
public abstract record MarkdownBlock;

/// <summary># to ######.</summary>
public sealed record MarkdownHeading(int Level, IReadOnlyList<MarkdownRun> Runs) : MarkdownBlock;

public sealed record MarkdownParagraph(IReadOnlyList<MarkdownRun> Runs) : MarkdownBlock;

/// <summary>A fenced (```) or indented code block, as written.</summary>
public sealed record MarkdownCodeBlock(string? Language, string Code) : MarkdownBlock;

/// <summary>"> …": the quoted blocks.</summary>
public sealed record MarkdownQuote(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

/// <summary>A list item: its text, its nesting level (0 = top), and for a task item ("- [ ]" / "- [x]") whether it's done.</summary>
public sealed record MarkdownListItem(IReadOnlyList<MarkdownRun> Runs, int Level = 0, bool? Checked = null);

/// <summary>"- a", "* a", "+ a" or "1. a" items; <paramref name="Start"/> is the first number of an ordered list.</summary>
public sealed record MarkdownList(bool Ordered, int Start, IReadOnlyList<MarkdownListItem> Items) : MarkdownBlock;

/// <summary>"---", "***" or "___" on a line of its own.</summary>
public sealed record MarkdownRule : MarkdownBlock;

/// <summary>
/// The Markdown DevHub shows (issue descriptions, comments, release notes, READMEs): a common subset of CommonMark and
/// GitHub's flavor, parsed into blocks of styled runs that a view turns into text. Headings, paragraphs (hard breaks
/// with two trailing spaces or "\"), fenced and indented code, quotes, bullet / numbered / task lists (nested by
/// indentation), rules; inline **bold**, *italic*, `code`, ~~strikethrough~~, [links](url), &lt;https://autolinks&gt;
/// and backslash escapes. No HTML: it's shown as text. Never throws: anything it doesn't know is text.
/// </summary>
public static class Markdown
{
    public static IReadOnlyList<MarkdownBlock> Parse(string? text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return ParseBlocks(lines);
    }

    private static List<MarkdownBlock> ParseBlocks(IReadOnlyList<string> lines)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();
        var i = 0;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;
            blocks.Add(new MarkdownParagraph(ParseParagraphLines(paragraph)));
            paragraph.Clear();
        }

        while (i < lines.Count)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                i++;
                continue;
            }

            // Fenced code: ``` or ~~~, optionally with a language; up to the same fence (or the end).
            if (Fence(trimmed) is { } fence)
            {
                FlushParagraph();
                var language = trimmed[fence.Length..].Trim();
                var code = new List<string>();
                i++;
                while (i < lines.Count && !lines[i].Trim().StartsWith(fence, StringComparison.Ordinal))
                    code.Add(lines[i++]);
                i++; // the closing fence
                blocks.Add(new MarkdownCodeBlock(language.Length > 0 ? language.Split(' ')[0] : null, string.Join("\n", code)));
                continue;
            }

            // Indented code (4 spaces or a tab), but not as the continuation of a paragraph.
            if (paragraph.Count == 0 && IsIndentedCode(line) && ListMarker(line) is null)
            {
                var code = new List<string>();
                while (i < lines.Count && (IsIndentedCode(lines[i]) || (lines[i].Trim().Length == 0 && i + 1 < lines.Count && IsIndentedCode(lines[i + 1]))))
                {
                    code.Add(lines[i].Trim().Length == 0 ? "" : lines[i].StartsWith('\t') ? lines[i][1..] : lines[i][4..]);
                    i++;
                }
                blocks.Add(new MarkdownCodeBlock(null, string.Join("\n", code).TrimEnd('\n')));
                continue;
            }

            if (Heading(trimmed) is { } heading)
            {
                FlushParagraph();
                blocks.Add(new MarkdownHeading(heading.Level, ParseInlines(heading.Text)));
                i++;
                continue;
            }

            if (IsRule(trimmed))
            {
                FlushParagraph();
                blocks.Add(new MarkdownRule());
                i++;
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                var quoted = new List<string>();
                while (i < lines.Count && lines[i].TrimStart().StartsWith('>'))
                {
                    var inner = lines[i].TrimStart()[1..];
                    quoted.Add(inner.StartsWith(' ') ? inner[1..] : inner);
                    i++;
                }
                blocks.Add(new MarkdownQuote(ParseBlocks(quoted)));
                continue;
            }

            if (ListMarker(line) is { } first)
            {
                FlushParagraph();
                var items = new List<MarkdownListItem>();
                var baseIndent = first.Indent;
                while (i < lines.Count)
                {
                    if (ListMarker(lines[i]) is { } marker && marker.Indent >= baseIndent && (marker.Ordered == first.Ordered || marker.Indent > baseIndent))
                    {
                        var (content, done) = TaskBox(marker.Content);
                        var text = new StringBuilder(content);
                        i++;
                        // Lazy continuation lines (indented text that isn't a new item) belong to the item.
                        while (i < lines.Count && lines[i].Trim().Length > 0 && ListMarker(lines[i]) is null
                               && (lines[i].StartsWith(' ') || lines[i].StartsWith('\t')))
                            text.Append(' ').Append(lines[i++].Trim());
                        items.Add(new MarkdownListItem(ParseInlines(text.ToString()), Level: (marker.Indent - baseIndent) / 2, Checked: done));
                    }
                    else
                    {
                        break;
                    }
                }
                blocks.Add(new MarkdownList(first.Ordered, first.Number, items));
                continue;
            }

            paragraph.Add(line);
            i++;
        }
        FlushParagraph();
        return blocks;
    }

    /// <summary>Lines of one paragraph: joined with a space, or a hard break after two trailing spaces or a "\".</summary>
    private static List<MarkdownRun> ParseParagraphLines(List<string> lines)
    {
        var runs = new List<MarkdownRun>();
        for (var n = 0; n < lines.Count; n++)
        {
            var line = lines[n].TrimStart();
            var hardBreak = line.EndsWith("  ", StringComparison.Ordinal) || (line.EndsWith('\\') && !line.EndsWith("\\\\", StringComparison.Ordinal));
            line = line.EndsWith('\\') && hardBreak ? line[..^1] : line.TrimEnd();
            runs.AddRange(ParseInlines(line));
            if (n < lines.Count - 1)
                runs.Add(new MarkdownRun(hardBreak ? "\n" : " "));
        }
        return Merge(runs);
    }

    private static string? Fence(string trimmed) =>
        trimmed.StartsWith("```", StringComparison.Ordinal) ? "```" : trimmed.StartsWith("~~~", StringComparison.Ordinal) ? "~~~" : null;

    private static bool IsIndentedCode(string line) => line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith('\t');

    private static (int Level, string Text)? Heading(string trimmed)
    {
        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
            level++;
        if (level is < 1 or > 6 || (level < trimmed.Length && trimmed[level] != ' '))
            return null;
        return (level, trimmed[level..].Trim().TrimEnd('#').Trim());
    }

    private static bool IsRule(string trimmed)
    {
        var compact = trimmed.Replace(" ", "");
        return compact.Length >= 3 && (compact.All(c => c == '-') || compact.All(c => c == '*') || compact.All(c => c == '_'));
    }

    private sealed record Marker(int Indent, bool Ordered, int Number, string Content);

    private static Marker? ListMarker(string line)
    {
        var indent = 0;
        while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
            indent += line[indent] == '\t' ? 4 : 1;
        var rest = line.TrimStart(' ', '\t');
        if (rest.Length >= 2 && rest[0] is '-' or '*' or '+' && rest[1] == ' ' && !IsRule(rest))
            return new Marker(indent, false, 1, rest[2..].Trim());
        var digits = 0;
        while (digits < rest.Length && digits < 9 && char.IsAsciiDigit(rest[digits]))
            digits++;
        if (digits > 0 && digits + 1 < rest.Length && rest[digits] is '.' or ')' && rest[digits + 1] == ' ')
            return new Marker(indent, true, int.Parse(rest[..digits]), rest[(digits + 2)..].Trim());
        return null;
    }

    private static (string Content, bool? Done) TaskBox(string content) =>
        content.StartsWith("[ ] ", StringComparison.Ordinal) || content == "[ ]" ? (content[3..].TrimStart(), false)
        : content.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase) || content.Equals("[x]", StringComparison.OrdinalIgnoreCase) ? (content[3..].TrimStart(), true)
        : (content, null);

    /// <summary>The inline syntax of one piece of text.</summary>
    public static IReadOnlyList<MarkdownRun> ParseInlines(string text) => Merge(Inlines(text, MarkdownStyle.None, null));

    private static List<MarkdownRun> Inlines(string text, MarkdownStyle style, string? link)
    {
        var runs = new List<MarkdownRun>();
        var plain = new StringBuilder();
        void Flush()
        {
            if (plain.Length == 0)
                return;
            runs.Add(new MarkdownRun(plain.ToString(), style, link));
            plain.Clear();
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            // \* \_ \` … : the character itself.
            if (c == '\\' && i + 1 < text.Length && IsEscapable(text[i + 1]))
            {
                plain.Append(text[i + 1]);
                i += 2;
                continue;
            }

            // `code` (also ``code with ` inside``): taken as written.
            if (c == '`')
            {
                var ticks = Count(text, i, '`');
                var close = text.IndexOf(new string('`', ticks), i + ticks, StringComparison.Ordinal);
                if (close > 0)
                {
                    Flush();
                    runs.Add(new MarkdownRun(text[(i + ticks)..close].Trim(), style | MarkdownStyle.Code, link));
                    i = close + ticks;
                    continue;
                }
            }

            // <https://…> autolinks.
            if (c == '<' && link is null)
            {
                var close = text.IndexOf('>', i + 1);
                var inner = close > 0 ? text[(i + 1)..close] : "";
                if (IsUrl(inner))
                {
                    Flush();
                    runs.Add(new MarkdownRun(inner, style, inner));
                    i = close + 1;
                    continue;
                }
            }

            // [text](url)
            if (c == '[' && link is null && LinkAt(text, i) is { } found)
            {
                Flush();
                runs.AddRange(Inlines(found.Label, style, found.Url));
                i = found.End;
                continue;
            }

            // **bold**, __bold__, *italic*, _italic_, ~~strike~~
            if (c is '*' or '_' or '~')
            {
                var count = Math.Min(Count(text, i, c), c == '~' ? 2 : 3);
                var styleOf = c == '~' ? (count == 2 ? MarkdownStyle.Strikethrough : MarkdownStyle.None)
                    : count == 3 ? MarkdownStyle.Bold | MarkdownStyle.Italic
                    : count == 2 ? MarkdownStyle.Bold : MarkdownStyle.Italic;
                var delimiter = new string(c, count);
                var opens = styleOf != MarkdownStyle.None && i + count < text.Length && !char.IsWhiteSpace(text[i + count])
                            && !(c == '_' && i > 0 && char.IsLetterOrDigit(text[i - 1])); // snake_case isn't italic
                var close = opens ? ClosingDelimiter(text, i + count, delimiter) : -1;
                if (close > 0)
                {
                    Flush();
                    runs.AddRange(Inlines(text[(i + count)..close], style | styleOf, link));
                    i = close + count;
                    continue;
                }
                plain.Append(text, i, Math.Max(count, 1));
                i += Math.Max(count, 1);
                continue;
            }

            plain.Append(c);
            i++;
        }
        Flush();
        return runs;
    }

    /// <summary>Where the run opened at <paramref name="from"/> closes: the delimiter right after a non-space, not followed by
    /// another of its characters (so ** doesn't close *), and for _ not inside a word. -1: it doesn't close.</summary>
    private static int ClosingDelimiter(string text, int from, string delimiter)
    {
        for (var at = text.IndexOf(delimiter, from, StringComparison.Ordinal); at > from; at = text.IndexOf(delimiter, at + 1, StringComparison.Ordinal))
        {
            var before = text[at - 1];
            var after = at + delimiter.Length < text.Length ? text[at + delimiter.Length] : ' ';
            if (!char.IsWhiteSpace(before) && before != '\\' && after != delimiter[0] && before != delimiter[0]
                && !(delimiter[0] == '_' && char.IsLetterOrDigit(after)))
                return at;
        }
        return -1;
    }

    private sealed record FoundLink(string Label, string Url, int End);

    private static FoundLink? LinkAt(string text, int open)
    {
        // The matching ] (nested brackets allowed), then (url) right after it.
        var depth = 0;
        for (var j = open; j < text.Length; j++)
        {
            if (text[j] == '\\') { j++; continue; }
            if (text[j] == '[') depth++;
            else if (text[j] == ']' && --depth == 0)
            {
                if (j + 1 >= text.Length || text[j + 1] != '(')
                    return null;
                var close = text.IndexOf(')', j + 2);
                if (close < 0)
                    return null;
                var target = text[(j + 2)..close].Trim();
                var space = target.IndexOf(' '); // [a](url "title"): the title is dropped
                var url = (space > 0 ? target[..space] : target).Trim('<', '>');
                return url.Length == 0 ? null : new FoundLink(text[(open + 1)..j], url, close + 1);
            }
        }
        return null;
    }

    private static bool IsUrl(string s) =>
        (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
         || s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) && !s.Any(char.IsWhiteSpace);

    private static bool IsEscapable(char c) => "\\`*_{}[]()#+-.!~>|<".Contains(c);

    private static int Count(string text, int at, char c)
    {
        var n = 0;
        while (at + n < text.Length && text[at + n] == c)
            n++;
        return n;
    }

    /// <summary>Neighboring runs with the same style and link become one.</summary>
    private static List<MarkdownRun> Merge(List<MarkdownRun> runs)
    {
        var merged = new List<MarkdownRun>();
        foreach (var run in runs.Where(r => r.Text.Length > 0))
        {
            if (merged.Count > 0 && merged[^1].Style == run.Style && merged[^1].Link == run.Link && run.Text != "\n" && merged[^1].Text != "\n")
                merged[^1] = merged[^1] with { Text = merged[^1].Text + run.Text };
            else
                merged.Add(run);
        }
        return merged;
    }

    /// <summary>True for links a viewer may open: http(s) and mailto only (never file:, javascript: …).</summary>
    public static bool IsSafeLink(string? url) => url is not null && IsUrl(url);
}
