using System.Globalization;
using Avalonia.Data.Converters;

namespace DevHub.Converters;

/// <summary>
/// Multiplies a number by the fraction given as ConverterParameter, e.g. "a tool window may be
/// at most 45% of the window": <c>MaxWidth="{Binding #Root.Bounds.Width,
/// Converter={StaticResource Fraction}, ConverterParameter=0.45}"</c>.
/// </summary>
public sealed class FractionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double size)
            return double.PositiveInfinity; // no limit until the size is known

        var fraction = parameter switch
        {
            double d => d,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => 1.0,
        };
        return size * fraction;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
