using StardewValley;

namespace AgrariaOS.Interactions;

internal static class InventoryStackActions
{
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
