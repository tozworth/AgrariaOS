using AgrariaOS.Configuration;
using AgrariaOS.Chat;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Notifications;
using AgrariaOS.Interactions;
using AgrariaOS.Integration;
using AgrariaOS.Mining;
using AgrariaOS.Movement;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace AgrariaOS.Desktop;

partial class WindowManager
{
    private void MergeStacks(InventoryWindow source, Item target, Item[] requested)
    {
        Farmer farmer = Game1.player;
        if (source.Chest == null)
        {
            if (!source.IsValid || !Context.IsPlayerFree)
                return;
            var items = farmer.Items;
            if (InventoryTransferBatch.IndexOf(items, target) < 0)
            {
                source.TransferStatus = "That stack is no longer available.";
                return;
            }
            try
            {
                var result = InventoryStackActions.Merge(items, target, requested, farmer.removeItemFromInventory);
                if (result.MovedUnits <= 0)
                {
                    source.TransferStatus = target.Stack >= target.maximumStackSize()
                        ? "That stack is full." : "Those stacks cannot be combined.";
                    return;
                }
                bool remaining = requested.Any(item => !ReferenceEquals(item, target)
                    && InventoryTransferBatch.IndexOf(items, item) >= 0);
                source.TransferStatus = result.FullyMerged > 0
                    ? $"Merged {result.FullyMerged} stack{(result.FullyMerged == 1 ? "" : "s")}."
                        + (remaining ? " Target stack is full." : "")
                    : $"Merged {result.MovedUnits} items.";
                source.CollapseSelection(target);
                Game1.playSound("Ship");
            }
            catch (Exception ex)
            {
                source.TransferStatus = "Merge failed.";
                onTransferFailed(ex);
            }
            return;
        }

        Chest chest = source.Chest;
        var mutex = chest.GetMutex();
        if (mutex.IsLocked())
        {
            source.TransferStatus = "Chest busy. Try again.";
            return;
        }
        GameLocation location = Game1.currentLocation;
        uint day = Game1.stats.DaysPlayed;
        Point viewport = drawnViewport;
        int version = ++transferVersion;
        pendingTransfer = (version, source, source);
        void CompletePending()
        {
            if (pendingTransfer?.Version == version)
                pendingTransfer = null;
        }
        source.TransferStatus = "Merging...";
        bool IsValid() => version == transferVersion && CanReceiveInput && Context.IsPlayerFree
            && Game1.player == farmer && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && viewport == drawnViewport && source.IsValid && !source.IsMinimized && windows.Contains(source)
            && ReferenceEquals(source.Chest, chest)
            && Utility.tileWithinRadiusOfPlayer((int)chest.TileLocation.X, (int)chest.TileLocation.Y, 1, farmer);
        try
        {
            mutex.RequestLock(() =>
            {
                try
                {
                    if (!IsValid())
                    {
                        if (version == transferVersion)
                            source.TransferStatus = "Transfer canceled.";
                        return;
                    }
                    var chestItems = chest.GetItemsForPlayer(farmer.UniqueMultiplayerID);
                    if (InventoryTransferBatch.IndexOf(chestItems, target) < 0)
                    {
                        source.TransferStatus = "That stack is no longer available.";
                        return;
                    }
                    void Remove(Item item) => chestItems[InventoryTransferBatch.IndexOf(chestItems, item)] = null!;
                    var result = InventoryStackActions.Merge(chestItems, target, requested, Remove);
                    chest.clearNulls();
                    if (result.MovedUnits <= 0)
                    {
                        source.TransferStatus = target.Stack >= target.maximumStackSize()
                            ? "That stack is full." : "Those stacks cannot be combined.";
                        return;
                    }
                    bool remaining = requested.Any(item => !ReferenceEquals(item, target)
                        && InventoryTransferBatch.IndexOf(chestItems, item) >= 0);
                    source.TransferStatus = result.FullyMerged > 0
                        ? $"Merged {result.FullyMerged} stack{(result.FullyMerged == 1 ? "" : "s")}."
                            + (remaining ? " Target stack is full." : "")
                        : $"Merged {result.MovedUnits} items.";
                    source.CollapseSelection(target);
                    Game1.playSound("Ship");
                }
                catch (Exception ex)
                {
                    source.TransferStatus = "Merge failed.";
                    onTransferFailed(ex);
                }
                finally
                {
                    mutex.ReleaseLock();
                    CompletePending();
                }
            }, () =>
            {
                CompletePending();
                if (version == transferVersion)
                    source.TransferStatus = "Chest busy. Try again.";
            });
        }
        catch (Exception ex)
        {
            CompletePending();
            source.TransferStatus = "Merge failed.";
            onTransferFailed(ex);
        }
    }

}
