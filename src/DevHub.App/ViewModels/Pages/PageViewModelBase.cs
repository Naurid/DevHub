namespace DevHub.ViewModels.Pages;

/// <summary>
/// A navigable page. The shell lists these in the sidebar (by <see cref="Title"/> +
/// <see cref="Icon"/>) and swaps the active one into the content area.
/// </summary>
public abstract class PageViewModelBase : ViewModelBase
{
    /// <summary>Label shown in the sidebar.</summary>
    public abstract string Title { get; }

    /// <summary>Segoe Fluent Icons / MDL2 glyph shown next to the title.</summary>
    public abstract string Icon { get; }
}
