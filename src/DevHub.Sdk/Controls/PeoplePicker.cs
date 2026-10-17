using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using DevHub.Sdk.Core.People;
using DevHub.Sdk.Core.Localization;

namespace DevHub.Controls;

/// <summary>
/// Picks people (an issue's assignee, reviewers, watchers) from <see cref="People"/>: type to search (fuzzy, by name, id or
/// email), ↓ / ↑ and Enter (or a click) to pick, the picked ones show as chips with a remove button, Backspace in the empty
/// box removes the last one, Escape closes the list. <see cref="AllowMultiple"/> false: picking replaces the one picked.
/// <code>&lt;c:PeoplePicker People="{Binding Team}" SelectedPeople="{Binding Assignees}" /&gt;</code>
/// <see cref="SelectedPeople"/> is the ViewModel's collection (an ObservableCollection), changed in place.
/// Look: Themes/Components/PeoplePicker.axaml.
/// </summary>
[TemplatePart("PART_Query", typeof(TextBox))]
[TemplatePart("PART_Popup", typeof(Popup))]
[TemplatePart("PART_Matches", typeof(ListBox))]
public class PeoplePicker : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<Person>?> PeopleProperty =
        AvaloniaProperty.Register<PeoplePicker, IEnumerable<Person>?>(nameof(People));

    public static readonly StyledProperty<IList<Person>?> SelectedPeopleProperty =
        AvaloniaProperty.Register<PeoplePicker, IList<Person>?>(nameof(SelectedPeople));

    public static readonly StyledProperty<bool> AllowMultipleProperty =
        AvaloniaProperty.Register<PeoplePicker, bool>(nameof(AllowMultiple), true);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<PeoplePicker, string>(nameof(Placeholder), L.T("Add someone…"));

    public static readonly DirectProperty<PeoplePicker, string?> QueryProperty =
        AvaloniaProperty.RegisterDirect<PeoplePicker, string?>(nameof(Query), p => p.Query, (p, v) => p.Query = v);

    public static readonly DirectProperty<PeoplePicker, bool> IsListOpenProperty =
        AvaloniaProperty.RegisterDirect<PeoplePicker, bool>(nameof(IsListOpen), p => p.IsListOpen, (p, v) => p.IsListOpen = v);

    public IEnumerable<Person>? People { get => GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }
    public IList<Person>? SelectedPeople { get => GetValue(SelectedPeopleProperty); set => SetValue(SelectedPeopleProperty, value); }
    public bool AllowMultiple { get => GetValue(AllowMultipleProperty); set => SetValue(AllowMultipleProperty, value); }
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    private string? _query;

    /// <summary>What's typed in the box.</summary>
    public string? Query
    {
        get => _query;
        set
        {
            if (SetAndRaise(QueryProperty, ref _query, value))
                UpdateMatches(open: true);
        }
    }

    private bool _isListOpen;

    public bool IsListOpen { get => _isListOpen; set => SetAndRaise(IsListOpenProperty, ref _isListOpen, value); }

    /// <summary>The people matching <see cref="Query"/>, best first (shown in the list).</summary>
    public ObservableCollection<Person> Matches { get; } = [];

    /// <summary>Removes a picked person (the chips' × buttons).</summary>
    public ICommand RemoveCommand { get; }

    /// <summary>Picks a person (a click in the list).</summary>
    public ICommand PickCommand { get; }

    private TextBox? _box;
    private ListBox? _list;

    public PeoplePicker()
    {
        RemoveCommand = new RelayCommand<Person>(person =>
        {
            if (person is not null)
                SelectedPeople?.Remove(person);
            UpdateMatches(open: false);
        });
        PickCommand = new RelayCommand<Person>(Pick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_box is not null)
        {
            _box.KeyDown -= OnBoxKeyDown;
            _box.GotFocus -= OnBoxFocused;
        }
        base.OnApplyTemplate(e);
        _box = e.NameScope.Find<TextBox>("PART_Query");
        _list = e.NameScope.Find<ListBox>("PART_Matches");
        if (_box is not null)
        {
            // Tunnel: Enter / ↓ / Escape reach the picker before the text box uses them.
            _box.AddHandler(KeyDownEvent, OnBoxKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            _box.GotFocus += OnBoxFocused;
        }
    }

    private void OnBoxFocused(object? sender, FocusChangedEventArgs e) => UpdateMatches(open: true);

    private void UpdateMatches(bool open)
    {
        Matches.Clear();
        foreach (var person in PeopleSearch.Find(People ?? [], Query, AllowMultiple ? SelectedPeople : null, limit: 8))
            Matches.Add(person);
        if (_list is not null)
            _list.SelectedIndex = Matches.Count > 0 ? 0 : -1;
        IsListOpen = open && Matches.Count > 0 && (_box?.IsFocused ?? false);
    }

    private void Pick(Person? person)
    {
        if (person is null || SelectedPeople is not { } selected)
            return;
        if (!AllowMultiple)
            selected.Clear();
        if (!selected.Any(p => p.Id.Equals(person.Id, StringComparison.OrdinalIgnoreCase)))
            selected.Add(person);
        Query = "";
        IsListOpen = false;
        _box?.Focus();
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when Matches.Count > 0:
                IsListOpen = true;
                if (_list is not null)
                    _list.SelectedIndex = Math.Min(Matches.Count - 1, _list.SelectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Up when Matches.Count > 0 && _list is not null:
                _list.SelectedIndex = Math.Max(0, _list.SelectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Enter when IsListOpen && _list?.SelectedItem is Person person:
                Pick(person);
                e.Handled = true;
                break;
            case Key.Escape when IsListOpen:
                IsListOpen = false;
                e.Handled = true;
                break;
            case Key.Back when string.IsNullOrEmpty(Query) && SelectedPeople is { Count: > 0 } selected:
                selected.RemoveAt(selected.Count - 1);
                UpdateMatches(open: true);
                e.Handled = true;
                break;
        }
    }
}
