using StardewValley;

namespace AgrariaOS.Interactions;

internal static class InventoryTransferBatch
{
    // The caller owns the chest mutex. Native insertion returns the unaccepted remainder.
    // Detach before insertion so an item never belongs to two net inventories at once.
    internal static int Move(IList<Item> source, IList<Item> destination, IEnumerable<Item> requested,
        Func<Item, bool> accepts, Func<Item, Item?> insert, Action<Item> remove, Action<Item> restored)
    {
        if (ReferenceEquals(source, destination))
            return 0;
        int moved = 0;
        var visited = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        foreach (Item item in requested)
        {
            int slot = IndexOf(source, item);
            if (!visited.Add(item) || slot < 0 || item.Stack <= 0 || !accepts(item))
                continue;
            int originalStack = item.Stack;
            Item? remainder = item;
            remove(item);
            try
            {
                remainder = insert(item);
                if (remainder == null || remainder.Stack < originalStack)
                    moved++;
            }
            finally
            {
                // Keep full/partial remainders in their original slot. Also restore a
                // detached stack if insertion fails before accepting it.
                if (remainder != null && remainder.Stack > 0 && IndexOf(destination, remainder) < 0)
                {
                    source[slot] = remainder;
                    restored(remainder);
                }
            }
        }
        return moved;
    }

    internal static int IndexOf(IList<Item> items, Item target)
    {
        for (int i = 0; i < items.Count; i++)
            if (ReferenceEquals(items[i], target))
                return i;
        return -1;
    }
}
