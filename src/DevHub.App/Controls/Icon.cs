using Avalonia;
using Avalonia.Styling;
using SvgControl = Avalonia.Svg.Skia.Svg;

namespace DevHub.Controls;

/// <summary>
/// A JetBrains "expui" icon by name: <c>&lt;c:Icon Kind="folder" /&gt;</c>.
/// Loads <c>Assets/Icons/{Kind}.svg</c>, or <c>{Kind}_dark.svg</c> when the element's
/// theme is Dark, and swaps automatically when the theme changes.
/// Size comes from the <c>Size.Icon</c> token (see Themes/Controls.axaml).
/// </summary>
/// <remarks>
/// Derives from Svg.Skia's <see cref="SvgControl"/> (Avalonia.Svg.Skia.Svg, aliased because
/// "Svg" is also a namespace), which does the actual rendering;
/// this class only decides which file to show. Names are listed in <see cref="IconKind"/>.
/// </remarks>
public class Icon : SvgControl
{
    private const string IconFolder = "avares://DevHub.App/Assets/Icons/";

    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<Icon, string?>(nameof(Kind));

    /// <summary>File name without extension or theme suffix, e.g. "folder". See <see cref="IconKind"/>.</summary>
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

    private void UpdatePath()
    {
        if (string.IsNullOrEmpty(Kind))
        {
            Path = null;
            return;
        }

        var suffix = ActualThemeVariant == ThemeVariant.Dark ? "_dark" : "";
        Path = $"{IconFolder}{Kind}{suffix}.svg";
    }
}
