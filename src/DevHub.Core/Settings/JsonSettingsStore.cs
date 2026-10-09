using System.Text.Json;

namespace DevHub.Core.Settings;

/// <summary>
/// Stores settings as pretty-printed JSON at <c>&lt;baseDir&gt;/DevHub/settings.json</c>
/// (baseDir defaults to %APPDATA%). Reads never throw into the UI: a missing or corrupt file
/// yields fresh defaults.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    /// <param name="baseDirectory">
    /// Root to store under; defaults to the roaming AppData folder. Injectable so tests can use
    /// a temp directory.
    /// </param>
    public JsonSettingsStore(string? baseDirectory = null)
    {
        var root = baseDirectory
                   ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(root, "DevHub", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or unreadable settings must not break the app — start fresh.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_filePath, json);
    }
}
