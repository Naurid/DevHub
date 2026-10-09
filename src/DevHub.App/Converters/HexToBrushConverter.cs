using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DevHub.Converters;

/// <summary>
/// "#RRGGBB" → a brush, for color swatches bound to ViewModel strings (ViewModels keep colors
/// as plain strings so they don't depend on Avalonia types). Invalid text → transparent.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && Color.TryParse(hex, out var color)
            ? new SolidColorBrush(color)
            : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
