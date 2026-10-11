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

    /// <param name="showCancel">False: a message to acknowledge (e.g. "Push rejected" with git's output as the items).</param>
    public ConfirmDialogViewModel(string title, string message, string okText,
        IReadOnlyList<string>? items = null, bool destructive = true, bool showCancel = true)
    {
        Title = title;
        Message = message;
        OkText = okText;
        IsDestructive = destructive;
        ShowCancel = showCancel;

        items ??= [];
        Items = items.Take(MaxListedItems).ToList();
        MoreText = items.Count > MaxListedItems ? $"and {items.Count - MaxListedItems} more" : null;
    }

    public override string Title { get; }
    public override string OkText { get; }
    public override bool IsDestructive { get; }
    public override bool ShowCancel { get; }

    // Narrow, and only as tall as the message and list need.
    public override bool FitsContent => true;
    public override double Width => 440;
    public override double MinWidth => 360;
    public override double MinHeight => 0;

    /// <summary>Warning for destructive confirmations and messages with Cancel hidden (errors), info otherwise.</summary>
    public string IconKind => IsDestructive || !ShowCancel ? Controls.IconKind.Warning : Controls.IconKind.Info;

    public string Message { get; }
    public IReadOnlyList<string> Items { get; }
    public bool HasItems => Items.Count > 0;
    public string? MoreText { get; }
}
