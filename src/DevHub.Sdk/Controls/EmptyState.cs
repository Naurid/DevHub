using Avalonia;
using Avalonia.Controls;

namespace DevHub.Controls;

/// <summary>
/// Centered placeholder for an empty list or panel: optional icon, a title, an optional
/// description, and optional actions (the Content, e.g. a Button).
/// <c>&lt;c:EmptyState Title="No repositories yet" Description="Add a folder to scan."&gt;&lt;Button … /&gt;&lt;/c:EmptyState&gt;</c>.
/// Look: Themes/Components/EmptyState.axaml.
/// </summary>
public class EmptyState : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Description));

    public static readonly StyledProperty<string?> IconKindProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(IconKind));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Optional large icon above the title; one of <see cref="Controls.IconKind"/>.</summary>
    public string? IconKind
    {
        get => GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }
}
