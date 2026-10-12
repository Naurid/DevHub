namespace DevHub.ViewModels.Documents;

/// <summary>
/// Creates documents that can be open many times, each with its own id, e.g. one tab per
/// repository ("Repository:C:\P\DevHub"). The <c>DocumentManager</c> (the shell) asks the factory whose
/// <see cref="IdPrefix"/> matches an id, both when opening and when restoring tabs at startup.
/// Register in DI as <c>services.AddSingleton&lt;IDocumentFactory, XyzFactory&gt;()</c>.
/// </summary>
/// <remarks>
/// Single documents (Templates, New Project…) don't need a factory: they are DI singletons
/// registered as <see cref="DocumentViewModelBase"/>.
/// </remarks>
public interface IDocumentFactory
{
    /// <summary>Start of the ids this factory handles, including the separator, e.g. "Repository:".</summary>
    string IdPrefix { get; }

    /// <summary>
    /// Creates the document for <paramref name="id"/>, or null when it can't be created any more
    /// (e.g. the repository folder was deleted). The returned document's Id must equal <paramref name="id"/>.
    /// </summary>
    DocumentViewModelBase? Create(string id);
}
