using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DevHub.Sdk.Core.Settings;

/// <summary>
/// Everything DevHub persists between runs. The shell's own sections are typed properties;
/// each plugin keeps its settings in <see cref="Plugins"/> under its id (see
/// <see cref="ISettingsSection"/>), so the shell doesn't depend on any plugin. Only user
/// choices are stored, so the file stays small and readable.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Tool-window layout from the last session. Null until the first save, so the app can
    /// tell "never saved" (use its own defaults) from "user closed everything".
    /// </summary>
    public ToolWindowLayout? ToolWindows { get; set; }

    /// <summary>Theme, accent and density; null until first changed (the app keeps its built-in look).</summary>
    public AppearanceSettings? Appearance { get; set; }

    /// <summary>Open editor tabs from the last session; null until the first save.</summary>
    public EditorLayout? Editor { get; set; }

    /// <summary>
    /// Ids of the plugins the user switched on (Settings → Plugins, first-run picker). Null until
    /// the first choice: then every installed plugin loads. Changes apply after a restart.
    /// </summary>
    public List<string>? EnabledPlugins { get; set; }

    /// <summary>
    /// Plugin id → that plugin's settings, kept as raw JSON. Sections of plugins that are switched
    /// off (or unknown to this version) are kept as they are, so turning a plugin back on finds
    /// its settings again.
    /// </summary>
    public Dictionary<string, JsonObject> Plugins { get; set; } = new();

    /// <summary>Fields this version doesn't know (written by a newer DevHub): kept, not dropped on save.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>The plugin section <typeparamref name="T"/>; defaults when missing or unreadable.</summary>
    public T GetSection<T>() where T : class, ISettingsSection, new()
    {
        if (!Plugins.TryGetValue(T.SectionId, out var json))
            return new T();
        try
        {
            return json.Deserialize<T>(SettingsJson.Options) ?? new T();
        }
        catch (JsonException)
        {
            // A broken section must not break the plugin (or the rest of the file): defaults.
            return new T();
        }
    }

    /// <summary>Replaces the plugin section <typeparamref name="T"/>.</summary>
    public void SetSection<T>(T section) where T : class, ISettingsSection, new() =>
        Plugins[T.SectionId] = JsonSerializer.SerializeToNode(section, SettingsJson.Options)!.AsObject();
}
