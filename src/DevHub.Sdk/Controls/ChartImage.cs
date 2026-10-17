using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using DevHub.Sdk.Core.Charts;

namespace DevHub.Controls;

/// <summary>
/// A chart drawn off screen as a PNG (to save or copy into a report): any <see cref="ChartKind"/>, with its title, on
/// the theme's background, light by default so it prints well. Used by the export service; works without a window.
/// </summary>
public static class ChartImage
{
    /// <summary>The PNG bytes of <paramref name="data"/> drawn as <paramref name="kind"/>, <paramref name="width"/> ×
    /// <paramref name="height"/> device-independent pixels at <paramref name="scale"/> (2 = sharp when printed).</summary>
    public static byte[] Png(ChartKind kind, ChartData data, double width = 900, double height = 500, ThemeVariant? theme = null,
        double scale = 2, HeatmapColors heatmapColors = HeatmapColors.Shades)
    {
        using var bitmap = Render(kind, data, width, height, theme, scale, heatmapColors);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    /// <summary>The chart as a bitmap (the caller disposes it), e.g. for the clipboard.</summary>
    public static RenderTargetBitmap Render(ChartKind kind, ChartData data, double width = 900, double height = 500, ThemeVariant? theme = null,
        double scale = 2, HeatmapColors heatmapColors = HeatmapColors.Shades)
    {
        var chart = Chart.Create(kind, heatmapColors);
        chart.ExportTheme = theme ?? ThemeVariant.Light;
        chart.ShowTitle = true;
        chart.Data = data;
        var size = new Size(width, height);
        // Space around the chart: a background color from the theme, set after the theme so the lookup uses it.
        chart.Background = Background(chart.ExportTheme);
        chart.Measure(size);
        chart.Arrange(new Rect(size));

        var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(chart);
        return bitmap;
    }

    private static IBrush Background(ThemeVariant theme) =>
        Application.Current?.TryGetResource("Color.Bg.Editor", theme, out var value) == true && value is Color color
            ? new SolidColorBrush(color)
            : theme == ThemeVariant.Dark ? Brushes.Black : Brushes.White;
}
