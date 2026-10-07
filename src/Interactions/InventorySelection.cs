using StardewValley;

namespace AgrariaOS.Interactions;

// Item identity distinguishes separate stacks of the same item and survives row shifts.
internal sealed class InventorySelection
{
    private readonly HashSet<Item> selected = new(ReferenceEqualityComparer.Instance);
    private Item? anchor;

    internal int Count => selected.Count;
    internal bool Contains(Item item) => selected.Contains(item);
    internal Item[] InSlotOrder(IReadOnlyList<Item> items) => items.Where(selected.Contains).ToArray();

    internal Item[] ContextTargets(Item item, IReadOnlyList<Item> items)
    {
        Refresh(items);
        if (!Contains(item))
            SelectOnly(item);
        return InSlotOrder(items);
    }

    internal void Clear()
    {
        selected.Clear();
        anchor = null;
    }

    internal void SelectAll(IReadOnlyList<Item> items)
    {
        selected.Clear();
        foreach (Item item in items)
            selected.Add(item);
        anchor = items.Count > 0 ? items[0] : null;
    }

    internal void Refresh(IReadOnlyList<Item> items)
    {
        selected.RemoveWhere(item => !items.Any(current => ReferenceEquals(current, item)));
        if (anchor != null && !items.Any(item => ReferenceEquals(item, anchor)))
            anchor = null;
    }

    internal void Click(Item item, IReadOnlyList<Item> items, bool control, bool shift)
    {
        Refresh(items);
        int start = anchor == null ? -1 : IndexOf(items, anchor);
        if (shift && start >= 0)
        {
            if (!control)
                selected.Clear();
            int end = IndexOf(items, item);
            for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                selected.Add(items[i]);
            return;
        }

        anchor = item;
        if (control)
        {
            if (!selected.Remove(item))
                selected.Add(item);
        }
        else if (!selected.Contains(item))
            SelectOnly(item);
        // A plain press on a selected row keeps the group available for dragging.
        // The caller collapses it on release if the pointer never started a drag.
    }

    internal void SelectOnly(Item item)
    {
        selected.Clear();
        selected.Add(item);
        anchor = item;
    }

    private static int IndexOf(IReadOnlyList<Item> items, Item target)
    {
        for (int i = 0; i < items.Count; i++)
            if (ReferenceEquals(items[i], target))
                return i;
        return -1;
    }
}
