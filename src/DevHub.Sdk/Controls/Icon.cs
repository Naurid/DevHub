using Avalonia;
using Avalonia.Styling;
using SvgControl = Avalonia.Svg.Skia.Svg;

namespace DevHub.Controls;

/// <summary>
/// A JetBrains "expui" icon by name: <c>&lt;c:Icon Kind="folder" /&gt;</c>.
/// Loads <c>Assets/Icons/{Kind}.svg</c> from DevHub.Sdk, or <c>{Kind}_dark.svg</c> when the
/// element's theme is Dark, and swaps automatically when the theme changes. A plugin's own
/// icon is given as a full URI without the extension, e.g.
/// <c>Kind="avares://DevHub.Git/Assets/Icons/branch"</c> (the same <c>_dark</c> rule applies).
/// Size comes from the <c>Size.Icon</c> token (see Themes/Controls.axaml).
/// </summary>
/// <remarks>
/// Derives from Svg.Skia's <see cref="SvgControl"/> (Avalonia.Svg.Skia.Svg, aliased because
/// "Svg" is also a namespace), which does the actual rendering;
/// this class only decides which file to show. Names are listed in <see cref="IconKind"/>.
/// </remarks>
public class Icon : SvgControl
{
    private const string IconFolder = "avares://DevHub.Sdk/Assets/Icons/";

    /// <summary>
    /// CSS applied to the SVG while disabled. Needed because Svg.Skia draws straight to
    /// Skia and ignores Avalonia's Opacity; CSS inside the SVG is the way to fade it.
    /// Only leaf shapes are listed so nested groups don't multiply the opacity.
    /// </summary>
    private const string DisabledCss =
        "path, circle, rect, ellipse, line, polyline, polygon { opacity: 0.4; }";

    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<Icon, string?>(nameof(Kind));

    /// <summary>
    /// File name without extension or theme suffix, e.g. "folder" (see <see cref="IconKind"/>),
    /// or a full "avares://…" path without extension for an icon shipped by a plugin.
    /// </summary>
    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Icon() : base(new Uri(IconFolder))
    {
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty)
            UpdatePath();
        else if (change.Property == IsEffectivelyEnabledProperty)
            UpdateDisabledLook();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        // The theme may have changed while we were detached.
        UpdatePath();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => UpdatePath();

    /// <summary>
    /// IsEffectivelyEnabled is false when this icon or any parent (e.g. its Button) is
    /// disabled, so icons inside disabled buttons and menu items fade automatically.
    /// </summary>
    private void UpdateDisabledLook() =>
        SetValue(CssProperty, IsEffectivelyEnabled ? null : DisabledCss);

    private void UpdatePath()
    {
        if (string.IsNullOrEmpty(Kind))
        {
            Path = null;
            return;
        }

        var suffix = ActualThemeVariant == ThemeVariant.Dark ? "_dark" : "";
        var path = Kind.StartsWith("avares://", StringComparison.Ordinal) ? Kind : IconFolder + Kind;
        Path = $"{path}{suffix}.svg";
    }
}
