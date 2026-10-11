using DevHub.Projects.Core;
using DevHub.Services;
using DevHub.ViewModels.Documents;

namespace DevHub.ViewModels.Git;

/// <summary>The Git plugin's <see cref="IProjectOpener"/>: a project opens as its repository tab.</summary>
public sealed class GitProjectOpener(DocumentManager documents) : IProjectOpener
{
    public bool Open(GitRepository repository) =>
        documents.OpenById(RepositoryDocumentFactory.IdFor(repository.Path));
}
