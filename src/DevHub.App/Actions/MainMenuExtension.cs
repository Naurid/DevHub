using Avalonia.Markup.Xaml;

namespace DevHub.Actions;

/// <summary>The main menu's top-level items (see <see cref="MainMenu"/>): <c>ItemsSource="{a:MainMenu}"</c>.</summary>
public sealed class MainMenuExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        ActionExtension.Registry is { } registry ? MainMenu.Build(registry) : Array.Empty<object>();
}

/// <summary>
/// The title-bar toolbar: actions with a <see cref="AppAction.ToolbarOrder"/>, arranged, with
/// <c>null</c> for a separator: <c>ItemsSource="{a:ToolbarActions}"</c>.
/// </summary>
public sealed class ToolbarActionsExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        ActionExtension.Registry is { } registry
            ? ActionLayout.Arrange(registry.All, a => a.ToolbarOrder).Select(a => (object?)a ?? ToolbarSeparatorItem.Instance).ToList()
            : Array.Empty<object>();
}

/// <summary>A separator in <see cref="ToolbarActionsExtension"/>'s list (a DataTemplate turns it into a line).</summary>
public sealed class ToolbarSeparatorItem
{
    public static readonly ToolbarSeparatorItem Instance = new();
}

/// <summary>The Welcome view's links: actions with a <see cref="AppAction.WelcomeOrder"/>, in order.</summary>
public sealed class WelcomeActionsExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        ActionExtension.Registry is { } registry
            ? ActionLayout.Arrange(registry.All, a => a.WelcomeOrder).OfType<AppAction>().ToList()
            : Array.Empty<AppAction>();
}
