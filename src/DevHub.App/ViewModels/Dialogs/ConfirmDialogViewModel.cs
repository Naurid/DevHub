namespace DevHub.ViewModels.Dialogs;

/// <summary>
/// "Are you sure?" for destructive operations (the Git rules: say exactly what will be lost).
/// A message, an optional list of affected items (first few, then "and N more"), and an OK
/// button labelled with the action ("Discard"), red when destructive.
/// </summary>
public sealed class ConfirmDialogViewModel : DialogViewModelBase
{
    /// <summary>How many items the list shows before "and N more".</summary>
    public const int MaxListedItems = 8;

    public ConfirmDialogViewModel(string title, string message, string okText,
        IReadOnlyList<string>? items = null, bool destructive = true)
    {
        Title = title;
        Message = message;
        OkText = okText;
        IsDestructive = destructive;

        items ??= [];
        Items = items.Take(MaxListedItems).ToList();
        MoreText = items.Count > MaxListedItems ? $"and {items.Count - MaxListedItems} more" : null;
    }

    public override string Title { get; }
    public override string OkText { get; }
    public override bool IsDestructive { get; }

    public override double Width => 520;
    public override double Height => 300;
    public override double MinWidth => 400;
    public override double MinHeight => 220;

    public string Message { get; }
    public IReadOnlyList<string> Items { get; }
    public bool HasItems => Items.Count > 0;
    public string? MoreText { get; }
}
