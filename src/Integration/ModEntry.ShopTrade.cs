using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private bool suppressShopIntercept;

    private void HandleShopMenuChanged(IClickableMenu? oldMenu, IClickableMenu? newMenu)
    {
        if (suppressShopIntercept && oldMenu is ShopMenu && newMenu == null)
        {
            suppressShopIntercept = false;
            return;
        }
        if (newMenu is not ShopMenu shop)
            return;
        if (!patchInstalled || !config.Enabled || !osHudActive.Value || !Context.IsWorldReady)
            return;
        try
        {
            if (Interactions.TradeActions.IsStorageShop(shop))
                return;
        }
        catch { return; }

        try
        {
            suppressShopIntercept = true;
            windowManager.Value.OpenTrade(shop);
            Game1.activeClickableMenu = null;
        }
        catch (Exception ex)
        {
            suppressShopIntercept = false;
            Monitor.Log($"Trade window failed; kept vanilla shop.\n{ex}", LogLevel.Error);
        }
    }
}
