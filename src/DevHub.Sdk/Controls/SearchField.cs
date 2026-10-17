using Avalonia.Controls;
using Avalonia.Input;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Controls;

/// <summary>
/// A TextBox with a search icon on the left and a clear button on the right (shown while
/// there is text). Escape clears it. Bind <c>Text</c> as usual.
/// </summary>
/// <remarks>
/// It keeps TextBox's look (<see cref="StyleKeyOverride"/>), so the Fluent TextBox template,
/// our TextBox sizing and the FluentMapping colors all apply. The icon and clear button come
/// from the <c>TextBox.search</c> styles in Themes/Controls.axaml, which the constructor
/// opts into by adding the "search" class.
/// </remarks>
public class SearchField : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    public SearchField()
    {
        Classes.Add("search");
        PlaceholderText = L.T("Search");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text))
        {
            Clear();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
