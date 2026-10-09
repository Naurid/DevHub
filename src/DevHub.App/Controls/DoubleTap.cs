using System.Windows.Input;
using Avalonia;
using Avalonia.Input;

namespace DevHub.Controls;

/// <summary>
/// Runs a command on double-click: <c>&lt;Grid c:DoubleTap.Command="{Binding OpenCommand}" /&gt;</c>.
/// For "open" gestures on tree and list rows (opening a repository tab from the Repositories
/// tool window) without code-behind.
/// </summary>
/// <remarks>
/// Attached property like <see cref="FocusOnLoad"/>: setting it subscribes to the element's
/// DoubleTapped event, clearing it unsubscribes. The element needs a Background (Transparent is
/// fine) to receive clicks on its empty parts.
/// Not for Enter: key events start at the focused list/tree item and bubble *up*, so they never
/// pass through an element inside the item's template.
/// </remarks>
public static class DoubleTap
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<InputElement, ICommand?>("Command", typeof(DoubleTap));

    static DoubleTap()
    {
        CommandProperty.Changed.AddClassHandler<InputElement>(OnCommandChanged);
    }

    public static ICommand? GetCommand(InputElement element) => element.GetValue(CommandProperty);

    public static void SetCommand(InputElement element, ICommand? value) => element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(InputElement element, AvaloniaPropertyChangedEventArgs e)
    {
        element.DoubleTapped -= OnDoubleTapped;
        if (e.NewValue is not null)
            element.DoubleTapped += OnDoubleTapped;
    }

    private static void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not InputElement element || GetCommand(element) is not { } command || !command.CanExecute(null))
            return;

        command.Execute(null);
        e.Handled = true;
    }
}
