using Avalonia;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace DevHub.Actions;

// XAML access to the ActionRegistry. A markup extension is a small class XAML calls while
// loading: {a:Action View.ToggleTheme} constructs ActionExtension("View.ToggleTheme") and
// uses whatever ProvideValue returns. Declare the prefix with
// xmlns:a="clr-namespace:DevHub.Actions".
//
// They read App.Services (the DI container) because XAML can't constructor-inject. It is null
// in the XAML previewer, so they return null / empty there instead of crashing.

/// <summary><c>&lt;c:ActionButton Action="{a:Action Repositories.Rescan}" /&gt;</c></summary>
public sealed class ActionExtension(string id) : MarkupExtension
{
    public string Id { get; set; } = id;

    // UnsetValue = "no value" for XAML (used in the previewer, where there is no registry).
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        (object?)Registry?.Get(Id) ?? AvaloniaProperty.UnsetValue;

    internal static ActionRegistry? Registry =>
        (App.Services as IServiceProvider)?.GetService<ActionRegistry>();
}

/// <summary>
/// Menu items for a comma-separated id list, "-" for separators:
/// <c>&lt;ContextMenu ItemsSource="{a:ActionMenu 'Repositories.Rescan,-,View.ToggleTheme'}" /&gt;</c>
/// </summary>
public sealed class ActionMenuExtension(string ids) : MarkupExtension
{
    public string Ids { get; set; } = ids;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var registry = ActionExtension.Registry;
        if (registry is null)
            return Array.Empty<object>();

        var ids = Ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return MenuBuilder.Build(registry, ids);
    }
}

/// <summary>Every registered action, e.g. for a shortcut list: <c>ItemsSource="{a:ActionList}"</c>.</summary>
public sealed class ActionListExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        (object?)ActionExtension.Registry?.All ?? Array.Empty<AppAction>();
}

/// <summary>
/// Just an action's command, for plain Buttons that show their own content (e.g. the
/// status bar's "Dark" widget): <c>Command="{a:ActionCommand View.ToggleTheme}"</c>.
/// </summary>
public sealed class ActionCommandExtension(string id) : MarkupExtension
{
    public string Id { get; set; } = id;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        (object?)ActionExtension.Registry?.Get(Id).Command ?? AvaloniaProperty.UnsetValue;
}

/// <summary>The main menu's top-level items (see <see cref="MainMenu"/>): <c>ItemsSource="{a:MainMenu}"</c>.</summary>
public sealed class MainMenuExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        ActionExtension.Registry is { } registry ? MainMenu.Build(registry) : Array.Empty<object>();
}
