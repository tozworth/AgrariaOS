using System.Diagnostics;
using System.Reflection;
using AgrariaOS.Wallet;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private void InstallWalletPatches()
    {
        var harmony = new Harmony(ModManifest.UniqueID);
        MethodBase? moneySetter = (MethodBase?)AccessTools.DeclaredProperty(typeof(Farmer), "_money")?.GetSetMethod()
            ?? AccessTools.Method(typeof(Farmer), "set__money");
        PatchWallet(harmony, "Farmer._money", moneySetter, nameof(WalletMoneyPrefix), nameof(WalletMoneyPostfix));
        PatchWallet(harmony, "ShippingBin.shipItem", AccessTools.DeclaredMethod(typeof(ShippingBin), "shipItem"),
            nameof(BinShipPrefix), nameof(BinShipPostfix));
        PatchWallet(harmony, "ShippingBin.leftClicked", AccessTools.DeclaredMethod(typeof(ShippingBin), nameof(ShippingBin.leftClicked)),
            nameof(BinClickPrefix), nameof(BinClickPostfix));
        PatchWallet(harmony, "Farm.shipItem", AccessTools.DeclaredMethod(typeof(Farm), "shipItem"),
            nameof(FarmShipPrefix), nameof(FarmShipPostfix));
        PatchWallet(harmony, "Chest.addItem", AccessTools.DeclaredMethod(typeof(Chest), nameof(Chest.addItem)),
            nameof(ChestAddPrefix), nameof(ChestAddPostfix));
        Monitor.Log("Wallet ready.", LogLevel.Info);
    }

    private void PatchWallet(Harmony harmony, string label, MethodBase? original, string prefix, string postfix)
    {
        try
        {
            if (original == null)
                throw new InvalidOperationException($"{label} not found.");
            harmony.Patch(original,
                prefix: new HarmonyMethod(typeof(ModEntry), prefix),
                postfix: new HarmonyMethod(typeof(ModEntry), postfix));
        }
        catch (Exception ex)
        {
            // A missed hook only costs itemized labels; the balance poll still journals the gold.
            Monitor.Log($"Wallet couldn't hook {label}; those transactions will show generic entries.\n{ex}", LogLevel.Warn);
        }
    }

    // Observation-only hooks: they never alter the game, only journal what happened.

    private static void WalletMoneyPrefix(Farmer __instance, int value, out int __state)
    {
        __state = 0;
        try
        {
            if (Context.IsWorldReady)
                __state = __instance._money;
        }
        catch { __state = 0; }
    }

    private static void WalletMoneyPostfix(Farmer __instance, int value, int __state)
    {
        try
        {
            if (!Context.IsWorldReady)
                return;
            int delta = value - __state;
            if (delta == 0)
                return;
            long farmerId;
            try { farmerId = __instance.UniqueMultiplayerID; }
            catch { return; }
            string typeName = "";
            string methodName = "";
            try
            {
                foreach (StackFrame frame in new StackTrace().GetFrames())
                {
                    MethodBase? method = frame.GetMethod();
                    Type? declaring = method?.DeclaringType;
                    if (declaring == null || method == null)
                        continue;
                    if (declaring == typeof(ModEntry) || declaring == typeof(WalletLedger))
                        continue;
                    if (declaring == typeof(Farmer) && (method.Name == "set_Money" || method.Name == "set__money"))
                        continue;
                    string full = declaring.FullName ?? "";
                    if (full.StartsWith("System.", StringComparison.Ordinal)
                        || full.StartsWith("HarmonyLib", StringComparison.Ordinal))
                        continue;
                    typeName = full;
                    methodName = method.Name ?? "";
                    break;
                }
            }
            catch { }
            var (text, kind) = WalletLedger.DescribeCaller(typeName, methodName, delta);
            Instance.walletLedger.NoteChange(farmerId, delta, text, kind);
        }
        catch { }
    }

    private sealed class ShipmentSnapshot
    {
        internal string Name = "";
        internal int Qty;
        internal int ExpectedValue;
        internal bool Shippable;
    }

    private static ShipmentSnapshot CaptureShipment(Item? item)
    {
        var snapshot = new ShipmentSnapshot();
        try
        {
            if (item == null)
                return snapshot;
            snapshot.Name = item.DisplayName ?? "";
            snapshot.Qty = Math.Max(0, item.Stack);
            try { snapshot.Shippable = item.canBeShipped(); }
            catch { snapshot.Shippable = false; }
            try
            {
                if (item is StardewValley.Object obj)
                    snapshot.ExpectedValue = obj.sellToStorePrice(-1L) * snapshot.Qty;
            }
            catch { snapshot.ExpectedValue = 0; }
        }
        catch { }
        return snapshot;
    }

    private static long WalletFarmerId(Farmer? who)
    {
        try
        {
            if (who != null)
                return who.UniqueMultiplayerID;
        }
        catch { }
        try { return Game1.player.UniqueMultiplayerID; }
        catch { return 0; }
    }

    private static int WalletMoney(Farmer? who)
    {
        try
        {
            if (who != null)
                return who.Money;
        }
        catch { }
        try { return Game1.player.Money; }
        catch { return 0; }
    }

    private static void RecordShipment(Farmer? who, ShipmentSnapshot snapshot, int qty)
    {
        try
        {
            if (!Context.IsWorldReady || qty <= 0 || string.IsNullOrWhiteSpace(snapshot.Name))
                return;
            long farmerId = WalletFarmerId(who);
            if (farmerId == 0)
                return;
            int unit = snapshot.Qty > 0 ? snapshot.ExpectedValue / snapshot.Qty : 0;
            Instance.walletLedger.RecordShipment(farmerId, snapshot.Name, qty, unit * qty, WalletMoney(who));
        }
        catch { }
    }

    // Vanilla shipping-bin menu path.
    private static void BinShipPrefix(Item i, Farmer who, out ShipmentSnapshot __state)
        => __state = CaptureShipment(i);

    private static void BinShipPostfix(Item i, Farmer who, ShipmentSnapshot __state)
    {
        if (i == null)
            return;
        RecordShipment(who, __state, __state.Qty);
    }

    // Vanilla held-item-against-the-bin path: the bin consumes the whole stack.
    private static void BinClickPrefix(out ShipmentSnapshot __state)
    {
        ShipmentSnapshot snapshot = new();
        try
        {
            if (Context.IsWorldReady)
                snapshot = CaptureShipment(Game1.player.ActiveItem);
        }
        catch { }
        __state = snapshot;
    }

    private static void BinClickPostfix(bool __result, ShipmentSnapshot __state)
    {
        try
        {
            if (!__result || __state.Qty <= 0 || !__state.Shippable || !Context.IsWorldReady)
                return;
            // An opened menu leaves the held stack alone; only journal a consumed one.
            if (Game1.player.ActiveItem != null)
                return;
            RecordShipment(Game1.player, __state, __state.Qty);
        }
        catch { }
    }

    // Rare direct path (other mods); same shape as the bin menu callback.
    private static void FarmShipPrefix(Item i, Farmer who, out ShipmentSnapshot __state)
        => __state = CaptureShipment(i);

    private static void FarmShipPostfix(Item i, Farmer who, ShipmentSnapshot __state)
    {
        if (i == null)
            return;
        RecordShipment(who, __state, __state.Qty);
    }

    // Mini-shipping bins (vanilla chest menus and desktop drag transfers alike).
    private static void ChestAddPrefix(Chest __instance, Item item, out ShipmentSnapshot __state)
        => __state = CaptureShipment(item);

    private static void ChestAddPostfix(Chest __instance, Item item, Item? __result, ShipmentSnapshot __state)
    {
        try
        {
            if (!Context.IsWorldReady || __state.Qty <= 0)
                return;
            if (__instance.SpecialChestType != Chest.SpecialChestTypes.MiniShippingBin)
                return;
            int moved = __result == null ? __state.Qty
                : Math.Max(0, __state.Qty - Math.Max(0, item?.Stack ?? 0));
            if (moved <= 0)
                return;
            RecordShipment(Game1.player, __state, moved);
        }
        catch { }
    }
}
