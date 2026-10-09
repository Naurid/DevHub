using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DevHub.ViewModels.Dialogs;
using DevHub.Views.Dialogs;

namespace DevHub.Services;

/// <summary>
/// Avalonia-backed <see cref="IDialogService"/>. It needs a <see cref="Window"/> to open
/// pickers and own dialogs, which the composition root hands it via <see cref="Attach"/> once
/// the main window exists. Keeping the Avalonia types here — not in the interface — means
/// ViewModels stay free of windowing concerns.
/// </summary>
public class DialogService : IDialogService
{
    /// <summary>
    /// Windows that can own a picker or dialog, innermost last: the main window, then any open
    /// dialogs. Pickers opened from inside a dialog must belong to that dialog, or they'd
    /// appear behind it.
    /// </summary>
    private readonly List<Window> _owners = [];

    private Window? Owner => _owners.Count > 0 ? _owners[^1] : null;

    /// <summary>Infrastructure hook, called once from App startup — not by ViewModels.</summary>
    public void Attach(Window mainWindow) => _owners.Insert(0, mainWindow);

    public async Task<bool> ShowDialogAsync(DialogViewModelBase dialog)
    {
        if (Owner is not { } owner)
            return false;

        var window = new DialogWindow { DataContext = dialog };

        // The ViewModel asks to close via an event; unsubscribed when the window is gone.
        void OnCloseRequested(object? sender, bool result) => window.Close(result);
        dialog.CloseRequested += OnCloseRequested;
        _owners.Add(window);
        try
        {
            // Closing with the window's X (no result) counts as Cancel.
            return await window.ShowDialog<bool?>(owner) == true;
        }
        finally
        {
            _owners.Remove(window);
            dialog.CloseRequested -= OnCloseRequested;
        }
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        if (Owner is not { } owner)
            return null;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task<string?> PickFileAsync(string title, string filterName, IReadOnlyList<string> patterns)
    {
        if (Owner is not { } owner)
            return null;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(filterName) { Patterns = patterns.ToList() } }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
