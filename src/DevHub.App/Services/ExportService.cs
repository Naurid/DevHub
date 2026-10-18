using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using DevHub.Controls;
using DevHub.Sdk.Core.Charts;
using DevHub.Sdk.Core.Tables;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Services;

/// <summary>
/// Avalonia-backed <see cref="IExportService"/>: PNGs through <see cref="ChartImage"/>, files through the dialog
/// service's save picker, the clipboard of the main window (handed over with <see cref="Attach"/>, like the dialogs).
/// </summary>
public sealed class ExportService(IDialogService dialogs, IStatusService status) : IExportService
{
    private TopLevel? _window;

    /// <summary>Infrastructure hook, called once from App startup — not by ViewModels.</summary>
    public void Attach(TopLevel window) => _window = window;

    public async Task<bool> SaveChartAsync(ChartKind kind, ChartData data, string suggestedName)
    {
        var path = await dialogs.PickSaveFileAsync(L.T("Save Chart as Image"), FileName(suggestedName, "png"), L.T("PNG image"), "png");
        if (path is null)
            return false;
        try
        {
            await File.WriteAllBytesAsync(path, ChartImage.Png(kind, data));
            status.Show(L.T($"Saved {Path.GetFileName(path)}"));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            status.Show(L.T($"The image couldn't be saved: {e.Message}"));
            return false;
        }
    }

    public async Task CopyChartAsync(ChartKind kind, ChartData data)
    {
        if (_window?.Clipboard is not { } clipboard)
            return;
        // The clipboard reads the bitmap when something pastes (not now): it must stay alive, and flushing writes it
        // out at once so it's still there after DevHub closes.
        var bitmap = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(ChartImage.Png(kind, data)));
        await clipboard.SetBitmapAsync(bitmap);
        await clipboard.FlushAsync();
        status.Show(L.T("Chart copied: paste it into your document"));
    }

    public async Task<bool> SaveTableAsync(TableData table, string suggestedName)
    {
        var path = await dialogs.PickSaveFileAsync(L.T("Export Table as CSV"), FileName(suggestedName, "csv"), L.T("CSV (comma-separated)"), "csv");
        if (path is null)
            return false;
        try
        {
            // With a byte order mark, so Excel reads accents (é, ü) right.
            await File.WriteAllTextAsync(path, table.ToCsv(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            status.Show(L.T($"Saved {Path.GetFileName(path)}"));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            status.Show(L.T($"The table couldn't be saved: {e.Message}"));
            return false;
        }
    }

    public async Task CopyTableAsync(TableData table)
    {
        if (_window?.Clipboard is not { } clipboard)
            return;
        await clipboard.SetTextAsync(table.ToTsv());
        status.Show(L.T($"Table copied ({table.Rows.Count} rows): paste it into a document or spreadsheet"));
    }

    public async Task CopyTextAsync(string text)
    {
        if (_window?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async Task<bool> SaveImageAsync(byte[] png, string suggestedName)
    {
        var path = await dialogs.PickSaveFileAsync(L.T("Save as Image"), FileName(suggestedName, "png"), L.T("PNG image"), "png");
        if (path is null)
            return false;
        try
        {
            await File.WriteAllBytesAsync(path, png);
            status.Show(L.T($"Saved {Path.GetFileName(path)}"));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            status.Show(L.T($"The image couldn't be saved: {e.Message}"));
            return false;
        }
    }

    public async Task CopyImageAsync(byte[] png)
    {
        if (_window?.Clipboard is not { } clipboard)
            return;
        var bitmap = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(png));
        await clipboard.SetBitmapAsync(bitmap);
        await clipboard.FlushAsync();
        status.Show(L.T("Image copied: paste it into your document"));
    }

    public async Task<bool> SaveTextAsync(string text, string suggestedName, string extension, string typeName)
    {
        var path = await dialogs.PickSaveFileAsync(L.T("Save As"), FileName(suggestedName, extension), typeName, extension);
        if (path is null)
            return false;
        try
        {
            await File.WriteAllTextAsync(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            status.Show(L.T($"Saved {Path.GetFileName(path)}"));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            status.Show(L.T($"The file couldn't be saved: {e.Message}"));
            return false;
        }
    }

    public async Task<byte[]?> GetClipboardImageAsync()
    {
        if (_window?.Clipboard is not { } clipboard)
            return null;
        try
        {
            using var bitmap = await clipboard.TryGetBitmapAsync();
            if (bitmap is null)
                return null;
            using var stream = new MemoryStream();
            bitmap.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            return stream.ToArray();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Some apps put formats on the clipboard we can't decode: no picture, rather than an error.
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetClipboardFilesAsync()
    {
        if (_window?.Clipboard is not { } clipboard)
            return [];
        var files = await clipboard.TryGetFilesAsync();
        return files?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
    }

    public async Task<string?> GetClipboardTextAsync() =>
        _window?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

    // "Velocity: SHOP" → "Velocity SHOP.png"
    private static string FileName(string name, string extension)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? ' ' : c).ToArray()).Trim();
        return (clean.Length == 0 ? "export" : clean) + "." + extension;
    }
}
