using AgrariaOS.Configuration;
using AgrariaOS.Chat;
using AgrariaOS.Desktop;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Hud;
using AgrariaOS.Notifications;
using AgrariaOS.UI;
using AgrariaOS.Integration;
using AgrariaOS.Mining;
using AgrariaOS.Movement;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS;

partial class ModEntry : Mod
{
    private bool UseCompactToolbar => toolbarPatchInstalled && config.Enabled && osHudActive.Value && Context.IsWorldReady;

    private bool UsePelicom => patchInstalled && config.Enabled && osHudActive.Value && Context.IsWorldReady;

    private void UpdateHardwareCursor()
    {
        // Stardew disables hardware cursors in split screen. Only the primary screen
        // manages the process-wide cursor so another screen can't overwrite it.
        if (Context.ScreenId != 0)
            return;
        if (!UsePelicom || !Game1.options.hardwareCursor || !Game1.game1.IsActiveNoOverlay)
        {
            hardwareCursor.Reset();
            return;
        }
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        WindowResizeEdges edges = windowManager.Value.GetResizeCursorEdges(cursor);
        bool textField = edges == WindowResizeEdges.None && windowManager.Value.IsTextFieldHovered(cursor);
        hardwareCursor.Update(edges, textField);
    }

    private void OnPelicomDrawFailed(Exception ex)
    {
        osHudActive.Value = false;
        hardwareCursor.Reset();
        Monitor.Log($"Pelicom failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    private void OnWindowDrawFailed(Exception ex)
    {
        osHudActive.Value = false;
        hardwareCursor.Reset();
        Monitor.Log($"Window drawing failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    private void OnInventoryTransferFailed(Exception ex)
    {
        osHudActive.Value = false;
        hardwareCursor.Reset();
        windowManager.Value.InvalidateLayout();
        Monitor.Log($"Inventory transfer failed; restored the vanilla HUD.\n{ex}", LogLevel.Error);
    }

    private bool UseTextVitals => vitalsPatchInstalled && UseCompactToolbar
        && Game1.onScreenMenus.OfType<Toolbar>().Any();

}
