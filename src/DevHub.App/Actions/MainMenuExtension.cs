using Avalonia.Markup.Xaml;

namespace DevHub.Actions;

/// <summary>The main menu's top-level items (see <see cref="MainMenu"/>): <c>ItemsSource="{a:MainMenu}"</c>.</summary>
public sealed class MainMenuExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        ActionExtension.Registry is { } registry ? MainMenu.Build(registry) : Array.Empty<object>();
}
