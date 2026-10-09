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
