using AgrariaOS.Configuration;
using AgrariaOS.UI;
using AgrariaOS.Wallet;
using AgrariaOS.Interactions;
using AgrariaOS.Integration;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace AgrariaOS.Desktop.Windows;

partial class InventoryWindow : OsWindow
{
    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        if (grid.GetHeaderContextMenuItems(cursor) is { } columns)
            return columns;
        Item? item = grid.GetRow(cursor);
        if (item == null)
            return null;
        Farmer farmer = Game1.player;
        Func<bool> IsCurrent = GetContextItemGuard(item);
        if (!IsCurrent())
            return null;
        var entries = new List<ContextMenuItem>();
        if (chest != null)
            return lookupAnything?.Available == true ? Array.Empty<ContextMenuItem>() : null;
        Item[] targets = selection.ContextTargets(item, farmer.Items.Where(current => current != null).ToArray());
        entries.AddRange(new ContextMenuItem[]
        {
            new("Split", () =>
            {
                if (targets.Length != 1 || !IsCurrent() || !Context.IsPlayerFree)
                    return;
                if (item.maximumStackSize() <= 1 || item.Stack <= 1)
                {
                    TransferStatus = "This item cannot be split.";
                    return;
                }
                Game1.activeClickableMenu = new NumberEntryMenu(config, $"Split {item.DisplayName}",
                    () => $"Quantity for the new stack (1-{item.Stack - 1})", amount =>
                    {
                        string? error = InventoryStackActions.Split(farmer.Items, farmer.MaxItems, item, amount);
                        if (error == null)
                            TransferStatus = null;
                        return error;
                    }, IsCurrent);
            }, IsEnabled: targets.Length == 1),
            new("Jettison", () =>
            {
                if (!IsCurrent() || !Context.IsPlayerFree)
                    return;
                Item[] liveTargets = targets.Where(target => InventoryTransferBatch.IndexOf(farmer.Items, target) >= 0).ToArray();
                if (liveTargets.Any(target => !target.canBeTrashed()))
                {
                    TransferStatus = "The selection contains an item that cannot be trashed.";
                    return;
                }
                // Match vanilla's canBeTrashed guard and trash helper, including
                // upgraded-can reclamation and special-item bookkeeping.
                // Reclamation gold is journaled per stack so it never reads as earnings.
                foreach (Item target in liveTargets)
                {
                    int stack = target.Stack;
                    string name = target.DisplayName;
                    int goldBefore = 0;
                    try { goldBefore = farmer.Money; }
                    catch { }
                    farmer.removeItemFromInventory(target);
                    Utility.trashItem(target);
                    try
                    {
                        int delta = farmer.Money - goldBefore;
                        if (delta == 0 || WalletLedger.Active is not { } ledger || !Context.IsWorldReady)
                            continue;
                        long farmerId = farmer.UniqueMultiplayerID;
                        ledger.Record(farmerId, delta, $"Trashed {stack}x {name}",
                            WalletLedger.KindTrash, farmer.Money);
                    }
                    catch { }
                }
                selection.Refresh(farmer.Items.Where(current => current != null).ToArray());
                TransferStatus = null;
            }, IsEnabled: targets.All(target => target.canBeTrashed()))
        });
        if (chest == null && getTradeShop?.Invoke() is { } shop)
        {
            ShopMenu capturedShop = shop;
            entries.Add(new ContextMenuItem("Sell", () =>
            {
                if (!IsCurrent() || !Context.IsPlayerFree)
                    return;
                if (getTradeShop?.Invoke() is not { } currentShop || !ReferenceEquals(currentShop, capturedShop))
                {
                    TransferStatus = "Shop closed.";
                    return;
                }
                Item[] liveTargets = targets.Where(target => InventoryTransferBatch.IndexOf(farmer.Items, target) >= 0).ToArray();
                if (liveTargets.Length == 0)
                {
                    TransferStatus = "That stack is no longer available.";
                    return;
                }
                var sold = new List<string>();
                string? lastStatus = null;
                foreach (Item target in liveTargets)
                {
                    if (!TradeActions.CanSell(shop, target))
                    {
                        TransferStatus = $"The shop won't buy {target.DisplayName}.";
                        return;
                    }
                    lastStatus = TradeActions.Sell(shop, target).Status;
                    sold.Add(target.DisplayName);
                }
                selection.Refresh(farmer.Items.Where(current => current != null).ToArray());
                TransferStatus = sold.Count == 1 ? lastStatus : $"Sold {sold.Count} stacks.";
                if (TransferStatus != null && shop.forSale.Count == 0)
                    TransferStatus += " Shop is sold out.";
            }, IsEnabled: targets.All(target => TradeActions.CanSell(shop, target))));
        }
        return entries.ToArray();
    }

    internal override ContextMenuInfo? GetContextMenuInfo(Point cursor)
    {
        if (lookupAnything?.Available != true || grid.GetRow(cursor) is not { } item)
            return null;
        Func<bool> isCurrent = GetContextItemGuard(item);
        return new ContextMenuInfo(() => lookupAnything.GetInventorySubject(item),
            () => isCurrent() && Context.IsPlayerFree);
    }

    private Func<bool> GetContextItemGuard(Item item)
    {
        Farmer farmer = Game1.player;
        GameLocation location = Game1.currentLocation;
        uint day = Game1.stats.DaysPlayed;
        return () => Context.IsWorldReady && Game1.player == farmer
            && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && IsValid && !IsMinimized
            && (chest?.GetItemsForPlayer() ?? farmer.Items).Any(current => ReferenceEquals(current, item));
    }

}
