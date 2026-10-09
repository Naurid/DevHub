using CommunityToolkit.Mvvm.ComponentModel;

namespace DevHub.ViewModels.Dialogs.Settings;

/// <summary>
/// One page of the Settings dialog (Appearance, Open With…). A page edits its own copy of the
/// values; nothing changes until <see cref="Apply"/> (Apply / OK). Cancel just drops the page.
/// </summary>
public abstract partial class SettingsPageViewModelBase : ViewModelBase
{
    /// <summary>Name in the page list and the page header.</summary>
    public abstract string Title { get; }

    /// <summary>Extra words the dialog's search box matches, e.g. "theme accent color".</summary>
    public virtual string Keywords => "";

    /// <summary>True when the page has edits that Apply would save (shown as a dot in the list).</summary>
    [ObservableProperty] private bool _isModified;

    /// <summary>Set by the dialog so it can enable/disable Apply when this page changes.</summary>
    internal Action? Modified { get; set; }

    /// <summary>Saves this page's edits.</summary>
    public abstract void Apply();

    /// <summary>Accessible name of the list item (screen readers announce ToString otherwise).</summary>
    public override string ToString() => Title;

    /// <summary>Call from subclasses whenever the user changes something.</summary>
    protected void MarkModified()
    {
        IsModified = true;
        Modified?.Invoke();
    }
}
