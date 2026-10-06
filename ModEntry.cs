using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS;

public sealed class ModEntry : Mod
{
    private static ModEntry Instance = null!;
    private readonly PerScreen<bool> textHudActive = new(() => true);
    private readonly PerScreen<HudRenderer> renderer = new(() => new HudRenderer());
    private ModConfig config = null!;
    private bool patchInstalled;

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        config = helper.ReadConfig<ModConfig>();
        config.Normalize();
        helper.WriteConfig(config);

        try
        {
            // Replacing this one menu's drawing preserves vanilla HUD visibility and UI scaling.
            // Harmony is provided by SMAPI; it isn't bundled with this mod.
            new Harmony(ModManifest.UniqueID).Patch(
                original: AccessTools.DeclaredMethod(typeof(DayTimeMoneyBox), nameof(DayTimeMoneyBox.draw), new[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(DrawClockPrefix))
            );
            patchInstalled = true;
            Monitor.Log($"Text HUD ready. {config.ToggleKey} switches between Agraria OS and the vanilla clock.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Couldn't replace the clock panel; keeping the vanilla HUD.\n{ex}", LogLevel.Error);
        }

        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            textHudActive.ResetAllScreens();
            renderer.ResetAllScreens();
        };
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!patchInstalled || !config.Enabled || !Context.IsWorldReady || !Context.IsPlayerFree || e.Button != config.ToggleKey)
            return;

        textHudActive.Value = !textHudActive.Value;
        Helper.Input.Suppress(e.Button);
        Monitor.Log(textHudActive.Value ? "Agraria OS HUD enabled." : "Vanilla clock enabled.", LogLevel.Info);
    }

    private static bool DrawClockPrefix(DayTimeMoneyBox __instance, SpriteBatch b)
    {
        ModEntry mod = Instance;
        if (!mod.config.Enabled || !mod.textHudActive.Value || !Context.IsWorldReady || __instance != Game1.dayTimeMoneyBox)
            return true;

        try
        {
            mod.renderer.Value.Draw(b, __instance, mod.config);
            return false;
        }
        catch (Exception ex)
        {
            // Fail once per screen, then let vanilla restore its drawing and button positions.
            mod.textHudActive.Value = false;
            mod.Monitor.Log($"Text HUD failed; restored the vanilla clock.\n{ex}", LogLevel.Error);
            return true;
        }
    }
}
