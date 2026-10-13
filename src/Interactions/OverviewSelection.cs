using AgrariaOS.Movement;

namespace AgrariaOS.Interactions;

// Stable row keys survive rescans and sorting; the window supplies visible row order.
internal sealed class OverviewSelection
{
    private readonly HashSet<string> selected = new(StringComparer.Ordinal);
    private string? anchor;

    internal int Count => selected.Count;
    internal bool Contains(string key) => selected.Contains(key);
    internal void Clear() { selected.Clear(); anchor = null; }
    internal void SelectOnly(string key) { Clear(); selected.Add(key); anchor = key; }

    internal void Refresh(IReadOnlyList<string> keys)
    {
        var visible = keys.ToHashSet(StringComparer.Ordinal);
        selected.RemoveWhere(key => !visible.Contains(key));
        if (anchor != null && !visible.Contains(anchor))
            anchor = null;
    }

    // false signals the shared queue/selection cap; keep the existing selection intact.
    internal bool Click(string key, IReadOnlyList<string> keys, bool control, bool shift, bool extendedQueue = false)
    {
        Refresh(keys);
        int start = anchor == null ? -1 : IndexOf(keys, anchor);
        int end = IndexOf(keys, key);
        if (end < 0)
            return false;
        if (shift && start >= 0)
        {
            var range = control ? new HashSet<string>(selected, StringComparer.Ordinal) : new(StringComparer.Ordinal);
            for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                range.Add(keys[i]);
            if (!extendedQueue && range.Count > ClickToMove.MaxQueued)
                return false;
            selected.Clear();
            selected.UnionWith(range);
            return true;
        }
        if (!control)
            SelectOnly(key);
        else if (!selected.Remove(key))
        {
            if (!extendedQueue && Count >= ClickToMove.MaxQueued)
                return false;
            selected.Add(key);
            anchor = key;
        }
        else
            anchor = key;
        return true;
    }

    private static int IndexOf(IReadOnlyList<string> keys, string key)
    {
        for (int i = 0; i < keys.Count; i++)
            if (keys[i] == key)
                return i;
        return -1;
    }
}
