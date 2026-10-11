using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace DevHub.Converters;

/// <summary>
/// A tree row's nesting depth → its left margin (depth × <see cref="Step"/>), for lists that show a
/// tree as indented rows (Local Changes' folders). The step comes from a size token:
/// <c>&lt;conv:IndentConverter x:Key="Indent" Step="{StaticResource Size.TreeIndent}" /&gt;</c>.
/// </summary>
public sealed class IndentConverter : IValueConverter
{
    /// <summary>Indent per level, in DIPs.</summary>
    public double Step { get; set; } = 16;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int depth ? new Thickness(depth * Step, 0, 0, 0) : new Thickness(0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
