using StardewValley;

namespace AgrariaOS.Interactions;

internal static class InventoryStackActions
{
    internal sealed record MergeResult(int FullyMerged, int MovedUnits, bool TargetFull);

    internal static MergeResult Merge(IList<Item> inventory, Item target, IEnumerable<Item> sources,
        Action<Item> removeFullyMerged)
    {
        int fullyMerged = 0;
        int movedUnits = 0;
        foreach (Item source in sources.ToArray())
        {
            if (ReferenceEquals(source, target))
                continue;
            if (InventoryTransferBatch.IndexOf(inventory, source) < 0
                || InventoryTransferBatch.IndexOf(inventory, target) < 0)
                continue;
            if (source.Stack <= 0 || !target.canStackWith(source))
                continue;
            if (target.Stack >= target.maximumStackSize())
                break;
            // Match farmer.addItemToInventory: target grows, source keeps the leftover.
            int original = source.Stack;
            int leftover = target.addToStack(source);
            int moved = original - leftover;
            if (moved <= 0)
                continue;
            movedUnits += moved;
            if (leftover <= 0)
            {
                removeFullyMerged(source);
                fullyMerged++;
            }
            else
            {
                source.Stack = leftover;
            }
        }
        bool targetFull = target.Stack >= target.maximumStackSize();
        return new MergeResult(fullyMerged, movedUnits, targetFull);
    }

    internal static string? Split(IList<Item> inventory, int capacity, Item source, int amount)
    {
        if (InventoryTransferBatch.IndexOf(inventory, source) < 0)
            return "That stack is no longer in the backpack.";
        if (source.maximumStackSize() <= 1 || source.Stack <= 1)
            return "This item cannot be split.";
        if (amount < 1 || amount >= source.Stack)
            return $"Enter a number from 1 to {source.Stack - 1}.";

        int emptySlot = -1;
        for (int slot = 0; slot < Math.Min(capacity, inventory.Count); slot++)
        {
            if (inventory[slot] == null)
            {
                emptySlot = slot;
                break;
            }
        }
        if (emptySlot < 0)
            return "An empty backpack slot is required.";

        // Clone via vanilla so quality and per-instance data survive. Do not use the
        // normal add-item helper, which would merge this straight back into its source.
        Item split = source.getOne();
        split.Stack = amount;
        split.onDetachedFromParent();
        inventory[emptySlot] = split;
        source.Stack -= amount;
        return null;
    }
}
