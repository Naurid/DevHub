using Avalonia;
using Avalonia.Controls;

namespace DevHub.Controls;

/// <summary>
/// One line of a settings page: a label (plus optional description) in a fixed-width
/// left column and the editor (the Content) on the right, so rows line up.
/// <c>&lt;c:SettingsRow Label="Theme" Description="…"&gt;&lt;ComboBox … /&gt;&lt;/c:SettingsRow&gt;</c>.
/// Look: Themes/Components/SettingsRow.axaml.
/// </summary>
public class SettingsRow : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SettingsRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsRow, string?>(nameof(Description));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Optional hint shown under the label in small, secondary text.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
