namespace DevHub.ViewModels.Shell;

/// <summary>
/// A small piece of UI a plugin puts in the title bar, next to the "DevHub › tab" breadcrumb
/// (e.g. Git's branch widget). Registered in DI as <see cref="TitleBarWidgetViewModelBase"/>;
/// the ViewLocator shows its View. Make interactive parts
/// <c>WindowDecorationProperties.ElementRole="User"</c> so clicks don't drag the window.
/// </summary>
public abstract class TitleBarWidgetViewModelBase : ViewModelBase
{
    /// <summary>Left to right, lowest first.</summary>
    public virtual int Order => 0;
}

/// <summary>
/// A widget a plugin puts in the status bar, left of the built-in theme and density widgets.
/// Registered in DI as <see cref="StatusBarWidgetViewModelBase"/>; the ViewLocator shows its View
/// (usually a <c>Button Classes="statusWidget"</c> that runs an action).
/// </summary>
public abstract class StatusBarWidgetViewModelBase : ViewModelBase
{
    /// <summary>Left to right, lowest first.</summary>
    public virtual int Order => 0;
}
