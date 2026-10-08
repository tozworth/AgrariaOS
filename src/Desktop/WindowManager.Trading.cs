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
    private void SellDraggedItems(InventoryWindow source, TradeWindow target, Item[] requested)
    {
        ShopMenu? shop = target.Shop;
        if (shop == null || !source.IsValid || !target.IsValid || !Context.IsPlayerFree)
            return;
        Farmer farmer = Game1.player;
        var live = requested.Where(item => InventoryTransferBatch.IndexOf(farmer.Items, item) >= 0).ToArray();
        if (live.Length == 0)
        {
            source.TransferStatus = target.TransferStatus = "That stack is no longer available.";
            return;
        }
        foreach (Item item in live)
        {
            if (!TradeActions.CanSell(shop, item))
            {
                source.TransferStatus = target.TransferStatus = $"The shop won't buy {item.DisplayName}.";
                return;
            }
        }
        string? lastStatus = null;
        foreach (Item item in live)
            lastStatus = TradeActions.Sell(shop, item).Status;
        source.TransferStatus = target.TransferStatus = live.Length == 1 ? lastStatus : $"Sold {live.Length} stacks.";
    }

    internal void EnqueueBuys(ISalable[] requested)
    {
        TradeWindow? trade = GetTradeWindow();
        ShopMenu? shop = trade?.Shop;
        if (trade == null || shop == null || !trade.IsValid || requested.Length == 0)
            return;
        if (!Context.IsPlayerFree && Game1.activeClickableMenu is not NumberEntryMenu)
            return;
        foreach (ISalable salable in requested)
        {
            if (!shop.forSale.Any(current => ReferenceEquals(current, salable)))
                continue;
            bool stackable;
            try { stackable = salable.maximumStackSize() > 1; }
            catch { stackable = false; }
            if (!stackable)
            {
                var result = TradeActions.Buy(shop, salable, 1);
                trade.TransferStatus = result.Error ?? result.Status;
                if (result.ShouldClose)
                {
                    pendingBuys.Clear();
                    CloseTrade();
                    return;
                }
            }
            else if (!pendingBuys.Any(queued => ReferenceEquals(queued, salable)))
                pendingBuys.Enqueue(salable);
        }
        ProcessBuyQueue();
    }

    private void ProcessBuyQueue()
    {
        if (Game1.activeClickableMenu != null || pendingBuys.Count == 0)
            return;
        if (!Context.IsWorldReady)
        {
            pendingBuys.Clear();
            return;
        }
        TradeWindow? trade = GetTradeWindow();
        ShopMenu? shop = trade?.Shop;
        if (trade == null || shop == null || !trade.IsValid || !windows.Contains(trade))
        {
            pendingBuys.Clear();
            return;
        }
        ISalable salable = pendingBuys.Dequeue();
        if (!shop.forSale.Any(current => ReferenceEquals(current, salable)))
        {
            trade.TransferStatus = "That item is no longer for sale.";
            ProcessBuyQueue();
            return;
        }
        Farmer farmer = Game1.player;
        int max = TradeActions.GetMaxBuyable(shop, farmer, salable);
        if (max <= 0)
        {
            trade.TransferStatus = TradeActions.GetCurrencyAmount(farmer, shop.currency) < (shop.itemPriceAndStock.TryGetValue(salable, out var stock) ? stock.Price : 0)
                ? "Not enough funds." : "Out of stock.";
            ProcessBuyQueue();
            return;
        }
        string unitPrice = TradeActions.PriceText(shop, salable);
        string suffix = TradeActions.CurrencySuffix(shop.currency);
        string title = $"Buy {salable.DisplayName}";
        string PricePrompt()
        {
            int liveBalance = TradeActions.GetCurrencyAmount(Game1.player, shop.currency);
            int liveMax = TradeActions.GetMaxBuyable(shop, Game1.player, salable);
            return $"{unitPrice} each (You have {liveBalance} {suffix}) - Qty 1-{liveMax}".Trim();
        }
        TradeWindow capturedTrade = trade;
        ShopMenu capturedShop = shop;
        ISalable capturedItem = salable;
        bool IsCurrent() => Context.IsWorldReady && capturedTrade.IsValid && !capturedTrade.IsMinimized
            && windows.Contains(capturedTrade) && ReferenceEquals(capturedTrade.Shop, capturedShop)
            && capturedShop.forSale.Any(current => ReferenceEquals(current, capturedItem));
        Game1.activeClickableMenu = new NumberEntryMenu(config, title, PricePrompt, amount =>
        {
            if (!IsCurrent())
                return "Shop closed.";
            int currentMax = TradeActions.GetMaxBuyable(capturedShop, Game1.player, capturedItem);
            if (amount < 1 || amount > currentMax)
                return $"Enter a number from 1 to {Math.Max(1, currentMax)}.";
            var result = TradeActions.Buy(capturedShop, capturedItem, amount);
            if (result.Error != null)
                return result.Error;
            capturedTrade.TransferStatus = result.Status;
            if (result.ShouldClose)
            {
                pendingBuys.Clear();
                CloseTrade();
            }
            return null;
        }, IsCurrent, confirmLabel: "Buy");
    }

}
