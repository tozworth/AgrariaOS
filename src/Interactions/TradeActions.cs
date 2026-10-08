using System.Globalization;
using System.Reflection;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Triggers;

namespace AgrariaOS.Interactions;

internal static class TradeActions
{
    private static readonly FieldInfo? SellPercentageField = typeof(ShopMenu)
        .GetField("sellPercentage", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? StorageShopField = typeof(ShopMenu)
        .GetField("_isStorageShop", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly HashSet<string> StorageShopIds = new(StringComparer.Ordinal)
    {
        "ReturnedDonations",
        "FishTank",
        "Dresser",
    };

    internal static bool IsStorageShop(ShopMenu shop)
    {
        if (StorageShopIds.Contains(shop.ShopId))
            return true;
        try { return StorageShopField?.GetValue(shop) is true; }
        catch { return false; }
    }

    internal static float GetSellPercentage(ShopMenu shop)
    {
        try
        {
            if (SellPercentageField?.GetValue(shop) is float percentage && float.IsFinite(percentage) && percentage > 0)
                return percentage;
        }
        catch { }
        return 1f;
    }

    internal static int GetCurrencyAmount(Farmer who, int currency)
        => ShopMenu.getPlayerCurrencyAmount(who, currency);

    internal static string CurrencySuffix(int currency) => currency switch
    {
        0 => "g",
        1 => "tokens",
        2 => "coins",
        4 => "gems",
        _ => "",
    };

    internal static string PriceText(ShopMenu shop, ISalable salable)
    {
        if (!shop.itemPriceAndStock.TryGetValue(salable, out ItemStockInformation? stock))
            return "";
        string price = stock.Price > 0 ? $"{stock.Price.ToString("N0", CultureInfo.InvariantCulture)} {CurrencySuffix(shop.currency)}".Trim() : "";
        if (stock.TradeItem == null)
            return price;
        int count = stock.TradeItemCount ?? 5;
        string tradeName = GetTradeItemName(stock.TradeItem);
        string trade = $"{count}x {tradeName}".Trim();
        return string.IsNullOrEmpty(price) ? trade : $"{price} + {trade}";
    }

    internal static int? PriceValue(ShopMenu shop, ISalable salable)
        => shop.itemPriceAndStock.TryGetValue(salable, out ItemStockInformation? stock) ? stock.Price : null;

    internal static int? StockValue(ShopMenu shop, ISalable salable)
    {
        if (!shop.itemPriceAndStock.TryGetValue(salable, out ItemStockInformation? stock))
            return null;
        return stock.Stock == int.MaxValue ? null : stock.Stock;
    }

    internal static string StockText(ShopMenu shop, ISalable salable)
    {
        if (!shop.itemPriceAndStock.TryGetValue(salable, out ItemStockInformation? stock))
            return "";
        return stock.Stock == int.MaxValue ? "∞" : stock.Stock.ToString(CultureInfo.InvariantCulture);
    }

    internal static int StockSort(ShopMenu shop, ISalable salable)
    {
        if (!shop.itemPriceAndStock.TryGetValue(salable, out ItemStockInformation? stock))
            return -1;
        return stock.Stock == int.MaxValue ? int.MaxValue : stock.Stock;
    }

    internal static bool CanSell(ShopMenu shop, Item item)
    {
        try { return shop.highlightItemToSell(item); }
        catch { return false; }
    }

    internal static int GetSellUnitPrice(ShopMenu shop, ISalable salable)
        => (int)(salable.sellToStorePrice(-1L) * GetSellPercentage(shop));

    internal static int GetMaxBuyable(ShopMenu shop, Farmer farmer, ISalable salable)
    {
        if (shop.readOnly || !shop.itemPriceAndStock.TryGetValue(salable, out ItemStockInformation? stock))
            return 0;
        if (stock.Stock <= 0)
            return 0;
        int max = stock.Stock == int.MaxValue ? 9999 : stock.Stock;
        if (stock.Price > 0)
        {
            int affordable = GetCurrencyAmount(farmer, shop.currency) / Math.Max(1, stock.Price);
            max = Math.Min(max, affordable);
        }
        if (stock.TradeItem != null)
        {
            int need = stock.TradeItemCount ?? 5;
            int have = CountTradeItem(farmer, stock.TradeItem);
            max = Math.Min(max, need > 0 ? have / need : max);
        }
        // Match vanilla's per-click clamp so a typed quantity never exceeds one cursor stack.
        try { max = Math.Min(max, Math.Max(1, salable.GetSalableInstance().maximumStackSize())); }
        catch { }
        return Math.Max(0, max);
    }

    internal static int CountTradeItem(Farmer farmer, string itemId)
    {
        try
        {
            string qualified = ItemRegistry.QualifyItemId(itemId);
            if (qualified == "(O)858")
                return farmer.QiGems;
            if (qualified == "(O)73")
                return Game1.netWorldState.Value.GoldenWalnuts;
            int count = 0;
            foreach (Item? item in farmer.Items)
            {
                if (item != null && string.Equals(item.QualifiedItemId, qualified, StringComparison.Ordinal))
                    count += item.Stack;
            }
            return count;
        }
        catch { return 0; }
    }

    private static string GetTradeItemName(string itemId)
    {
        try
        {
            var data = ItemRegistry.GetDataOrErrorItem(itemId);
            string name = data.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? itemId : name;
        }
        catch { return itemId; }
    }

    internal sealed record BuyResult(string? Error, string? Status, bool ShouldClose);

    internal static BuyResult Buy(ShopMenu shop, ISalable salable, int count)
    {
        Farmer farmer = Game1.player;
        if (shop.readOnly)
            return new BuyResult(null, "This shop is read-only.", false);
        if (!shop.itemPriceAndStock.TryGetValue(salable, out ItemStockInformation? stockInfo))
            return new BuyResult(null, "That item is no longer for sale.", false);
        if (stockInfo.Stock == 0)
            return new BuyResult(null, "Out of stock.", false);
        if (count < 1)
            return new BuyResult("Enter a quantity of at least 1.", null, false);
        if (stockInfo.Stock != int.MaxValue && count > stockInfo.Stock)
            return new BuyResult($"Only {stockInfo.Stock} left in stock.", null, false);

        int totalPrice = stockInfo.Price * count;
        int tradeNeed = 0;
        string? tradeItem = stockInfo.TradeItem;
        if (tradeItem != null)
        {
            tradeNeed = (stockInfo.TradeItemCount ?? 5) * count;
            if (!shop.HasTradeItem(tradeItem, tradeNeed))
                return new BuyResult("Missing trade items.", null, false);
        }
        if (GetCurrencyAmount(farmer, shop.currency) < totalPrice)
            return new BuyResult("Not enough funds.", null, false);

        ISalable fresh;
        try
        {
            fresh = salable.GetSalableInstance();
            if (!fresh.CanBuyItem(farmer) && !salable.IsInfiniteStock() && !salable.IsRecipe)
            {
                Game1.playSound("smallSelect");
                return new BuyResult(null, "You can't buy that yet.", false);
            }
        }
        catch (Exception ex)
        {
            return new BuyResult(null, $"Purchase failed: {ex.Message}", false);
        }

        int perSet = 0;
        try { perSet = salable.Stack; } catch { perSet = 1; }
        if (perSet <= 0)
            perSet = 1;
        int totalItems = perSet * count;

        if (shop.buyBackItems.Contains(salable))
            shop.BuyBuybackItem(salable, totalPrice, totalItems);
        ShopMenu.chargePlayer(farmer, shop.currency, totalPrice);
        if (!string.IsNullOrEmpty(tradeItem))
            shop.ConsumeTradeItem(tradeItem, tradeNeed);

        bool consumed = false;
        try { consumed = !_isStorageShop(shop) && salable.actionWhenPurchased(shop.ShopId); }
        catch { consumed = false; }

        if (consumed)
        {
            if (salable.IsRecipe && salable is Item recipeItem)
            {
                try { recipeItem.LearnRecipe(); } catch { }
                Game1.playSound("newRecipe");
            }
        }
        else
        {
            ISalable purchased = salable.GetSalableInstance();
            purchased.Stack = totalItems;
            if (purchased is Item purchasedItem && purchasedItem.QualifiedItemId == "(O)858")
            {
                try { farmer.team.addQiGemsToTeam.Fire(purchased.Stack); } catch { }
            }
            else if (purchased is Item asItem)
            {
                if (!farmer.addItemToInventoryBool(asItem))
                {
                    // Refund on full backpack; vanilla would hold the cursor stack instead.
                    ShopMenu.chargePlayer(farmer, shop.currency, -totalPrice);
                    if (!string.IsNullOrEmpty(tradeItem))
                        RefundTradeItem(farmer, tradeItem, tradeNeed);
                    return new BuyResult("Backpack full.", null, false);
                }
                Game1.playSound("purchaseClick");
            }
            else
            {
                return new BuyResult("That item can't fit in the backpack.", null, false);
            }
        }

        if (stockInfo.Stock != int.MaxValue && !salable.IsInfiniteStock())
        {
            try { shop.HandleSynchedItemPurchase(salable, farmer, count); } catch { stockInfo.Stock -= count; }
            try
            {
                if (stockInfo.ItemToSyncStack != null)
                    stockInfo.ItemToSyncStack.Stack = stockInfo.Stock;
            }
            catch { }
        }
        if (stockInfo.ActionsOnPurchase is { Count: > 0 } actions)
        {
            foreach (string action in actions)
            {
                try { TriggerActionManager.TryRunAction(action, out _, out _); }
                catch { }
            }
        }

        bool shouldClose = false;
        try { shouldClose = shop.onPurchase != null && shop.onPurchase(salable, farmer, count, stockInfo); }
        catch { shouldClose = false; }

        if (stockInfo.Stock <= 0)
        {
            shop.buyBackItems.Remove(salable);
            shop.forSale.Remove(salable);
            shop.itemPriceAndStock.Remove(salable);
        }

        string name = salable.DisplayName;
        string status = totalItems != perSet || count > 1
            ? $"Bought {totalItems}x {name} for {totalPrice} {CurrencySuffix(shop.currency)}.".Trim()
            : $"Bought {name} for {totalPrice} {CurrencySuffix(shop.currency)}.".Trim();
        return new BuyResult(null, status, shouldClose);
    }

    internal sealed record SellResult(string? Status);

    internal static SellResult Sell(ShopMenu shop, Item item)
    {
        Farmer farmer = Game1.player;
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
        return new SellResult(total > 0
            ? $"Sold {stack}x {item.DisplayName} for {total} {suffix}.".Trim()
            : $"Sold {stack}x {item.DisplayName}.");
    }

    private static bool _isStorageShop(ShopMenu shop)
    {
        try { return StorageShopField?.GetValue(shop) is true; }
        catch { return false; }
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
