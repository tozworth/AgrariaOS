using System.Globalization;
using System.Reflection;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Triggers;

namespace AgrariaOS.Interactions;

internal static partial class TradeActions
{
    internal static bool CanSell(ShopMenu shop, Item item)
    {
        try { return shop.highlightItemToSell(item); }
        catch { return false; }
    }

    internal static int GetSellUnitPrice(ShopMenu shop, ISalable salable)
        => (int)(salable.sellToStorePrice(-1L) * GetSellPercentage(shop));

    internal sealed record SellResult(string? Status);

    internal static SellResult Sell(ShopMenu shop, Item item)
    {
        Farmer farmer = Game1.player;
        int goldBefore = 0;
        try { goldBefore = farmer.Money; }
        catch { }
        if (!CanSell(shop, item))
            return new SellResult("The shop won't buy that.");
        int stack = item.Stack;
        if (stack <= 0)
            return new SellResult("Nothing to sell.");

        // Inventory.leftClick already detaches in vanilla; mirror that before callbacks.
        farmer.removeItemFromInventory(item);

        if (shop.onSell != null)
        {
            try { shop.onSell(item); } catch { }
            Game1.playSound("sell");
            NoteTradeGold(farmer, shop, goldBefore, item.DisplayName, stack);
            return new SellResult($"Sold {stack}x {item.DisplayName}.");
        }

        int unitPrice = GetSellUnitPrice(shop, item);
        int total = unitPrice * stack;
        ShopMenu.chargePlayer(farmer, shop.currency, -total);

        ISalable? buyback = null;
        try
        {
            if (shop.CanBuyback())
                buyback = shop.AddBuybackItem(item, unitPrice, stack);
        }
        catch { }

        try
        {
            if (item is StardewValley.Object obj && obj.Edibility != -300
                && Game1.random.NextDouble() < 0.04 && Game1.currentLocation is ShopLocation shopLocation)
            {
                Item one = obj.getOne();
                one.Stack = obj.Stack;
                if (buyback != null && shop.buyBackItemsToResellTomorrow.TryGetValue(buyback, out ISalable? existing))
                    existing.Stack += obj.Stack;
                else
                {
                    if (buyback != null)
                        shop.buyBackItemsToResellTomorrow[buyback] = one;
                    shopLocation.itemsToStartSellingTomorrow.Add(one);
                }
            }
        }
        catch { }

        Game1.playSound("sell");
        string suffix = CurrencySuffix(shop.currency);
        NoteTradeGold(farmer, shop, goldBefore, item.DisplayName, stack);
        return new SellResult(total > 0
            ? $"Sold {stack}x {item.DisplayName} for {total} {suffix}.".Trim()
            : $"Sold {stack}x {item.DisplayName}.");
    }

    private static void RefundTradeItem(Farmer farmer, string itemId, int count)
    {
        try
        {
            string qualified = ItemRegistry.QualifyItemId(itemId);
            if (qualified == "(O)858")
            {
                farmer.QiGems += count;
                return;
            }
            if (qualified == "(O)73")
            {
                Game1.netWorldState.Value.GoldenWalnuts += count;
                return;
            }
            Item? sample = ItemRegistry.Create(qualified);
            if (sample == null)
                return;
            sample.Stack = count;
            farmer.addItemToInventoryBool(sample);
        }
        catch { }
    }

}
