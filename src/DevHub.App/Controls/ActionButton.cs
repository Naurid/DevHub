using Avalonia;
using Avalonia.Controls;

namespace DevHub.Controls;

/// <summary>
/// Square, icon-only button used in toolbars and tool-window headers (JetBrains "action button"):
/// <c>&lt;c:ActionButton IconKind="refresh" ToolTip.Tip="Refresh" Command="{Binding RefreshCommand}" /&gt;</c>.
/// Transparent until hovered. Always give it a tooltip; the icon alone isn't a label.
/// Look: Themes/Components/ActionButton.axaml.
/// </summary>
public class ActionButton : Button
{
    public static readonly StyledProperty<string?> IconKindProperty =
        AvaloniaProperty.Register<ActionButton, string?>(nameof(IconKind));

    /// <summary>Which icon to show; one of <see cref="Controls.IconKind"/>.</summary>
    public string? IconKind
    {
        get => GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }
}
