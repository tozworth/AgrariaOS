using System.Globalization;
using System.Reflection;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Triggers;

namespace AgrariaOS.Interactions;

internal static partial class TradeActions
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










    private static bool _isStorageShop(ShopMenu shop)
    {
        try { return StorageShopField?.GetValue(shop) is true; }
        catch { return false; }
    }

}
