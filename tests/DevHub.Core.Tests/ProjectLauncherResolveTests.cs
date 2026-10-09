using DevHub.Core.Projects;
using DevHub.Core.Repositories;
using DevHub.Core.Settings;
using Xunit;

namespace DevHub.Core.Tests;

public class ProjectLauncherResolveTests
{
    [Fact]
    public void Rider_runs_rider_on_the_folder()
    {
        using var repo = new RepoDir(KnownLanguages.DotNet);
        var ctx = Context(riderPath: @"C:\rider64.exe");

        var spec = ProjectLauncher.Resolve(repo.Repo, Choice(ToolIds.Rider), ctx);

        Assert.Equal(@"C:\rider64.exe", spec.FileName);
        Assert.Equal(new[] { repo.Path }, spec.Arguments);
        Assert.True(spec.UseShellExecute); // launched detached so the IDE's logs don't leak into ours
    }

    [Fact]
    public void VsCode_runs_code_on_the_folder()
    {
        using var repo = new RepoDir(KnownLanguages.Python);
        var ctx = Context(vsCodePath: @"C:\Code.exe");

        var spec = ProjectLauncher.Resolve(repo.Repo, Choice(ToolIds.VsCode), ctx);

        Assert.Equal(@"C:\Code.exe", spec.FileName);
        Assert.Equal(new[] { repo.Path }, spec.Arguments);
    }

    [Fact]
    public void OsDefault_shell_opens_the_sln_when_present()
    {
        using var repo = new RepoDir(KnownLanguages.DotNet);
        var sln = repo.Touch("MyApp.sln");

        var spec = ProjectLauncher.Resolve(repo.Repo, Choice(ToolIds.OsDefault), Context());

        Assert.Equal(sln, spec.FileName);
        Assert.Empty(spec.Arguments);
        Assert.True(spec.UseShellExecute);
    }

    [Fact]
    public void OsDefault_shell_opens_the_folder_when_no_project_file()
    {
        using var repo = new RepoDir(KnownLanguages.Python);

        var spec = ProjectLauncher.Resolve(repo.Repo, Choice(ToolIds.OsDefault), Context());

        Assert.Equal(repo.Path, spec.FileName);
        Assert.True(spec.UseShellExecute);
    }

    [Fact]
    public void Unity_launches_the_pinned_editor_when_installed()
    {
        using var repo = new RepoDir(KnownLanguages.Unity);
        repo.WriteUnityVersion("6000.3.13f1");
        using var editors = new EditorsRoot();
        var unityExe = editors.InstallEditor("6000.3.13f1");

        var spec = ProjectLauncher.Resolve(repo.Repo, Choice(ToolIds.UnityHub), Context(unityEditorsRoot: editors.Root));

        Assert.Equal(unityExe, spec.FileName);
        Assert.Equal(new[] { "-projectPath", repo.Path }, spec.Arguments);
    }

    [Fact]
    public void Unity_falls_back_to_hub_when_editor_missing()
    {
        using var repo = new RepoDir(KnownLanguages.Unity);
        repo.WriteUnityVersion("1234.5.6f7"); // not installed
        using var editors = new EditorsRoot();

        var ctx = Context(unityHubPath: @"C:\Unity Hub.exe", unityEditorsRoot: editors.Root);
        var spec = ProjectLauncher.Resolve(repo.Repo, Choice(ToolIds.UnityHub), ctx);

        Assert.Equal(@"C:\Unity Hub.exe", spec.FileName);
        Assert.Equal(new[] { "--", "--projectPath", repo.Path }, spec.Arguments);
    }

    [Fact]
    public void Custom_runs_the_exe_on_the_folder()
    {
        using var repo = new RepoDir(KnownLanguages.Go);

        var choice = new LanguageToolChoice
        {
            ToolId = ToolIds.Custom,
            CustomExe = @"C:\tools\editor.exe"
        };

        var spec = ProjectLauncher.Resolve(repo.Repo, choice, Context());

        Assert.Equal(@"C:\tools\editor.exe", spec.FileName);
        Assert.Equal(new[] { repo.Path }, spec.Arguments);
        Assert.True(spec.UseShellExecute);
    }

    [Fact]
    public void Custom_without_exe_falls_back_to_os_default()
    {
        using var repo = new RepoDir(KnownLanguages.Go);

        var spec = ProjectLauncher.Resolve(repo.Repo, Choice(ToolIds.Custom), Context());

        // No custom exe set → safe fallback (shell-open the folder, since no project file).
        Assert.Equal(repo.Path, spec.FileName);
        Assert.True(spec.UseShellExecute);
    }

    private static LanguageToolChoice Choice(string toolId) => new() { ToolId = toolId };

    private static ToolContext Context(
        string? riderPath = null, string? vsCodePath = null, string? devenvPath = null,
        string? unityHubPath = null, string? unityEditorsRoot = null) =>
        new(riderPath, vsCodePath, devenvPath, unityHubPath, unityEditorsRoot ?? @"C:\none");

    /// <summary>A temp repo folder with a chosen language.</summary>
    private sealed class RepoDir : IDisposable
    {
        public string Path { get; }
        public GitRepository Repo { get; }

        public RepoDir(ProjectLanguage language)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "devhub-launch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Repo = new GitRepository(System.IO.Path.GetFileName(Path), Path, language);
        }

        public string Touch(string name)
        {
            var full = System.IO.Path.Combine(Path, name);
            File.WriteAllText(full, "");
            return full;
        }

        public void WriteUnityVersion(string version)
        {
            var settings = System.IO.Path.Combine(Path, "ProjectSettings");
            Directory.CreateDirectory(settings);
            File.WriteAllText(System.IO.Path.Combine(settings, "ProjectVersion.txt"),
                $"m_EditorVersion: {version}\n");
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }

    /// <summary>A temp Unity Hub editors-root into which we "install" editor versions.</summary>
    private sealed class EditorsRoot : IDisposable
    {
        public string Root { get; }

        public EditorsRoot()
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "devhub-editors-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string InstallEditor(string version)
        {
            var dir = System.IO.Path.Combine(Root, version, "Editor");
            Directory.CreateDirectory(dir);
            var exe = System.IO.Path.Combine(dir, "Unity.exe");
            File.WriteAllText(exe, "");
            return exe;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { }
        }
    }
}
