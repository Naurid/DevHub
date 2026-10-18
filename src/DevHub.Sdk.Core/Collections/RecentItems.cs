using System.Collections.ObjectModel;

namespace DevHub.Sdk.Core.Collections;

/// <summary>
/// The latest items, newest first, at most <see cref="Capacity"/> of them (the status bar's message history). An
/// observable collection, so a list bound to it follows; change it on the UI thread.
/// </summary>
public sealed class RecentItems<T>(int capacity) : ObservableCollection<T>
{
    private int _capacity = Math.Max(1, capacity);

    /// <summary>How many are kept; lowering it drops the oldest straight away.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = Math.Max(1, value);
            Trim();
        }
    }

    /// <summary>Puts <paramref name="item"/> first and drops what no longer fits.</summary>
    public void Push(T item)
    {
        Insert(0, item);
        Trim();
    }

    private void Trim()
    {
        while (Count > _capacity)
            RemoveAt(Count - 1);
    }
}
