using DevHub.Projects.Core;

namespace DevHub.Services;

/// <summary>
/// Soft hook of the Projects plugin: a plugin that can open a project inside DevHub (Git opens a
/// repository tab) registers one. Projects uses the first one for double-click / Enter / "Open in
/// DevHub"; with none registered it opens the project in its external tool (Open With) instead.
/// Projects never names the plugin that implements it, so it works with or without Git.
/// </summary>
public interface IProjectOpener
{
    /// <summary>Opens the project; false when it couldn't (e.g. the folder is gone).</summary>
    bool Open(GitRepository repository);
}
