using System.Text.Json;
using DevHub.Sdk.Core.Plugins.Marketplace;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public sealed class PluginFoldersTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("devhub-folders-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static PluginFolder F(string id, string version, bool bundled) =>
        new($@"C:\{(bundled ? "app" : "data")}\plugins\{id}", new PluginPackageInfo { Id = id, Name = id, Version = version, ApiVersion = PluginApi.Version }, bundled);

    private static string[] Chosen(PluginFolderChoice choice) =>
        choice.Chosen.Select(f => $"{f.Info.Id} {f.Info.Version} {(f.Bundled ? "bundled" : "installed")}").ToArray();

    [Fact]
    public void An_installed_update_wins_over_the_bundled_copy()
    {
        var choice = PluginFolders.Choose([F("devhub.git", "1.2.0", true)], [F("devhub.git", "1.3.0", false)]);
        Assert.Equal(["devhub.git 1.3.0 installed"], Chosen(choice));
        Assert.Empty(choice.Outdated);
    }

    [Fact]
    public void A_bundled_copy_at_least_as_new_wins_and_the_installed_one_is_outdated()
    {
        // DevHub was updated and now bundles the version the user had installed (or a newer one).
        foreach (var bundledVersion in new[] { "1.3.0", "1.10.0" })
        {
            var installed = F("devhub.git", "1.3.0", false);
            var choice = PluginFolders.Choose([F("devhub.git", bundledVersion, true)], [installed]);
            Assert.Equal([$"devhub.git {bundledVersion} bundled"], Chosen(choice));
            Assert.Equal([installed], choice.Outdated);
        }
    }

    [Fact]
    public void Plugins_in_one_place_only_are_all_chosen_bundled_first()
    {
        var choice = PluginFolders.Choose(
            [F("devhub.projects", "1.0.0", true), F("devhub.git", "1.0.0", true)],
            [F("naurid.board", "0.4.0", false), F("DEVHUB.GIT", "1.1.0", false)]);
        Assert.Equal(["devhub.projects 1.0.0 bundled", "DEVHUB.GIT 1.1.0 installed", "naurid.board 0.4.0 installed"], Chosen(choice));
    }

    [Fact]
    public void Versions_compare_as_numbers()
    {
        var choice = PluginFolders.Choose([F("a", "1.9.0", true)], [F("a", "1.10.0", false)]);
        Assert.Equal(["a 1.10.0 installed"], Chosen(choice));
    }

    [Fact]
    public void Scan_finds_folders_with_a_plugin_json_and_skips_pending_and_unreadable_ones()
    {
        void Plugin(string folder, string json)
        {
            Directory.CreateDirectory(Path.Combine(_dir, folder));
            File.WriteAllText(Path.Combine(_dir, folder, PluginPackageInfo.FileName), json);
        }
        Plugin("b-plugin", JsonSerializer.Serialize(new { id = "b", name = "B", version = "1.0.0", apiVersion = 1 }));
        Plugin("a-plugin", JsonSerializer.Serialize(new { id = "a", name = "A", version = "2.0.0", apiVersion = 1 }));
        Plugin(".pending", JsonSerializer.Serialize(new { id = "p", name = "P", version = "1.0.0", apiVersion = 1 }));
        Plugin("broken", "{ not json");
        Directory.CreateDirectory(Path.Combine(_dir, "empty"));

        var found = PluginFolders.Scan(_dir, bundled: true);
        Assert.Equal(["a", "b"], found.Select(f => f.Info.Id));
        Assert.All(found, f => Assert.True(f.Bundled));
        Assert.Empty(PluginFolders.Scan(Path.Combine(_dir, "missing"), bundled: false));
    }
}
