using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace DevHub.Controls;

/// <summary>
/// Lets a ViewModel move keyboard focus without knowing about controls: bind
/// <c>c:FocusRequest.Token="{Binding CommitFocusRequest}"</c> to a number, and every time the
/// ViewModel increments it the element takes focus (e.g. Ctrl+K → the commit message box).
/// </summary>
/// <remarks>
/// Same attached-property pattern as <see cref="FocusOnLoad"/>, but triggered by a value change
/// instead of appearing. A number rather than a bool, so asking twice in a row works.
/// </remarks>
public static class FocusRequest
{
    public static readonly AttachedProperty<int> TokenProperty =
        AvaloniaProperty.RegisterAttached<InputElement, int>("Token", typeof(FocusRequest));

    static FocusRequest()
    {
        TokenProperty.Changed.AddClassHandler<InputElement>(OnTokenChanged);
    }

    public static int GetToken(InputElement element) => element.GetValue(TokenProperty);

    public static void SetToken(InputElement element, int value) => element.SetValue(TokenProperty, value);

    private static void OnTokenChanged(InputElement element, AvaloniaPropertyChangedEventArgs e)
    {
        // 0 is the initial value: binding it isn't a request.
        if (e.NewValue is int and > 0)
            Dispatcher.UIThread.Post(() => element.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }
}
