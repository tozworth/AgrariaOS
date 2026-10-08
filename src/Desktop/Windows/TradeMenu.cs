using AgrariaOS.Configuration;
using AgrariaOS.Integration;
using AgrariaOS.Interactions;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace AgrariaOS.Desktop.Windows;

partial class TradeWindow : OsWindow
{
    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        if (grid.GetHeaderContextMenuItems(cursor) is { } columns)
            return columns;
        if (shop == null || grid.GetRow(cursor) is not { } item)
            return null;
        Func<bool> IsCurrent = GetContextItemGuard(item);
        if (!IsCurrent())
            return null;
        ISalable[] targets = ContextTargets(item);
        return new[]
        {
            new ContextMenuItem("Buy", () =>
            {
                if (!IsCurrent() || !Context.IsPlayerFree)
                    return;
                ISalable[] live = targets.Where(target => shop.forSale.Any(current => ReferenceEquals(current, target))).ToArray();
                if (live.Length == 0)
                {
                    TransferStatus = "That item is no longer for sale.";
                    return;
                }
                requestBuy(live);
            }, IsEnabled: targets.Length > 0),
        };
    }

    internal override ContextMenuInfo? GetContextMenuInfo(Point cursor)
    {
        if (lookupAnything?.Available != true || shop == null || grid.GetRow(cursor) is not { } salable)
            return null;
        if (salable is not Item item)
            return null;
        Func<bool> isCurrent = GetContextItemGuard(salable);
        return new ContextMenuInfo(() => lookupAnything.GetInventorySubject(item),
            () => isCurrent() && Context.IsPlayerFree);
    }

    private Func<bool> GetContextItemGuard(ISalable item)
    {
        Farmer farmer = Game1.player;
        uint day = Game1.stats.DaysPlayed;
        ShopMenu? captured = shop;
        return () => Context.IsWorldReady && Game1.player == farmer
            && Game1.stats.DaysPlayed == day
            && IsValid && !IsMinimized && ReferenceEquals(shop, captured)
            && captured != null && captured.forSale.Any(current => ReferenceEquals(current, item));
    }

}
