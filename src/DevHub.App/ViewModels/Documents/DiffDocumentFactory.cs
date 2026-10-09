using DevHub.Core.Git;
using DevHub.Services;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// Creates diff tabs from ids like "Diff:unstaged|C:\P\DevHub|src/App.cs" ("Open in New Tab"
/// in the diff pane, and restoring tabs at startup). "|" can't appear in a Windows path, so it
/// separates the parts.
/// </summary>
public sealed class DiffDocumentFactory(ExternalEditor editor, GitRunner? runner = null) : IDocumentFactory
{
    private const string Staged = "staged";
    private const string Unstaged = "unstaged";

    public string IdPrefix => DiffDocumentViewModel.IdPrefix;

    public static string IdFor(string repositoryPath, string filePath, bool staged) =>
        $"{DiffDocumentViewModel.IdPrefix}{(staged ? Staged : Unstaged)}|{repositoryPath}|{filePath}";

    public DocumentViewModelBase? Create(string id)
    {
        if (!id.StartsWith(IdPrefix, StringComparison.Ordinal))
            return null;

        var parts = id[IdPrefix.Length..].Split('|');
        if (parts.Length != 3 || parts[0] is not (Staged or Unstaged) || parts[2].Length == 0)
            return null;

        var repositoryPath = parts[1];
        // Repository moved or deleted since last time → skip (the DocumentManager tells the user).
        if (!Directory.Exists(repositoryPath))
            return null;

        var client = runner is null ? null : new GitRepositoryClient(runner, repositoryPath);
        return new DiffDocumentViewModel(repositoryPath, parts[2], parts[0] == Staged, client, editor);
    }
}
