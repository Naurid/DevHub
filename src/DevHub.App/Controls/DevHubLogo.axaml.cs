using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DevHub.Controls;

public partial class DevHubLogo : UserControl
{
    /// <summary>
    /// The logo's lines in a 64 × 64 unit box (the circle at 32,32 r=4.5 is drawn separately).
    /// Single source for both the control and <see cref="CreateWindowIcon"/>.
    /// </summary>
    public static Geometry LinesGeometry { get; } = StreamGeometry.Parse(
        "M22,18 L8,32 L22,46 M42,18 L56,32 L42,46 M22,18 L29.38,28.34 M22,46 L29.38,35.66 M36.5,32 L56,32");

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<DevHubLogo, double>(nameof(LineThickness), 2.0);

    /// <summary>Line weight in logo units (the drawing is 64 × 64 units).</summary>
    public double LineThickness
    {
        get => GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    public DevHubLogo()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Renders the logo in <paramref name="color"/> to a bitmap for <c>Window.Icon</c>
    /// (title bar + taskbar). A WindowIcon is a picture, not a live control, so it can't
    /// follow DynamicResource; call this again when the accent changes.
    /// </summary>
    /// <param name="color">Stroke color, normally the current accent.</param>
    /// <param name="pixelSize">Bitmap size; Windows scales it down for the title bar.</param>
    public static WindowIcon CreateWindowIcon(Color color, int pixelSize = 64)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(pixelSize, pixelSize), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            // Thicker than the in-app default: the icon is shown as small as 16 px.
            var pen = new Pen(new SolidColorBrush(color), thickness: 4.5,
                lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var scale = pixelSize / 64.0;
            using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            {
                context.DrawGeometry(null, pen, LinesGeometry);
                context.DrawEllipse(null, pen, new Point(32, 32), 4.5, 4.5);
            }
        }

        // WindowIcon reads the image; go through a PNG stream so it owns its own copy
        // and the RenderTargetBitmap can be disposed here.
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}
