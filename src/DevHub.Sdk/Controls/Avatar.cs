using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DevHub.Controls;

/// <summary>
/// A round account picture (Fork / GitHub style). <see cref="ImageData"/> is the downloaded image
/// (PNG / JPEG bytes from a ViewModel, which can't hold Avalonia bitmaps); without it, or while it
/// loads, the first letter of <see cref="Text"/> shows on the accent color.
/// <c>&lt;c:Avatar ImageData="{Binding AvatarImage}" Text="{Binding Account}" Width="{DynamicResource Size.Avatar}" Height="{DynamicResource Size.Avatar}" /&gt;</c>
/// </summary>
public class Avatar : Control
{
    public static readonly StyledProperty<byte[]?> ImageDataProperty =
        AvaloniaProperty.Register<Avatar, byte[]?>(nameof(ImageData));

    /// <summary>Name for the fallback letter (the account's login).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(Text));

    private Bitmap? _bitmap;

    static Avatar()
    {
        AffectsRender<Avatar>(ImageDataProperty, TextProperty);
    }

    public byte[]? ImageData { get => GetValue(ImageDataProperty); set => SetValue(ImageDataProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageDataProperty)
            ReleaseBitmap();
    }

    /// <summary>Decodes <see cref="ImageData"/> the first time it's drawn (again after being off screen).</summary>
    private Bitmap? EnsureBitmap()
    {
        if (_bitmap is null && ImageData is { Length: > 0 } data)
        {
            try
            {
                using var stream = new MemoryStream(data);
                _bitmap = new Bitmap(stream);
            }
            catch (Exception)
            {
                // Not an image after all: keep the letter.
            }
        }
        return _bitmap;
    }

    // Not disposed: the render thread may still be drawing the last frame with it. An avatar is a
    // few KB; the garbage collector frees it.
    private void ReleaseBitmap() => _bitmap = null;

    // The fallback colors follow the theme: redraw when it switches (subscribed only while on screen).
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
        ReleaseBitmap();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
            return;
        var rect = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);

        if (EnsureBitmap() is { } bitmap)
        {
            using (context.PushGeometryClip(new EllipseGeometry(rect)))
                context.DrawImage(bitmap, rect);
            return;
        }

        context.DrawEllipse(Brush("Brush.Accent", Brushes.SteelBlue), null, rect.Center, size / 2, size / 2);
        var letter = string.IsNullOrEmpty(Text) ? "?" : Text[..1].ToUpperInvariant();
        var text = new FormattedText(letter, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Typeface.Default, size * 0.5, Brush("Brush.Text.OnAccent", Brushes.White));
        context.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Center.Y - text.Height / 2));
    }

    private IBrush Brush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : fallback;
}
