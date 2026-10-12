namespace DevHub.Sdk.Core.Collections;

/// <summary>
/// Moving an item within a list or from one list to another, the way drag and drop means it (a board's cards between
/// columns, columns along the board): <c>toIndex</c> is the drop position counted in the target list as it looks
/// <i>before</i> the move (dropping "before the 3rd card" is index 2), so a ViewModel can pass straight through what
/// the Sdk's ItemsDragDrop reports.
/// </summary>
public static class ListMoves
{
    /// <summary>Moves <paramref name="item"/> from <paramref name="from"/> to <paramref name="to"/> at <paramref name="toIndex"/>
    /// (clamped to the list). Returns the index it ends up at, or -1 when it isn't in <paramref name="from"/>.</summary>
    public static int Move<T>(IList<T> from, T item, IList<T> to, int toIndex)
    {
        var fromIndex = from.IndexOf(item);
        if (fromIndex < 0)
            return -1;
        if (ReferenceEquals(from, to))
        {
            // Removing it first shifts everything after it one place up.
            var target = Math.Clamp(toIndex > fromIndex ? toIndex - 1 : toIndex, 0, from.Count - 1);
            if (target == fromIndex)
                return fromIndex;
            from.RemoveAt(fromIndex);
            from.Insert(target, item);
            return target;
        }
        from.RemoveAt(fromIndex);
        var index = Math.Clamp(toIndex, 0, to.Count);
        to.Insert(index, item);
        return index;
    }

    /// <summary>
    /// Where an item would land, as an index into its list after the move (what <see cref="Move{T}"/> returns), without
    /// moving it; for "no change" checks (dropping a card right where it is).
    /// </summary>
    public static bool IsNoOp<T>(IList<T> from, T item, IList<T> to, int toIndex)
    {
        var fromIndex = from.IndexOf(item);
        return fromIndex >= 0 && ReferenceEquals(from, to) && (toIndex == fromIndex || toIndex == fromIndex + 1);
    }
}
