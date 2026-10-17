using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Sdk.Core.Plugins.Marketplace;

/// <summary>
/// The plugin API version DevHub offers. A plugin built for another version is listed in the
/// marketplace but can't be installed. Bump it when a change to the Sdk breaks existing plugins
/// (a removed or changed member they use), and say so in the release notes.
/// </summary>
public static class PluginApi
{
    public const int Version = 1;

    /// <summary>The marketplace index DevHub reads when no other source is set (docs/PLUGINS.md).</summary>
    public const string DefaultIndexUrl = "https://github.com/Naurid/DevHub-Plugins/releases/download/index/index.json";
}

/// <summary>
/// <c>plugin.json</c> at the top of a plugin package (the .zip), also kept in the installed
/// plugin's folder. The DLLs sit next to it: the plugin's own, never the Sdk or Avalonia.
/// </summary>
public sealed record PluginPackageInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int ApiVersion { get; init; }
    public IReadOnlyList<string> Requires { get; init; } = [];

    public const string FileName = "plugin.json";
}

/// <summary>One plugin in a marketplace index.</summary>
/// <param name="Url">The package (.zip) to download (https).</param>
/// <param name="Sha256">The package's SHA-256, hex: checked before anything is unpacked.</param>
public sealed record IndexEntry(string Id, string Name, string Version, string? Description, string? Category,
    int ApiVersion, IReadOnlyList<string>? Requires, string Url, string Sha256, long Size, string? Homepage)
{
    /// <summary>Built for this DevHub's plugin API.</summary>
    public bool IsCompatible => ApiVersion == PluginApi.Version;
}

/// <summary>What the marketplace offers (every source together) and what went wrong reading a source.</summary>
public sealed record MarketplaceListing(IReadOnlyList<IndexEntry> Entries, IReadOnlyList<string> Problems);

/// <summary>The marketplace's files: the index format, reading sources, comparing versions.</summary>
public static class Marketplace
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>An index file: a JSON array of entries. Entries missing an id, a version, an https (or local file:///) URL or a SHA-256 are left out.</summary>
    public static IReadOnlyList<IndexEntry> ParseIndex(string json)
    {
        var entries = JsonSerializer.Deserialize<List<IndexEntry>>(json, Json) ?? [];
        return entries.Where(e => !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Version)
                                  && (e.Url?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true
                                      || e.Url?.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) == true) // file: a local index, to try your own plugin
                                  && e.Sha256?.Length == 64)
            .ToList();
    }

    /// <summary>Reads every source (the first source wins when two list the same id). A source that fails is reported, not fatal.</summary>
    public static async Task<MarketplaceListing> LoadAsync(HttpClient http, IEnumerable<string> sources, CancellationToken ct = default)
    {
        var entries = new List<IndexEntry>();
        var problems = new List<string>();
        foreach (var source in sources)
        {
            try
            {
                var json = source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? await http.GetStringAsync(source, ct)
                    : await File.ReadAllTextAsync(source, ct); // a local file: for trying a plugin before publishing it
                foreach (var entry in ParseIndex(json))
                {
                    if (entries.All(e => !e.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase)))
                        entries.Add(entry);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or UnauthorizedAccessException or TaskCanceledException)
            {
                problems.Add($"{source}: {ex.Message}");
            }
        }
        return new MarketplaceListing(entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), problems);
    }

    /// <summary>"1.10.0" is newer than "1.9.2" (numbers compared as numbers; anything else as text).</summary>
    public static bool IsNewer(string candidate, string current) =>
        Version.TryParse(Pad(candidate), out var a) && Version.TryParse(Pad(current), out var b)
            ? a > b
            : string.CompareOrdinal(candidate, current) > 0;

    private static string Pad(string version)
    {
        var core = version.Split('-', '+')[0];
        return core.Count(c => c == '.') == 0 ? core + ".0" : core;
    }
}

/// <summary>A plugin installed from the marketplace (its folder under the plugins folder).</summary>
/// <param name="PendingRemoval">Removed; gone after the next restart.</param>
/// <param name="PendingVersion">A newer version waiting for the next restart, or null.</param>
public sealed record InstalledPlugin(PluginPackageInfo Info, string Folder, bool PendingRemoval, string? PendingVersion);

/// <summary>
/// Installs and removes marketplace plugins under <see cref="Root"/> (<c>%AppData%\DevHub\plugins</c>).
/// A running DevHub has its plugins' DLLs open, so changes go into <c>.pending</c> first and
/// <see cref="ApplyPending"/> moves them into place at the next start, before plugins are found:
/// installing, updating and removing all take a restart, like switching plugins on and off.
/// A download must match its SHA-256 from the index; a package entry that would land outside the
/// plugin's folder ("zip slip") stops the install.
/// </summary>
public sealed class PluginInstaller(string root, HttpClient http)
{
    public string Root { get; } = root;
    private string Pending => Path.Combine(Root, ".pending");

    /// <summary>The plugins folder under DevHub's data folder (DEVHUB_DATA_DIR, else the roaming AppData folder).</summary>
    public static string DefaultRoot(string? dataDirectory) =>
        Path.Combine(dataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevHub", "plugins");

    /// <summary>Downloads, checks and unpacks a plugin for the next start. Returns why it can't, or null.</summary>
    public async Task<string?> InstallAsync(IndexEntry entry, CancellationToken ct = default)
    {
        if (!entry.IsCompatible)
            return L.T($"{entry.Name} is made for plugin API {entry.ApiVersion}; this DevHub has {PluginApi.Version}. Update DevHub (or wait for a new version of the plugin).");

        var download = Path.Combine(Path.GetTempPath(), $"devhub-plugin-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var stream = entry.Url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
                             ? File.OpenRead(new Uri(entry.Url).LocalPath)
                             : await http.GetStreamAsync(entry.Url, ct))
            await using (var file = File.Create(download))
                await stream.CopyToAsync(file, ct);
            return await InstallFileAsync(download, entry.Id, entry.Sha256, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return L.T($"Couldn't download {entry.Name}: {ex.Message}");
        }
        finally
        {
            File.Delete(download);
        }
    }

    /// <summary>Unpacks a downloaded package (its SHA-256 checked when given) for the next start.</summary>
    public async Task<string?> InstallFileAsync(string zipPath, string expectedId, string? sha256, CancellationToken ct = default)
    {
        if (sha256 is not null)
        {
            await using var check = File.OpenRead(zipPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(check, ct));
            if (!actual.Equals(sha256, StringComparison.OrdinalIgnoreCase))
                return L.T("The download doesn't match the index's SHA-256 (damaged, or not the published file); nothing was installed.");
        }

        var target = Path.Combine(Pending, expectedId);
        var targetRoot = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        try
        {
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            Directory.CreateDirectory(target);
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var item in zip.Entries)
            {
                var path = Path.GetFullPath(Path.Combine(target, item.FullName));
                if (!path.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T($"\"{item.FullName}\" would be written outside the plugin's folder."));
                if (item.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(path);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                item.ExtractToFile(path);
            }

            var info = ReadInfo(target);
            if (info is null)
                throw new InvalidDataException(L.T("The package has no readable plugin.json at its top."));
            if (!info.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L.T($"The package is the plugin \"{info.Id}\", not \"{expectedId}\"."));
            if (info.ApiVersion != PluginApi.Version)
                throw new InvalidDataException(L.T($"The package is made for plugin API {info.ApiVersion}; this DevHub has {PluginApi.Version}."));
            File.Delete(RemovalMarker(expectedId)); // installing again cancels a pending removal
            return null;
        }
        catch (InvalidDataException ex)
        {
            Directory.Delete(target, recursive: true);
            return ex.Message + L.T(" Nothing was installed.");
        }
    }

    /// <summary>Removes a marketplace plugin at the next start (a pending install of it is dropped now).</summary>
    public void Remove(string id)
    {
        Directory.CreateDirectory(Pending);
        if (Directory.Exists(Path.Combine(Pending, id)))
            Directory.Delete(Path.Combine(Pending, id), recursive: true);
        if (Directory.Exists(Path.Combine(Root, id)))
            File.WriteAllText(RemovalMarker(id), "");
    }

    /// <summary>The installed marketplace plugins, with what's waiting for the next start.</summary>
    public IReadOnlyList<InstalledPlugin> List()
    {
        if (!Directory.Exists(Root))
            return [];
        var list = new List<InstalledPlugin>();
        foreach (var folder in Directory.EnumerateDirectories(Root).Where(f => Path.GetFileName(f) != ".pending"))
        {
            if (ReadInfo(folder) is not { } info)
                continue;
            var pending = ReadInfo(Path.Combine(Pending, info.Id))?.Version;
            list.Add(new InstalledPlugin(info, folder, File.Exists(RemovalMarker(info.Id)), pending));
        }
        foreach (var folder in Directory.Exists(Pending) ? Directory.EnumerateDirectories(Pending) : [])
        {
            if (ReadInfo(folder) is { } info && list.All(p => !p.Info.Id.Equals(info.Id, StringComparison.OrdinalIgnoreCase)))
                list.Add(new InstalledPlugin(info with { Version = "" }, folder, false, info.Version)); // new: not installed yet
        }
        return list;
    }

    /// <summary>
    /// At startup, before plugins are found: pending removals and installs (new and updated plugins)
    /// are moved into place. Returns what couldn't be done (a file still in use).
    /// </summary>
    public static IReadOnlyList<string> ApplyPending(string root)
    {
        var problems = new List<string>();
        var pending = Path.Combine(root, ".pending");
        if (!Directory.Exists(pending))
            return problems;

        foreach (var marker in Directory.EnumerateFiles(pending, "*.remove"))
        {
            var id = Path.GetFileNameWithoutExtension(marker);
            try
            {
                if (Directory.Exists(Path.Combine(root, id)))
                    Directory.Delete(Path.Combine(root, id), recursive: true);
                File.Delete(marker);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Add($"Couldn't remove the plugin {id}: {ex.Message}");
            }
        }
        foreach (var folder in Directory.EnumerateDirectories(pending))
        {
            var id = Path.GetFileName(folder);
            var target = Path.Combine(root, id);
            try
            {
                if (Directory.Exists(target))
                    Directory.Delete(target, recursive: true);
                Directory.Move(folder, target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Add($"Couldn't install the plugin {id}: {ex.Message}");
            }
        }
        return problems;
    }

    /// <summary>The plugin.json in a folder, or null.</summary>
    public static PluginPackageInfo? ReadInfo(string folder)
    {
        var file = Path.Combine(folder, PluginPackageInfo.FileName);
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<PluginPackageInfo>(File.ReadAllText(file), Marketplace.Json) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private string RemovalMarker(string id) => Path.Combine(Pending, id + ".remove");
}
