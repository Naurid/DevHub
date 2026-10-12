using System.Text.Json.Nodes;
using DevHub.Sdk.Core.Settings;
using Xunit;

namespace DevHub.Sdk.Core.Tests;

public class JsonSettingsStoreTests
{
    /// <summary>A plugin's settings section, as a plugin would declare it.</summary>
    private sealed class SampleSection : ISettingsSection
    {
        public static string SectionId => "test.sample";

        public bool Enabled { get; set; } = true;
        public List<string> Folders { get; set; } = new();
    }

    [Fact]
    public void Load_returns_defaults_when_nothing_saved()
    {
        using var dir = new TempDir();
        var settings = new JsonSettingsStore(dir.Path).Load();

        Assert.Null(settings.ToolWindows);
        Assert.Empty(settings.Plugins);
    }

    [Fact]
    public void Load_returns_defaults_on_corrupt_file()
    {
        using var dir = new TempDir();
        dir.WriteSettings("{ this is not valid json ");

        var settings = new JsonSettingsStore(dir.Path).Load();

        Assert.Null(settings.Appearance);
        Assert.Empty(settings.Plugins);
    }

    // ----- Shell sections -----

    [Fact]
    public void Updates_check_on_startup_by_default_and_round_trips()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);
        Assert.Null(store.Load().Updates); // never changed: the app treats it as "check on startup"

        store.Update(s => s.Updates = new UpdateSettings { CheckOnStartup = false });

        Assert.False(new JsonSettingsStore(dir.Path).Load().Updates!.CheckOnStartup);
        Assert.True(new UpdateSettings().CheckOnStartup);
    }

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
    public void Editor_layout_round_trips_tab_order_and_active_tab()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        store.Update(s => s.Editor = new EditorLayout
        {
            OpenDocuments = ["Templates", "Generate", "Settings"],
            ActiveDocument = "Generate"
        });

        var editor = new JsonSettingsStore(dir.Path).Load().Editor;
        Assert.NotNull(editor);
        Assert.Equal(["Templates", "Generate", "Settings"], editor.OpenDocuments);
        Assert.Equal("Generate", editor.ActiveDocument);
    }

    [Fact]
    public void Appearance_round_trips()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        store.Update(s => s.Appearance = new AppearanceSettings
        {
            Theme = AppearanceSettings.ThemeSystem,
            Accent = "#8C5CDB",
            Compact = true
        });

        var appearance = new JsonSettingsStore(dir.Path).Load().Appearance;
        Assert.NotNull(appearance);
        Assert.Equal(AppearanceSettings.ThemeSystem, appearance.Theme);
        Assert.Equal("#8C5CDB", appearance.Accent);
        Assert.True(appearance.Compact);
    }

    // ----- Plugin sections -----

    [Fact]
    public void A_section_that_was_never_saved_has_its_defaults()
    {
        using var dir = new TempDir();

        var section = new JsonSettingsStore(dir.Path).LoadSection<SampleSection>();

        Assert.True(section.Enabled);
        Assert.Empty(section.Folders);
    }

    [Fact]
    public void A_section_round_trips_under_its_plugin_id()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        store.UpdateSection<SampleSection>(s =>
        {
            s.Enabled = false;
            s.Folders = [@"C:\Projects", @"D:\Work"];
        });

        var loaded = new JsonSettingsStore(dir.Path).LoadSection<SampleSection>();
        Assert.False(loaded.Enabled);
        Assert.Equal([@"C:\Projects", @"D:\Work"], loaded.Folders);
        Assert.Contains("\"test.sample\"", dir.ReadSettings());
    }

    [Fact]
    public void A_broken_section_gives_defaults_without_losing_the_rest()
    {
        using var dir = new TempDir();
        dir.WriteSettings("""
            { "appearance": { "theme": "light" },
              "plugins": { "test.sample": { "enabled": "not a bool" } } }
            """);
        var store = new JsonSettingsStore(dir.Path);

        Assert.True(store.LoadSection<SampleSection>().Enabled);
        Assert.Equal("light", store.Load().Appearance?.Theme);
    }

    [Fact]
    public void Sections_of_other_plugins_and_unknown_fields_survive_a_save()
    {
        // A disabled (or newer) plugin's settings, and fields a newer DevHub wrote, must not be
        // dropped when this version saves the file.
        using var dir = new TempDir();
        dir.WriteSettings("""
            { "futureField": { "x": 1 },
              "plugins": { "someone.else": { "keep": [1, 2, 3] } } }
            """);
        var store = new JsonSettingsStore(dir.Path);

        store.UpdateSection<SampleSection>(s => s.Enabled = false);
        store.Update(s => s.Editor = new EditorLayout { ActiveDocument = "Welcome" });

        var json = JsonNode.Parse(dir.ReadSettings())!;
        Assert.Equal(1, (int)json["futureField"]!["x"]!);
        Assert.Equal(3, json["plugins"]!["someone.else"]!["keep"]!.AsArray().Count);
        Assert.False((bool)json["plugins"]!["test.sample"]!["enabled"]!);
    }

    [Fact]
    public void Update_changes_one_section_and_keeps_the_others()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);
        store.UpdateSection<SampleSection>(s => s.Folders = ["a"]);

        store.Update(s => s.ToolWindows = new ToolWindowLayout { OpenLeft = "repositories" });

        Assert.Equal(["a"], store.LoadSection<SampleSection>().Folders);
        Assert.Equal("repositories", store.Load().ToolWindows?.OpenLeft);
    }

    [Fact]
    public void Two_features_updating_different_sections_do_not_overwrite_each_other()
    {
        // The bug Update prevents: feature A and B each change their own section. With
        // "load once at startup, save later", B's save would drop A's change.
        using var dir = new TempDir();
        var store = new JsonSettingsStore(dir.Path);

        store.Update(s => s.ToolWindows = new ToolWindowLayout { OpenLeft = "repositories" });
        store.UpdateSection<SampleSection>(s => s.Enabled = false);

        Assert.Equal("repositories", store.Load().ToolWindows?.OpenLeft);
        Assert.False(store.LoadSection<SampleSection>().Enabled);
    }

    // ----- Migration from the single-assembly layout (before the plugin split) -----

    [Fact]
    public void Old_git_and_projects_fields_move_into_their_plugin_sections()
    {
        using var dir = new TempDir();
        dir.WriteSettings("""
            {
              "languageTools": { "dotnet": { "toolId": "vscode", "customExe": null } },
              "repositoryFolders": [ "C:\\Projects" ],
              "editor": { "openDocuments": [ "Welcome" ], "activeDocument": "Welcome" },
              "git": { "changesAsTree": false, "diffSideBySide": true }
            }
            """);
        var store = new JsonSettingsStore(dir.Path);

        var settings = store.Load();

        Assert.Null(settings.Extra);
        Assert.Equal("Welcome", settings.Editor?.ActiveDocument);
        Assert.False((bool)settings.Plugins["devhub.git"]["changesAsTree"]!);
        Assert.True((bool)settings.Plugins["devhub.git"]["diffSideBySide"]!);
        var projects = settings.Plugins["devhub.projects"];
        Assert.Equal("vscode", (string)projects["languageTools"]!["dotnet"]!["toolId"]!);
        Assert.Equal(@"C:\Projects", (string)projects["repositoryFolders"]![0]!);

        // After a save the file has the new shape, and loading it again changes nothing.
        store.Save(settings);
        var json = JsonNode.Parse(dir.ReadSettings())!.AsObject();
        Assert.False(json.ContainsKey("git") || json.ContainsKey("languageTools") || json.ContainsKey("repositoryFolders"));
        Assert.Equal(@"C:\Projects", (string)store.Load().Plugins["devhub.projects"]["repositoryFolders"]![0]!);
    }

    [Fact]
    public void Migration_does_not_overwrite_a_section_that_already_exists()
    {
        using var dir = new TempDir();
        dir.WriteSettings("""
            { "git": { "changesAsTree": false },
              "plugins": { "devhub.git": { "changesAsTree": true } } }
            """);

        var settings = new JsonSettingsStore(dir.Path).Load();

        Assert.True((bool)settings.Plugins["devhub.git"]["changesAsTree"]!);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "devhub-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        private string SettingsFile => System.IO.Path.Combine(Path, "DevHub", "settings.json");

        public void WriteSettings(string json)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, json);
        }

        public string ReadSettings() => File.ReadAllText(SettingsFile);

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
