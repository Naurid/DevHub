using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Sdk.Core.Settings;
using DevHub.Services;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// The editor area: which documents are open as tabs, which one is active, opening/closing/
/// cycling them, and saving the tab list. With no tabs, the Welcome view shows instead.
/// </summary>
/// <remarks>
/// Two kinds of documents:
/// <list type="bullet">
/// <item>Single documents (Templates, New Project…): DI singletons registered as
/// <see cref="DocumentViewModelBase"/>, received here as <see cref="Available"/>. One tab each;
/// closing keeps the instance (and its state) for next time.</item>
/// <item>Instance documents (one tab per repository): created on demand by an
/// <see cref="IDocumentFactory"/> from an id like "Repository:C:\P\DevHub", and disposed when
/// their tab closes.</item>
/// </list>
/// Opening a document that's already open (same Id) just activates its tab.
/// </remarks>
public partial class DocumentManager : ViewModelBase, IDocumentService
{
    private readonly ISettingsStore _store;
    private readonly IReadOnlyList<IDocumentFactory> _factories;
    private readonly IStatusService _status;

    /// <summary>Saved tabs of plugins that are switched off: not shown, but saved again (after the open ones).</summary>
    private readonly List<string> _keptForLater = [];

    /// <summary>Every single document that can be opened, in registration order.</summary>
    public IReadOnlyList<DocumentViewModelBase> Available { get; }

    /// <summary>Open tabs, left to right.</summary>
    public ObservableCollection<DocumentViewModelBase> Open { get; } = new();

    /// <summary>The selected tab (two-way bound to the tab strip), or null when none is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveTitle))]
    private DocumentViewModelBase? _active;

    IReadOnlyList<DocumentViewModelBase> IDocumentService.OpenDocuments => Open;

    /// <inheritdoc />
    public event EventHandler? OpenDocumentsChanged;

    /// <summary>True while at least one tab is open (false shows the Welcome view).</summary>
    public bool HasDocuments => Open.Count > 0;

    /// <summary>For the title bar breadcrumb: the active tab's title, or "Welcome".</summary>
    public string ActiveTitle => Active?.Title ?? "Welcome";

    public DocumentManager(IEnumerable<DocumentViewModelBase> documents, IEnumerable<IDocumentFactory> factories,
        ISettingsStore store, IStatusService status)
    {
        _store = store;
        _status = status;
        _factories = factories.ToList();
        Available = documents.ToList();

        foreach (var document in Available)
            AttachCommands(document);

        Open.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDocuments));
            OpenDocumentsChanged?.Invoke(this, EventArgs.Empty);
        };
        Restore(_store.Load().Editor);
    }

    /// <summary>Opens the document as a tab (or activates it if it's already open).</summary>
    public void Show(DocumentViewModelBase document)
    {
        if (!Open.Contains(document))
        {
            // New tabs go right after the active one, like JetBrains.
            var index = Active is null ? Open.Count : Open.IndexOf(Active) + 1;
            Open.Insert(index, document);
        }
        Active = document;
    }

    /// <summary>
    /// Opens the document with this id: activates its tab if it's open, otherwise opens the
    /// single document with that id or creates an instance document through its factory.
    /// Returns false when no document can be made for the id.
    /// </summary>
    public bool OpenById(string id)
    {
        if (Find(id) is not { } document)
            return false;

        Show(document);
        return true;
    }

    /// <summary>Opens the first available document of type <typeparamref name="T"/>.</summary>
    public void Show<T>() where T : DocumentViewModelBase =>
        Show(Available.OfType<T>().First());

    /// <summary>Closes a tab after asking it (<see cref="DocumentViewModelBase.ConfirmCloseAsync"/>: unsaved changes).</summary>
    public async Task CloseAsync(DocumentViewModelBase document)
    {
        if (Open.Contains(document) && await document.ConfirmCloseAsync())
            Close(document);
    }

    /// <summary>Closes a tab without asking; the neighbour to its left (or right) becomes active.</summary>
    public void Close(DocumentViewModelBase document)
    {
        var index = Open.IndexOf(document);
        if (index < 0)
            return;

        // Read this BEFORE removing: the tab strip's two-way SelectedItem binding sets Active
        // to null as soon as the selected item disappears from the list.
        var wasActive = Active == document;

        Open.RemoveAt(index);
        if (wasActive || Active is null)
            Active = Open.Count == 0 ? null : Open[Math.Max(0, index - 1)];

        // Instance documents (repository tabs) end with their tab: cancel work, stop watchers.
        if (!Available.Contains(document) && document is IDisposable disposable)
            disposable.Dispose();
    }

    [RelayCommand]
    private async Task CloseActiveAsync()
    {
        if (Active is not null)
            await CloseAsync(Active);
    }

    /// <summary>Activates the next (+1) or previous (-1) tab, wrapping around.</summary>
    public void Cycle(int direction)
    {
        if (Open.Count < 2 || Active is null)
            return;

        var index = (Open.IndexOf(Active) + direction + Open.Count) % Open.Count;
        Active = Open[index];
    }

    /// <summary>Saves the open tabs and the active one. Called when the main window closes.</summary>
    public void Save()
    {
        var layout = new EditorLayout
        {
            OpenDocuments = Open.Select(d => d.Id).Concat(_keptForLater).ToList(),
            ActiveDocument = Active?.Id,
        };
        _store.Update(s => s.Editor = layout);
    }

    private void Restore(EditorLayout? layout)
    {
        // First launch: nothing open, so the Welcome view greets the user.
        if (layout is null)
            return;

        var skipped = 0;
        foreach (var id in layout.OpenDocuments)
        {
            if (Find(id) is { } document)
            {
                if (!Open.Contains(document))
                    Open.Add(document);
            }
            else if (_factories.Any(f => id.StartsWith(f.IdPrefix, StringComparison.Ordinal)))
            {
                skipped++; // e.g. a repository that was moved or deleted since last time
            }
            else
            {
                // Nothing here knows this id: its plugin is switched off. Keep it in the saved
                // layout, so the tab comes back when the plugin is switched on again.
                _keptForLater.Add(id);
            }
        }

        if (skipped > 0)
        {
            _status.Show(skipped == 1
                ? "1 tab from last time couldn't be reopened (moved or deleted?)."
                : $"{skipped} tabs from last time couldn't be reopened (moved or deleted?).");
        }

        Active = Open.FirstOrDefault(d => d.Id == layout.ActiveDocument) ?? Open.FirstOrDefault();
    }

    /// <summary>An open tab with this id, else a single document, else a new instance from a factory.</summary>
    private DocumentViewModelBase? Find(string id)
    {
        if (Open.FirstOrDefault(d => d.Id == id) is { } open)
            return open;
        if (Available.FirstOrDefault(d => d.Id == id) is { } single)
            return single;

        var factory = _factories.FirstOrDefault(f => id.StartsWith(f.IdPrefix, StringComparison.Ordinal));
        if (factory?.Create(id) is not { } created)
            return null;

        AttachCommands(created);
        return created;
    }

    private void AttachCommands(DocumentViewModelBase document)
    {
        document.CloseCommand = new AsyncRelayCommand(() => CloseAsync(document));
        document.OpenDocument = OpenById;
    }

    // Keep each document's IsActive flag (tab highlight, close-button visibility) in sync.
    partial void OnActiveChanged(DocumentViewModelBase? oldValue, DocumentViewModelBase? newValue)
    {
        if (oldValue is not null)
            oldValue.IsActive = false;
        if (newValue is not null)
            newValue.IsActive = true;
    }
}
