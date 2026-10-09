using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using DevHub.Actions;

namespace DevHub.Controls;

/// <summary>
/// Square, icon-only button used in toolbars and tool-window headers (JetBrains "action button").
/// Usually bound to an action, which supplies the command, icon and tooltip in one go:
/// <c>&lt;c:ActionButton Action="{a:Action Repositories.Rescan}" /&gt;</c>.
/// Can also be wired by hand: <c>IconKind="refresh" ToolTip.Tip="Refresh" Command="…"</c>.
/// Transparent until hovered. Look: Themes/Components/ActionButton.axaml.
/// </summary>
public class ActionButton : Button
{
    public static readonly StyledProperty<string?> IconKindProperty =
        AvaloniaProperty.Register<ActionButton, string?>(nameof(IconKind));

    public static readonly StyledProperty<AppAction?> ActionProperty =
        AvaloniaProperty.Register<ActionButton, AppAction?>(nameof(Action));

    /// <summary>Which icon to show; one of <see cref="Controls.IconKind"/>.</summary>
    public string? IconKind
    {
        get => GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    /// <summary>
    /// The action this button runs. Sets Command, IconKind and the tooltip
    /// ("Rescan (F5)") from it; enabled state follows the action's command.
    /// </summary>
    public AppAction? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ActionProperty && change.NewValue is AppAction action)
        {
            Command = action.Command;
            IconKind = action.IconKind;
            ToolTip.SetTip(this, action.ToolTip);
            // Icon-only button: give screen readers (and UI Automation) a name to announce.
            AutomationProperties.SetName(this, action.Text);
        }
    }
}
