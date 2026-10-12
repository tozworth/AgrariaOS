using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private void InstallFurnitureInputPatches()
    {
        if (!patchInstalled)
            return;
        try
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.pressUseToolButton)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(FurniturePlacementRepeatPrefix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(GameLocation), nameof(GameLocation.LowPriorityLeftClick)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(FurniturePickupRepeatPrefix)));
        }
        catch (Exception ex)
        {
            Monitor.Log($"Couldn't prevent held-click furniture repeats; keeping vanilla input.\n{ex}", LogLevel.Error);
        }
    }

    private static bool IsRepeatedFurnitureMouseInput()
    {
        if (!Instance.UsePelicom || !Context.IsPlayerFree
            || Game1.input.GetMouseState().LeftButton != ButtonState.Pressed
            || Game1.oldMouseState.LeftButton != ButtonState.Pressed)
            return false;
        // Simultaneous keyboard/controller tool actions still use vanilla behavior.
        return !Game1.isOneOfTheseKeysDown(Game1.GetKeyboardState(), Game1.options.useToolButton)
            && (!Game1.options.gamepadControls || !Game1.input.GetGamePadState().IsButtonDown(Buttons.X));
    }

    private static bool FurniturePlacementRepeatPrefix(ref bool __result)
    {
        // Pickup selects the furniture in the hotbar. Stop the next held-mouse
        // repeat before pressUseToolButton can immediately place it again.
        if (!IsRepeatedFurnitureMouseInput() || Game1.player.ActiveObject is not Furniture)
            return true;
        __result = true;
        return false;
    }

    private static bool FurniturePickupRepeatPrefix(GameLocation __instance, int x, int y, Farmer who, ref bool __result)
    {
        // This vanilla fallback handles furniture removal (including wall pieces).
        // Consume repeats so a newly placed piece isn't grabbed again on the same
        // press, and the fallback can't continue into an equipped tool action.
        if (who != Game1.player || !IsRepeatedFurnitureMouseInput())
            return true;
        bool targetsFurniture = false;
        foreach (Furniture furniture in __instance.furniture)
        {
            int targetY = y;
            if (!furniture.isGroundFurniture() && __instance is DecoratableLocation decoratable)
            {
                int wallTop = decoratable.GetWallTopY(x / 64, y / 64);
                if (wallTop != -1)
                    targetY = wallTop * 64;
            }
            var bounds = furniture.GetBoundingBox();
            if (bounds.Contains(x, y) || bounds.Contains(x, targetY))
            {
                targetsFurniture = true;
                break;
            }
        }
        if (!targetsFurniture)
            return true;
        __result = true;
        return false;
    }
}
