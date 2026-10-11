using System.ComponentModel;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// The editor area as plugins see it: which tab is in front, which tabs are open, and opening
/// one by id. The shell's DocumentManager implements it (plugins can't reference the shell).
/// Raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for <see cref="Active"/>.
/// </summary>
public interface IDocumentService : INotifyPropertyChanged
{
    /// <summary>The tab in front, or null when none is open. Setting it brings an open tab forward.</summary>
    DocumentViewModelBase? Active { get; set; }

    /// <summary>Open tabs, left to right.</summary>
    IReadOnlyList<DocumentViewModelBase> OpenDocuments { get; }

    /// <summary>Raised when a tab opens or closes.</summary>
    event EventHandler? OpenDocumentsChanged;

    /// <summary>
    /// Activates the tab with this id, or opens it (a single document, or a new one from the
    /// <see cref="IDocumentFactory"/> for the id's prefix). False when no document can be made.
    /// </summary>
    bool OpenById(string id);
}
