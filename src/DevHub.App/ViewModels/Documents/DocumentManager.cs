using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHub.Core.Settings;

namespace DevHub.ViewModels.Documents;

/// <summary>
/// The editor area: which documents are open as tabs, which one is active, opening/closing/
/// cycling them, and saving the tab list. With no tabs, the Welcome view shows instead.
/// </summary>
/// <remarks>
/// Documents are registered in DI as <see cref="DocumentViewModelBase"/> (one registration
/// each, received here as an IEnumerable, like tool windows and action providers). Opening a
/// document that's already open just activates its tab.
/// </remarks>
public partial class DocumentManager : ViewModelBase
{
    private readonly ISettingsStore _store;

    /// <summary>Every document that can be opened, in registration order.</summary>
    public IReadOnlyList<DocumentViewModelBase> Available { get; }

    /// <summary>Open tabs, left to right.</summary>
    public ObservableCollection<DocumentViewModelBase> Open { get; } = new();

    /// <summary>The selected tab (two-way bound to the tab strip), or null when none is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveTitle))]
    private DocumentViewModelBase? _active;

    /// <summary>True while at least one tab is open (false shows the Welcome view).</summary>
    public bool HasDocuments => Open.Count > 0;

    /// <summary>For the title bar breadcrumb: the active tab's title, or "Welcome".</summary>
    public string ActiveTitle => Active?.Title ?? "Welcome";

    public DocumentManager(IEnumerable<DocumentViewModelBase> documents, ISettingsStore store)
    {
        _store = store;
        Available = documents.ToList();

        foreach (var document in Available)
        {
            var captured = document;
            document.CloseCommand = new RelayCommand(() => Close(captured));
        }

        Open.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDocuments));
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

    /// <summary>Opens the first available document of type <typeparamref name="T"/>.</summary>
    public void Show<T>() where T : DocumentViewModelBase =>
        Show(Available.OfType<T>().First());

    /// <summary>Closes a tab; the neighbour to its left (or right) becomes active.</summary>
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
    }

    [RelayCommand]
    private void CloseActive()
    {
        if (Active is not null)
            Close(Active);
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
            OpenDocuments = Open.Select(d => d.Id).ToList(),
            ActiveDocument = Active?.Id,
        };
        _store.Update(s => s.Editor = layout);
    }

    private void Restore(EditorLayout? layout)
    {
        // First launch: nothing open, so the Welcome view greets the user.
        if (layout is null)
            return;

        foreach (var id in layout.OpenDocuments)
        {
            if (Available.FirstOrDefault(d => d.Id == id) is { } document && !Open.Contains(document))
                Open.Add(document);
        }

        Active = Open.FirstOrDefault(d => d.Id == layout.ActiveDocument) ?? Open.FirstOrDefault();
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
