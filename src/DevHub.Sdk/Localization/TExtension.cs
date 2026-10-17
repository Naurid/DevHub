using Avalonia.Markup.Xaml;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Localization;

/// <summary>
/// Translated text in XAML: <c>Text="{l:T 'Edit Selected…'}"</c>, with <c>xmlns:l="using:DevHub.Localization"</c>.
/// The English text is the key (<see cref="L.T(string)"/>); the language is fixed at startup, so the value is read once.
/// The view's own assembly (the host of its avares:// address) is asked first, as <c>L.T</c> does in C#.
/// </summary>
public sealed class TExtension(string text) : MarkupExtension
{
    public string Text { get; set; } = text;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var uri = (serviceProvider.GetService(typeof(IUriContext)) as IUriContext)?.BaseUri;
        return L.T(Text, uri is { IsAbsoluteUri: true, Scheme: "avares" } ? uri.Host : null);
    }
}
