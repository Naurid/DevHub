using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevHub.ViewModels.Dialogs;

/// <summary>
/// Base for modal dialogs shown with <c>IDialogService.ShowDialogAsync</c>. The dialog window
/// (Views/Dialogs/DialogWindow) supplies the frame and the OK / Cancel / Apply footer; the
/// subclass's View (found by the ViewLocator) is the content.
/// </summary>
/// <remarks>
/// The ViewModel never touches the window. To close, it raises <see cref="CloseRequested"/>
/// and the dialog service closes the window. Enter = OK and Esc = Cancel (button IsDefault/IsCancel).
/// </remarks>
public abstract partial class DialogViewModelBase : ViewModelBase
{
    /// <summary>Window title.</summary>
    public abstract string Title { get; }

    /// <summary>Initial window size in DIPs.</summary>
    public virtual double Width => 760;
    public virtual double Height => 520;

    /// <summary>Smallest size the user can resize to.</summary>
    public virtual double MinWidth => 480;
    public virtual double MinHeight => 320;

    /// <summary>Label of the OK button: say what it does ("Discard"), JetBrains-style.</summary>
    public virtual string OkText => "OK";

    /// <summary>The OK button is destructive (red, not accent): discard, delete…</summary>
    public virtual bool IsDestructive => false;

    /// <summary>Show an Apply button (settings-style dialogs) next to OK/Cancel.</summary>
    public virtual bool ShowApply => false;

    /// <summary>Raised with true (OK) or false (Cancel); the dialog service closes the window.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Whether there are changes for Apply to save. Subclasses call ApplyCommand.NotifyCanExecuteChanged() when it changes.</summary>
    protected virtual bool CanApply() => true;

    /// <summary>Saves the dialog's changes. Return false to keep the dialog open (e.g. invalid input).</summary>
    protected virtual Task<bool> ApplyChangesAsync() => Task.FromResult(true);

    [RelayCommand]
    private async Task OkAsync()
    {
        if (!CanApply() || await ApplyChangesAsync())
            CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        await ApplyChangesAsync();
        ApplyCommand.NotifyCanExecuteChanged();
    }
}
