using DevHub.Sdk.Core.Formatting;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class MarkdownTests
{
    private static MarkdownRun Run(string text, MarkdownStyle style = MarkdownStyle.None, string? link = null) => new(text, style, link);

    private static IReadOnlyList<MarkdownRun> Inline(string text) => Markdown.ParseInlines(text);

    [Fact]
    public void Plain_text_is_one_paragraph_and_lines_join_with_a_space()
    {
        var blocks = Markdown.Parse("Hello\nworld");
        var paragraph = Assert.IsType<MarkdownParagraph>(Assert.Single(blocks));
        Assert.Equal([Run("Hello world")], paragraph.Runs);
    }

    [Fact]
    public void Blank_lines_separate_paragraphs_and_crlf_is_fine()
    {
        var blocks = Markdown.Parse("One\r\n\r\nTwo\r\n");
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.IsType<MarkdownParagraph>(b));
    }

    [Fact]
    public void Two_trailing_spaces_or_a_backslash_make_a_hard_break()
    {
        Assert.Equal([Run("a"), Run("\n"), Run("b"), Run("\n"), Run("c")],
            ((MarkdownParagraph)Markdown.Parse("a  \nb\\\nc")[0]).Runs);
    }

    [Theory]
    [InlineData("# Title", 1, "Title")]
    [InlineData("### Deep ###", 3, "Deep")]
    [InlineData("###### Six", 6, "Six")]
    public void Headings(string text, int level, string title)
    {
        var heading = Assert.IsType<MarkdownHeading>(Assert.Single(Markdown.Parse(text)));
        Assert.Equal(level, heading.Level);
        Assert.Equal([Run(title)], heading.Runs);
    }

    [Theory]
    [InlineData("#hashtag")]       // no space: text
    [InlineData("####### seven")]  // more than six: text
    public void Not_headings(string text) => Assert.IsType<MarkdownParagraph>(Assert.Single(Markdown.Parse(text)));

    [Fact]
    public void Fenced_code_keeps_its_text_and_language()
    {
        var code = Assert.IsType<MarkdownCodeBlock>(Assert.Single(Markdown.Parse("```csharp\nvar x = 1; // *not* italic\n\n  indented\n```")));
        Assert.Equal("csharp", code.Language);
        Assert.Equal("var x = 1; // *not* italic\n\n  indented", code.Code);
    }

    [Fact]
    public void An_unclosed_fence_runs_to_the_end()
    {
        var code = Assert.IsType<MarkdownCodeBlock>(Assert.Single(Markdown.Parse("~~~\na\nb")));
        Assert.Null(code.Language);
        Assert.Equal("a\nb", code.Code);
    }

    [Fact]
    public void Indented_code_after_a_blank_line()
    {
        var blocks = Markdown.Parse("Text\n\n    code();\n    more();\n\nAfter");
        Assert.Equal("code();\nmore();", Assert.IsType<MarkdownCodeBlock>(blocks[1]).Code);
        Assert.IsType<MarkdownParagraph>(blocks[2]);
    }

    [Fact]
    public void Quotes_hold_blocks()
    {
        var quote = Assert.IsType<MarkdownQuote>(Assert.Single(Markdown.Parse("> # Note\n> Be **careful**.")));
        Assert.IsType<MarkdownHeading>(quote.Blocks[0]);
        Assert.Equal([Run("Be "), Run("careful", MarkdownStyle.Bold), Run(".")], ((MarkdownParagraph)quote.Blocks[1]).Runs);
    }

    [Fact]
    public void Bullet_lists_with_nesting_and_continuation_lines()
    {
        var list = Assert.IsType<MarkdownList>(Assert.Single(Markdown.Parse("- one\n  still one\n  - nested\n* two")));
        Assert.False(list.Ordered);
        Assert.Equal(["one still one", "nested", "two"], list.Items.Select(i => string.Concat(i.Runs.Select(r => r.Text))));
        Assert.Equal([0, 1, 0], list.Items.Select(i => i.Level));
    }

    [Fact]
    public void Numbered_lists_keep_their_start()
    {
        var list = Assert.IsType<MarkdownList>(Assert.Single(Markdown.Parse("3. three\n4) four")));
        Assert.True(list.Ordered);
        Assert.Equal(3, list.Start);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Task_lists()
    {
        var list = Assert.IsType<MarkdownList>(Assert.Single(Markdown.Parse("- [ ] todo\n- [x] done\n- [X] also done\n- plain")));
        Assert.Equal([false, true, true, null], list.Items.Select(i => i.Checked));
        Assert.Equal("todo", list.Items[0].Runs[0].Text);
    }

    [Theory]
    [InlineData("---")]
    [InlineData("* * *")]
    [InlineData("___")]
    public void Rules(string text) => Assert.IsType<MarkdownRule>(Assert.Single(Markdown.Parse(text)));

    [Fact]
    public void A_list_a_rule_and_a_heading_end_a_paragraph()
    {
        var blocks = Markdown.Parse("Para\n- item\n\nPara\n---\nPara\n# H");
        Assert.Equal([typeof(MarkdownParagraph), typeof(MarkdownList), typeof(MarkdownParagraph), typeof(MarkdownRule), typeof(MarkdownParagraph), typeof(MarkdownHeading)],
            blocks.Select(b => b.GetType()));
    }

    [Fact]
    public void Emphasis()
    {
        Assert.Equal([Run("a "), Run("b", MarkdownStyle.Bold), Run(" "), Run("c", MarkdownStyle.Italic), Run(" "), Run("d", MarkdownStyle.Bold), Run(" "),
                      Run("e", MarkdownStyle.Italic), Run(" "), Run("f", MarkdownStyle.Strikethrough), Run(" "), Run("g", MarkdownStyle.Bold | MarkdownStyle.Italic)],
            Inline("a **b** *c* __d__ _e_ ~~f~~ ***g***"));
    }

    [Fact]
    public void Nested_emphasis_combines()
    {
        Assert.Equal([Run("bold ", MarkdownStyle.Bold), Run("both", MarkdownStyle.Bold | MarkdownStyle.Italic), Run(" bold", MarkdownStyle.Bold)],
            Inline("**bold *both* bold**"));
    }

    [Theory]
    [InlineData("snake_case_name")]
    [InlineData("2 * 3 * 4")]
    [InlineData("an *unclosed star")]
    [InlineData("a ** b")]
    public void Not_emphasis(string text) => Assert.Equal([Run(text)], Inline(text));

    [Fact]
    public void Code_spans_are_taken_as_written()
    {
        Assert.Equal([Run("run "), Run("dotnet **build**", MarkdownStyle.Code), Run(" now")], Inline("run `dotnet **build**` now"));
        Assert.Equal([Run("a`b", MarkdownStyle.Code)], Inline("``a`b``"));
    }

    [Fact]
    public void Links_and_autolinks()
    {
        Assert.Equal([Run("see "), Run("the docs", link: "https://x.dev/docs"), Run(".")], Inline("see [the docs](https://x.dev/docs \"Docs\")."));
        Assert.Equal([Run("https://x.dev", link: "https://x.dev")], Inline("<https://x.dev>"));
        Assert.Equal([Run("bold", MarkdownStyle.Bold, "https://x.dev"), Run(" link", link: "https://x.dev")], Inline("[**bold** link](https://x.dev)"));
    }

    [Theory]
    [InlineData("[no target]")]
    [InlineData("[text] (space)")]
    [InlineData("[empty]()")]
    public void Not_links(string text) => Assert.Null(Inline(text).Single().Link);

    [Fact]
    public void Backslash_escapes()
    {
        Assert.Equal([Run("*not italic* and [not](a link)")], Inline(@"\*not italic\* and \[not](a link)"));
    }

    [Fact]
    public void Html_is_text() => Assert.Equal([Run("<b>hi</b>")], Inline("<b>hi</b>"));

    [Theory]
    [InlineData("https://github.com", true)]
    [InlineData("mailto:a@b.c", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/Windows", false)]
    [InlineData(null, false)]
    public void Only_web_and_mail_links_are_safe(string? url, bool safe) => Assert.Equal(safe, Markdown.IsSafeLink(url));

    [Fact]
    public void Empty_or_null_is_no_blocks()
    {
        Assert.Empty(Markdown.Parse(null));
        Assert.Empty(Markdown.Parse("\n\n  \n"));
    }
}
