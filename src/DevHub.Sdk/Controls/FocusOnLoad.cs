using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace DevHub.Controls;

/// <summary>
/// Puts keyboard focus on an element as soon as it appears:
/// <c>&lt;TextBox c:FocusOnLoad.IsEnabled="True" /&gt;</c>. Used for popups and dialogs whose
/// first field should be ready to type into (Find Action's search box).
/// </summary>
/// <remarks>
/// An "attached property": a property defined here but set on other controls, like
/// <c>Grid.Row</c>. Setting it subscribes to the control's AttachedToVisualTree event; the focus
/// call is posted to run once layout is done (focusing too early is ignored).
/// </remarks>
public static class FocusOnLoad
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("IsEnabled", typeof(FocusOnLoad));

    static FocusOnLoad()
    {
        IsEnabledProperty.Changed.AddClassHandler<InputElement>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(InputElement element) => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(InputElement element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(InputElement element, AvaloniaPropertyChangedEventArgs e)
    {
        // Matching -= when turned off; the subscription lives as long as the element.
        element.AttachedToVisualTree -= OnAttached;
        if (e.NewValue is true)
            element.AttachedToVisualTree += OnAttached;
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is InputElement element)
            Dispatcher.UIThread.Post(() => element.Focus(), DispatcherPriority.Loaded);
    }
}
