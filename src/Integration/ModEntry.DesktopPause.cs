using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private bool desktopPausePatchesInstalled;

    private bool HasDesktopFocus => desktopPausePatchesInstalled && UsePelicom
        && windowManager.Value.HasDesktopFocus;

    private void InstallDesktopPausePatches()
    {
        if (!patchInstalled)
            return;
        try
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.shouldTimePass)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(DesktopTimePassPostfix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Farmer), nameof(Farmer.MovePosition)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DesktopMovementPrefix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.pressActionButton)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DesktopActionPrefix)));
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.pressUseToolButton)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DesktopActionPrefix)));
            // Vanilla queues FireTool before pressUseToolButton. Guard the event handler;
            // FireTool itself is small enough to be inlined by the game's runtime.
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Farmer), "performFireTool"),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DesktopFireToolPrefix)));
            desktopPausePatchesInstalled = true;
            Monitor.Log("Desktop focus pause ready.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            // Every hook is gated on successful installation; partial installation stays inert.
            Monitor.Log($"Couldn't enable desktop pausing.\n{ex}", LogLevel.Error);
        }
    }

    private static void DesktopTimePassPostfix(bool ignore_multiplayer, ref bool __result)
    {
        // Preserve online time authority. Split screen calls with ignore_multiplayer to
        // collect each local farmer's pause request and pauses only when all agree.
        if (__result && (!Game1.IsMultiplayer || ignore_multiplayer) && Instance.HasDesktopFocus)
            __result = false;
    }

    private static bool DesktopMovementPrefix(Farmer __instance)
        => __instance != Game1.player || !Instance.HasDesktopFocus;

    private static bool DesktopActionPrefix(ref bool __result)
    {
        if (!Instance.HasDesktopFocus)
            return true;
        __result = false;
        return false;
    }

    private static bool DesktopFireToolPrefix(Farmer __instance)
        => __instance != Game1.player || !Instance.HasDesktopFocus;

    private void UpdateDesktopPause()
    {
        if (!HasDesktopFocus)
            return;
        clickToMove.Value.Cancel();
        // Don't change CanMove/freezeControls: vanilla and other mods own those flags,
        // and desktop transfers still need the normal player-free context.
        Game1.player.Halt();
        var options = Game1.options;
        foreach (InputButton binding in options.moveUpButton.Concat(options.moveRightButton)
            .Concat(options.moveDownButton).Concat(options.moveLeftButton)
            .Concat(options.actionButton).Concat(options.useToolButton))
        {
            SButton button = binding.ToSButton();
            // Mouse presses still reach native HUD controls; desktop clicks are consumed
            // by WindowManager, and actual world clicks release focus before dispatch.
            if (button is not (SButton.None or SButton.MouseLeft or SButton.MouseRight)
                && button != config.ToggleKey && Helper.Input.IsDown(button))
                Helper.Input.Suppress(button);
        }
        foreach (SButton button in new[] { SButton.ControllerA, SButton.ControllerX,
            SButton.DPadUp, SButton.DPadRight, SButton.DPadDown, SButton.DPadLeft,
            SButton.LeftThumbstickUp, SButton.LeftThumbstickRight, SButton.LeftThumbstickDown, SButton.LeftThumbstickLeft })
        {
            if (button != config.ToggleKey && Helper.Input.IsDown(button))
                Helper.Input.Suppress(button);
        }
    }
}
