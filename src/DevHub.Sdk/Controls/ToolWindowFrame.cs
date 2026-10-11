using Avalonia;
using Avalonia.Controls.Primitives;

namespace DevHub.Controls;

/// <summary>
/// The chrome around a tool window (like Rider's Project or Git panels): a header row with
/// the title on the left and action buttons on the right, and the content below.
/// <code>
/// &lt;c:ToolWindowFrame Header="Repositories"&gt;
///     &lt;c:ToolWindowFrame.HeaderActions&gt;
///         &lt;c:Toolbar&gt;…&lt;/c:Toolbar&gt;
///     &lt;/c:ToolWindowFrame.HeaderActions&gt;
///     …content…
/// &lt;/c:ToolWindowFrame&gt;
/// </code>
/// Look: Themes/Components/ToolWindowFrame.axaml.
/// </summary>
public class ToolWindowFrame : HeaderedContentControl
{
    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<ToolWindowFrame, object?>(nameof(HeaderActions));

    /// <summary>Right-aligned header content, usually a <see cref="Toolbar"/>.</summary>
    public object? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }
}
