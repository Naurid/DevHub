using DevHub.Core.Projects;
using DevHub.Core.Settings;
using Xunit;

namespace DevHub.Core.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public void Load_returns_defaults_when_nothing_saved()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        var settings = store.Load();

        Assert.Empty(settings.LanguageTools);
    }

    [Fact]
    public void Save_then_load_round_trips_overrides()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        var settings = new AppSettings();
        settings.LanguageTools["dotnet"] = new LanguageToolChoice { ToolId = ToolIds.VsCode };
        settings.LanguageTools["go"] = new LanguageToolChoice
        {
            ToolId = ToolIds.Custom,
            CustomExe = @"C:\e.exe"
        };
        store.Save(settings);

        var loaded = new JsonSettingsStore(dir.Path).Load();

        Assert.Equal(ToolIds.VsCode, loaded.LanguageTools["dotnet"].ToolId);
        Assert.Equal(@"C:\e.exe", loaded.LanguageTools["go"].CustomExe);
    }

    [Fact]
    public void Load_returns_defaults_on_corrupt_file()
    {
        using var dir = new TempDir();
        var file = Path.Combine(dir.Path, "DevHub", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{ this is not valid json ");

        var settings = new JsonSettingsStore(dir.Path).Load();

        Assert.Empty(settings.LanguageTools);
    }

    // ----- Tool-window layout -----

    [Fact]
    public void Tool_window_layout_is_null_until_saved()
    {
        using var dir = new TempDir();

        Assert.Null(new JsonSettingsStore(dir.Path).Load().ToolWindows);
    }

    [Fact]
    public void Tool_window_layout_round_trips_including_closed_slots()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        store.Save(new AppSettings
        {
            ToolWindows = new ToolWindowLayout
            {
                OpenLeft = "repositories",
                OpenRight = null,
                OpenBottom = "log",
                LeftWidth = 333,
                BottomHeight = 150
            }
        });

        var layout = new JsonSettingsStore(dir.Path).Load().ToolWindows;

        Assert.NotNull(layout);
        Assert.Equal("repositories", layout.OpenLeft);
        Assert.Null(layout.OpenRight);
        Assert.Equal("log", layout.OpenBottom);
        Assert.Equal(333, layout.LeftWidth);
        Assert.Equal(ToolWindowLayout.DefaultSideWidth, layout.RightWidth);
        Assert.Equal(150, layout.BottomHeight);
    }

    [Fact]
    public void Repository_folders_round_trip_in_order()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        store.Update(s => s.RepositoryFolders = [@"C:\Projects", @"D:\Work"]);

        Assert.Equal([@"C:\Projects", @"D:\Work"], new JsonSettingsStore(dir.Path).Load().RepositoryFolders);
    }

    // ----- Update (load → change → save) -----

    [Fact]
    public void Update_changes_one_section_and_keeps_the_others()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);
        var settings = new AppSettings();
        settings.LanguageTools["dotnet"] = new LanguageToolChoice { ToolId = ToolIds.VsCode };
        store.Save(settings);

        store.Update(s => s.ToolWindows = new ToolWindowLayout { OpenLeft = "repositories" });

        var loaded = store.Load();
        Assert.Equal(ToolIds.VsCode, loaded.LanguageTools["dotnet"].ToolId);
        Assert.Equal("repositories", loaded.ToolWindows?.OpenLeft);
    }

    [Fact]
    public void Two_features_updating_different_sections_do_not_overwrite_each_other()
    {
        // The bug Update prevents: feature A and B each change their own section. With
        // "load once at startup, save later", B's save would drop A's change.
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        store.Update(s => s.ToolWindows = new ToolWindowLayout { OpenLeft = "repositories" });
        store.Update(s => s.LanguageTools["go"] = new LanguageToolChoice { ToolId = ToolIds.VsCode });

        var loaded = store.Load();
        Assert.Equal("repositories", loaded.ToolWindows?.OpenLeft);
        Assert.Equal(ToolIds.VsCode, loaded.LanguageTools["go"].ToolId);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "devhub-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
