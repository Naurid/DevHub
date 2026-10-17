using Avalonia.Markup.Xaml;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Localization;

/// <summary>
/// Translated text in XAML: <c>Text="{l:T 'Edit Selected…'}"</c>, with <c>xmlns:l="using:DevHub.Localization"</c>.
/// The English text is the key (<see cref="L.T(string)"/>); the language is fixed at startup, so the value is read once.
/// </summary>
public sealed class TExtension(string text) : MarkupExtension
{
    public string Text { get; set; } = text;

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Text);
}
