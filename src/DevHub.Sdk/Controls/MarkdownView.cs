using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using DevHub.Sdk.Core.Formatting;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Controls;

/// <summary>
/// Shows Markdown (an issue's description, a comment, release notes, a README): <c>&lt;c:MarkdownView Markdown="{Binding Description}" /&gt;</c>.
/// The subset <see cref="Sdk.Core.Formatting.Markdown"/> parses: headings, paragraphs, emphasis, code, quotes, lists with
/// task boxes (read-only), rules and links. Text is selectable; links open in the browser, and only http(s) / mailto
/// links are links at all (anything else stays text: Markdown from an issue can't open a local file). Fonts, sizes and
/// colors come from the design tokens. Read-only: an editor is a TextBox next to it.
/// </summary>
public class MarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    public string? Markdown { get => GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }

    public MarkdownView()
    {
        Bind(SpacingProperty, this.GetResourceObservable("Spacing"));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();
        foreach (var block in Sdk.Core.Formatting.Markdown.Parse(Markdown))
            Children.Add(Build(block));
    }

    private Control Build(MarkdownBlock block) => block switch
    {
        MarkdownHeading h => Heading(h),
        MarkdownParagraph p => Paragraph(p.Runs),
        MarkdownCodeBlock c => CodeBlock(c),
        MarkdownQuote q => Quote(q),
        MarkdownList l => List(l),
        MarkdownRule => Rule(),
        _ => new Control(),
    };

    private Control Heading(MarkdownHeading heading)
    {
        var text = Paragraph(heading.Runs);
        text.FontWeight = FontWeight.SemiBold;
        Token(text, TextBlock.FontSizeProperty, heading.Level <= 2 ? "FontSize.Header" : "FontSize.Body");
        return text;
    }

    private SelectableTextBlock Paragraph(IReadOnlyList<MarkdownRun> runs)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        Token(text, TextBlock.ForegroundProperty, "Brush.Text.Primary");
        text.Inlines = [.. runs.Select(Inline)];
        return text;
    }

    private Inline Inline(MarkdownRun run)
    {
        if (run.Text == "\n")
            return new LineBreak();
        if (run.Link is { } url && Sdk.Core.Formatting.Markdown.IsSafeLink(url))
        {
            var link = new HyperlinkButton { Content = run.Text, NavigateUri = new Uri(url), Padding = new Thickness(0), Margin = new Thickness(0) };
            ToolTip.SetTip(link, url);
            return new InlineUIContainer(link) { BaselineAlignment = BaselineAlignment.TextBottom };
        }
        var inline = new Run(run.Text);
        if (run.Style.HasFlag(MarkdownStyle.Bold))
            inline.FontWeight = FontWeight.SemiBold;
        if (run.Style.HasFlag(MarkdownStyle.Italic))
            inline.FontStyle = FontStyle.Italic;
        if (run.Style.HasFlag(MarkdownStyle.Strikethrough))
            inline.TextDecorations = TextDecorations.Strikethrough;
        if (run.Style.HasFlag(MarkdownStyle.Code))
        {
            Token(inline, TextElement.FontFamilyProperty, "Font.Code");
            Token(inline, TextElement.BackgroundProperty, "Brush.Bg.SelectionInactive");
        }
        return inline;
    }

    private Control CodeBlock(MarkdownCodeBlock code)
    {
        var text = new SelectableTextBlock { Text = code.Code, TextWrapping = TextWrapping.NoWrap };
        Token(text, TextBlock.FontFamilyProperty, "Font.Code");
        Token(text, TextBlock.FontSizeProperty, "FontSize.Code");
        Token(text, TextBlock.ForegroundProperty, "Brush.Text.Primary");
        var border = new Border
        {
            Child = new ScrollViewer { Content = text, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
        };
        Token(border, Border.BackgroundProperty, "Brush.Bg.SelectionInactive");
        Token(border, Border.CornerRadiusProperty, "Radius.Control");
        Token(border, Border.PaddingProperty, "Padding.Banner");
        if (code.Language is { } language)
            AutomationPropertiesName(border, L.T($"{language} code"));
        return border;
    }

    private Control Quote(MarkdownQuote quote)
    {
        var inner = new StackPanel();
        Token(inner, SpacingProperty, "Spacing");
        foreach (var block in quote.Blocks)
            inner.Children.Add(Build(block));
        var border = new Border { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 0, 0, 0), Child = inner };
        Token(border, Border.BorderBrushProperty, "Brush.Border.Control");
        return border;
    }

    private Control List(MarkdownList list)
    {
        var panel = new StackPanel();
        Token(panel, SpacingProperty, "Spacing.Small");
        var number = list.Start;
        foreach (var item in list.Items)
        {
            Control marker = item.Checked is { } done
                ? new CheckBox { IsChecked = done, IsHitTestVisible = false, Focusable = false, MinWidth = 0, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top }
                : new TextBlock { Text = list.Ordered && item.Level == 0 ? $"{number}." : item.Level == 0 ? "•" : "◦", MinWidth = list.Ordered ? 22 : 14 };
            if (marker is TextBlock markerText)
                Token(markerText, TextBlock.ForegroundProperty, "Brush.Text.Secondary");
            if (item.Level == 0)
                number++;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(item.Level * 18, 0, 0, 0), ColumnSpacing = 6 };
            var text = Paragraph(item.Runs);
            Grid.SetColumn(text, 1);
            row.Children.Add(marker);
            row.Children.Add(text);
            panel.Children.Add(row);
        }
        return panel;
    }

    private Control Rule()
    {
        var line = new Border { Height = 1, Margin = new Thickness(0, 4) };
        Token(line, Border.BackgroundProperty, "Brush.Separator");
        return line;
    }

    /// <summary>A property follows a design token (like a DynamicResource: it changes with the theme), looked up from
    /// this view, so it works for inlines too (they can't look resources up before their text block takes them).</summary>
    private void Token(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));

    private static void AutomationPropertiesName(Control control, string name) =>
        Avalonia.Automation.AutomationProperties.SetName(control, name);
}