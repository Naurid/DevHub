using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DevHub.Services;

/// <summary>
/// Avalonia-backed <see cref="IDialogService"/>. It needs a <see cref="TopLevel"/> (the window)
/// to open pickers, which the composition root hands it via <see cref="Attach"/> once the
/// window exists. Keeping the Avalonia types here — not in the interface — means ViewModels
/// stay free of windowing concerns.
/// </summary>
public class DialogService : IDialogService
{
    private TopLevel? _topLevel;

    /// <summary>Infrastructure hook, called once from App startup — not by ViewModels.</summary>
    public void Attach(TopLevel topLevel) => _topLevel = topLevel;

    public async Task<string?> PickFolderAsync(string title)
    {
        if (_topLevel is null)
            return null;

        var folders = await _topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task<string?> PickFileAsync(string title, string filterName, IReadOnlyList<string> patterns)
    {
        if (_topLevel is null)
            return null;

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(filterName) { Patterns = patterns.ToList() } }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
