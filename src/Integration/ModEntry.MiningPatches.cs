using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private bool miningPatchInstalled;

    private void InstallMiningPatches()
    {
        try
        {
            // Debris pickup is the mining signal: mined stones, monster drops and magnet
            // collection all land here, while chest shuffling and shop purchases don't.
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(Debris), nameof(Debris.collect)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(CollectDebrisPostfix)));
            miningPatchInstalled = true;
            Monitor.Log("Mining ledger ready.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Couldn't track mining pickups; the ledger will stay empty.\n{ex}", LogLevel.Error);
        }
    }

    private static void CollectDebrisPostfix(Debris __instance, Farmer farmer, bool __result)
    {
        ModEntry mod = Instance;
        // Keep recording while F8 shows vanilla so the day's graph survives the toggle.
        if (!mod.miningPatchInstalled || !__result || farmer == null || !Context.IsWorldReady)
            return;
        try
        {
            // Ore debris carries its qualified ID on the debris itself; each pickup is one unit.
            mod.miningLedger.Record(farmer.UniqueMultiplayerID, __instance?.itemId.Value, 1);
        }
        catch (Exception ex)
        {
            mod.miningPatchInstalled = false;
            mod.Monitor.Log($"Mining capture failed; the ledger will stay empty.\n{ex}", LogLevel.Error);
        }
    }
}
