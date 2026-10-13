using AgrariaOS.Desktop;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private void InstallHudPatches()
    {
        try
        {
            // Replacing this one menu's drawing preserves vanilla HUD visibility and UI scaling.
            // Harmony is provided by SMAPI; it isn't bundled with this mod.
            new Harmony(ModManifest.UniqueID).Patch(
                original: AccessTools.DeclaredMethod(typeof(DayTimeMoneyBox), nameof(DayTimeMoneyBox.draw), new[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DrawClockPrefix))
            );
            patchInstalled = true;
            Monitor.Log($"Status readout ready. {config.ToggleKey} switches between Agraria OS and the vanilla HUD.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Couldn't replace the clock panel; keeping the vanilla HUD.\n{ex}", LogLevel.Error);
        }

        if (patchInstalled)
        {
            InstallNotificationPatches();
            try
            {
                var harmony = new Harmony(ModManifest.UniqueID);
                harmony.Patch(
                    original: AccessTools.DeclaredMethod(typeof(Toolbar), nameof(Toolbar.draw), new[] { typeof(SpriteBatch) }),
                    prefix: new HarmonyMethod(typeof(ModEntry), nameof(DrawToolbarPrefix))
                );
                harmony.Patch(
                    original: AccessTools.DeclaredMethod(typeof(Toolbar), nameof(Toolbar.gameWindowSizeChanged)),
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(ResizeToolbarPostfix))
                );
                harmony.Patch(
                    original: AccessTools.DeclaredMethod(typeof(Toolbar), nameof(Toolbar.isWithinBounds)),
                    prefix: new HarmonyMethod(typeof(ModEntry), nameof(ToolbarBoundsPrefix))
                );
                toolbarPatchInstalled = true;
                Monitor.Log("Compact toolbar ready.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Couldn't replace the toolbar; keeping the vanilla toolbar.\n{ex}", LogLevel.Error);
            }
        }

        if (toolbarPatchInstalled)
        {
            try
            {
                new Harmony(ModManifest.UniqueID).Patch(
                    original: AccessTools.DeclaredMethod(typeof(Game1), "drawHUD"),
                    transpiler: new HarmonyMethod(typeof(ModEntry), nameof(DrawHudTranspiler))
                );
                vitalsPatchInstalled = true;
                Monitor.Log("Text health and energy ready.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Couldn't replace the health/energy bars; keeping the vanilla bars.\n{ex}", LogLevel.Error);
            }
        }
    }

    private static IEnumerable<CodeInstruction> DrawHudTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        List<CodeInstruction> codes = instructions.ToList();
        int start = codes.FindIndex(code => code.opcode == OpCodes.Ldc_R4 && code.operand is float value && value == 0.625f);
        int end = codes.FindIndex(code => code.LoadsField(AccessTools.Field(typeof(Game1), nameof(Game1.onScreenMenus))));
        if (start < 0 || end <= start || codes[start].blocks.Count > 0 || codes[end].blocks.Count > 0)
            throw new InvalidOperationException("The vanilla HUD bar block wasn't recognized.");

        // Keep event guards, on-screen menus and tracking indicators intact. Only bypass the bars.
        Label afterBars = generator.DefineLabel();
        codes[end].labels.Add(afterBars);
        var check = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(HideVanillaVitals)));
        check.labels.AddRange(codes[start].labels);
        codes[start].labels.Clear();
        codes.InsertRange(start, new[] { check, new CodeInstruction(OpCodes.Brtrue, afterBars) });
        return codes;
    }

    private static bool HideVanillaVitals()
    {
        if (!Instance.UseTextVitals)
            return false;

        // These flags also drive vanilla's low-health screen flash; preserve that behavior.
        Game1.showingHealth = Game1.currentLocation is MineShaft or Woods or SlimeHutch or VolcanoDungeon
            || Game1.player.health < Game1.player.maxHealth;
        Game1.showingHealthBar |= Game1.showingHealth;
        return true;
    }

    private static bool DrawToolbarPrefix(Toolbar __instance, SpriteBatch b)
    {
        ModEntry mod = Instance;
        if (!mod.UseCompactToolbar)
        {
            mod.toolbarRenderer.Value.RestoreLayout(__instance);
            return true;
        }

        try
        {
            mod.toolbarRenderer.Value.Draw(b, __instance, mod.config, mod.UseTextVitals, mod.pelicom.Value.IsVisible);
            return false;
        }
        catch (Exception ex)
        {
            mod.osHudActive.Value = false;
            mod.toolbarRenderer.Value.RestoreLayout(__instance);
            mod.Monitor.Log($"Compact toolbar failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
            return true;
        }
    }

    private static void ResizeToolbarPostfix(Toolbar __instance)
    {
        if (Instance.UseCompactToolbar)
            Instance.toolbarRenderer.Value.ApplyDrawnLayout(__instance);
    }

    private static bool ToolbarBoundsPrefix(Toolbar __instance, int x, int y, ref bool __result)
    {
        ModEntry mod = Instance;
        if (!mod.UseCompactToolbar)
        {
            mod.toolbarRenderer.Value.RestoreLayout(__instance);
            return true;
        }

        // Vanilla assumes 64px slots here; use the visible strip so adjacent world clicks pass through.
        Rectangle strip = mod.toolbarRenderer.Value.GetDrawnBounds(__instance);
        __result = strip.Contains(x, y);
        return false;
    }

    private static bool DrawClockPrefix(DayTimeMoneyBox __instance, SpriteBatch b)
    {
        ModEntry mod = Instance;
        if (!mod.config.Enabled || !mod.osHudActive.Value || !Context.IsWorldReady || __instance != Game1.dayTimeMoneyBox)
            return true;

        try
        {
            Rectangle workArea = mod.pelicom.Value.IsVisible ? Pelicom.GetHudWorkArea(mod.config)
                : new Rectangle(0, 0, Game1.uiViewport.Width - 8, Game1.uiViewport.Height - 8);
            mod.statusReadoutRenderer.Value.Draw(b, __instance, mod.config, workArea);
            return false;
        }
        catch (Exception ex)
        {
            // Fail once per screen, then let vanilla restore its drawing and button positions.
            mod.osHudActive.Value = false;
            mod.Monitor.Log($"Status readout failed; restored the vanilla clock.\n{ex}", LogLevel.Error);
            return true;
        }
    }
}
