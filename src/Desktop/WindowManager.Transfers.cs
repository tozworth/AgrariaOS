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
    private void TransferItems(InventoryWindow source, InventoryWindow target, Item[] requested)
    {
        Chest chest = (source.Chest ?? target.Chest)!;
        var mutex = chest.GetMutex();
        void SetStatus(string message) => source.TransferStatus = target.TransferStatus = message;
        // Never borrow/release a lock already owned by a vanilla menu or another operation.
        if (mutex.IsLocked())
        {
            SetStatus("Chest busy. Try again.");
            return;
        }
        Farmer farmer = Game1.player;
        GameLocation location = Game1.currentLocation;
        uint day = Game1.stats.DaysPlayed;
        Chest? sourceChest = source.Chest;
        Chest? targetChest = target.Chest;
        Point viewport = drawnViewport;
        int version = ++transferVersion;
        pendingTransfer = (version, source, target);
        void CompletePending()
        {
            if (pendingTransfer?.Version == version)
                pendingTransfer = null;
        }
        SetStatus("Transferring...");
        bool IsValid() => version == transferVersion && CanReceiveInput && Context.IsPlayerFree
            && Game1.player == farmer && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && viewport == drawnViewport && source.IsValid && target.IsValid
            && !source.IsMinimized && !target.IsMinimized && windows.Contains(source) && windows.Contains(target)
            && ReferenceEquals(source.Chest, sourceChest) && ReferenceEquals(target.Chest, targetChest)
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
                            SetStatus("Transfer canceled.");
                        return;
                    }
                    var sourceItems = sourceChest?.GetItemsForPlayer(farmer.UniqueMultiplayerID) ?? farmer.Items;
                    var targetItems = targetChest?.GetItemsForPlayer(farmer.UniqueMultiplayerID) ?? farmer.Items;
                    bool Accepts(Item item) => targetChest?.SpecialChestType switch
                    {
                        Chest.SpecialChestTypes.MiniShippingBin => Utility.highlightShippableObjects(item),
                        Chest.SpecialChestTypes.Enricher => StardewValley.Object.HighlightFertilizers(item),
                        _ => true
                    };
                    Item? Insert(Item item) => targetChest != null ? targetChest.addItem(item) : farmer.addItemToInventory(item);
                    void Remove(Item item)
                    {
                        if (sourceChest == null)
                            farmer.removeItemFromInventory(item);
                        else
                            sourceItems[InventoryTransferBatch.IndexOf(sourceItems, item)] = null!;
                    }
                    void Restored(Item item)
                    {
                        if (sourceChest == null && ReferenceEquals(farmer.CurrentItem, item))
                            item.actionWhenBeingHeld(farmer);
                    }
                    int moved = InventoryTransferBatch.Move(sourceItems, targetItems, requested, Accepts, Insert, Remove, Restored);
                    sourceChest?.clearNulls();
                    bool remaining = requested.Any(item => InventoryTransferBatch.IndexOf(sourceItems, item) >= 0);
                    SetStatus(remaining ? "Some stacks stayed: full or ineligible." : $"Moved {moved} stack{(moved == 1 ? "" : "s")}.");
                    if (moved > 0)
                        Game1.playSound("Ship");
                }
                catch (Exception ex)
                {
                    SetStatus("Transfer failed.");
                    onTransferFailed(ex);
                }
                finally
                {
                    // Release only the lock acquired for this request, including canceled requests.
                    mutex.ReleaseLock();
                    CompletePending();
                }
            }, () =>
            {
                CompletePending();
                if (version == transferVersion)
                    SetStatus("Chest busy. Try again.");
            });
        }
        catch (Exception ex)
        {
            CompletePending();
            SetStatus("Transfer failed.");
            onTransferFailed(ex);
        }
    }





    private void CancelPendingTransfer()
    {
        transferVersion++;
        if (pendingTransfer is { } pending)
            pending.Source.TransferStatus = pending.Target.TransferStatus = "Transfer canceled.";
        // An invalid/removed chest may never tick its mutex again. Let the UI proceed;
        // any late acquisition still validates its version and releases without moving items.
        pendingTransfer = null;
    }

}
