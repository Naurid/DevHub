using System.Text.Json;

namespace DevHub.Sdk.Core.Settings;

/// <summary>How settings.json is written: camelCase, indented, read case-insensitively. Shared by the file and the plugin sections.</summary>
internal static class SettingsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
