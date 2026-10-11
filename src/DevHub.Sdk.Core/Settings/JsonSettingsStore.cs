using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevHub.Sdk.Core.Settings;

/// <summary>
/// Stores settings as pretty-printed JSON at <c>&lt;baseDir&gt;/DevHub/settings.json</c>
/// (baseDir defaults to %APPDATA%). Reads never throw into the UI: a missing or corrupt file
/// yields fresh defaults.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
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
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SettingsJson.Options) ?? new AppSettings();
            MigrateFeatureSections(settings);
            return settings;
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
        var json = JsonSerializer.Serialize(settings, SettingsJson.Options);
        File.WriteAllText(_filePath, json);
    }

    /// <summary>
    /// Before the plugin split, the Git and Projects settings were top-level fields ("git",
    /// "languageTools", "repositoryFolders"). They arrive in <see cref="AppSettings.Extra"/>
    /// now; move them into their plugin sections (unless that section already exists), so
    /// nobody loses their folders or Open With choices. The next save writes the new shape.
    /// </summary>
    private static void MigrateFeatureSections(AppSettings settings)
    {
        if (settings.Extra is not { Count: > 0 } extra)
            return;

        if (Take(extra, "git") is JsonObject git)
            settings.Plugins.TryAdd("devhub.git", git);

        var languageTools = Take(extra, "languageTools");
        var repositoryFolders = Take(extra, "repositoryFolders");
        if ((languageTools ?? repositoryFolders) is not null && !settings.Plugins.ContainsKey("devhub.projects"))
        {
            var projects = new JsonObject();
            if (languageTools is not null)
                projects["languageTools"] = languageTools;
            if (repositoryFolders is not null)
                projects["repositoryFolders"] = repositoryFolders;
            settings.Plugins["devhub.projects"] = projects;
        }

        if (extra.Count == 0)
            settings.Extra = null;
    }

    private static JsonNode? Take(Dictionary<string, JsonElement> extra, string key)
    {
        // Keys are compared as written: older versions always wrote camelCase.
        if (!extra.Remove(key, out var element) || element.ValueKind == JsonValueKind.Null)
            return null;
        return JsonNode.Parse(element.GetRawText());
    }
}
