using AgrariaOS.Interactions;
using AgrariaOS.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private sealed record LookupReplay(MouseState Mouse, MouseState OldMouse);
    private readonly PerScreen<LookupReplay?> lookupReplay = new(() => null);

    private void InitializeLookupAnything()
    {
        try
        {
            lookupAnything.Initialize();
            if (!lookupAnything.Available)
                return;
            // A world popup moves the mouse. Replay vanilla's action at the original
            // cursor only for its Interact continuation; physical input stays untouched.
            foreach ((string method, string prefix) in new[]
            {
                (nameof(Game1.getMouseX), nameof(LookupMouseXPrefix)),
                (nameof(Game1.getMouseY), nameof(LookupMouseYPrefix)),
                (nameof(Game1.getOldMouseX), nameof(LookupOldMouseXPrefix)),
                (nameof(Game1.getOldMouseY), nameof(LookupOldMouseYPrefix)),
                (nameof(Game1.didPlayerJustRightClick), nameof(LookupRightClickPrefix))
            })
                lookupAnything.Harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), method, new[] { typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(ModEntry), prefix));
        }
        catch (Exception ex) { lookupAnything.Disable(ex); }
    }

    private bool TryShowLookupWorldMenu(KeyboardState keyboard, MouseState mouse, GamePadState pad)
    {
        if (!lookupAnything.Available || !CanRouteWorldInteraction || lookupReplay.Value != null
            || Game1.IsChatting || Game1.dialogueUp)
            return false;
        object? subject = lookupAnything.GetWorldSubject();
        if (subject == null)
            return false;
        // Registered action providers own menu priority, including map-tile actions.
        // Lookup Anything doesn't need a list of native interaction types.
        if (worldContextMenus.OwnsTarget(new WorldMenuTarget(Game1.currentLocation,
                lookupAnything.GetSubjectEntity(subject), Game1.currentCursorTile)))
            return false;

        Farmer farmer = Game1.player;
        GameLocation location = Game1.currentLocation;
        Point tile = farmer.TilePoint;
        uint day = Game1.stats.DaysPlayed;
        Item? held = farmer.CurrentItem;
        xTile.Dimensions.Rectangle viewport = Game1.viewport;
        Vector2 cursorTile = Game1.currentCursorTile;
        MouseState oldMouse = Game1.oldMouseState;
        bool visibleCursor = Game1.wasMouseVisibleThisFrame;
        float transparency = Game1.mouseCursorTransparency;
        bool IsCurrent() => UsePelicom && pelicom.Value.IsInteractive && Context.IsPlayerFree
            && Game1.player == farmer && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && farmer.TilePoint == tile && ReferenceEquals(farmer.CurrentItem, held) && Game1.viewport.Equals(viewport);

        void Interact()
        {
            if (!IsCurrent())
                return;
            MouseState previousMouse = Game1.oldMouseState;
            KeyboardState previousKeyboard = Game1.oldKBState;
            GamePadState previousPad = Game1.oldPadState;
            Vector2 previousTile = Game1.currentCursorTile;
            bool previousVisible = Game1.wasMouseVisibleThisFrame;
            float previousTransparency = Game1.mouseCursorTransparency;
            lookupReplay.Value = new LookupReplay(mouse, oldMouse);
            try
            {
                Game1.oldMouseState = oldMouse;
                Game1.currentCursorTile = cursorTile;
                Game1.wasMouseVisibleThisFrame = visibleCursor;
                Game1.mouseCursorTransparency = transparency;
                Game1.pressActionButton(keyboard, mouse, pad);
            }
            catch (Exception ex) { OnWorldInteractionFailed(ex); }
            finally
            {
                lookupReplay.Value = null;
                Game1.oldMouseState = previousMouse;
                Game1.oldKBState = previousKeyboard;
                Game1.oldPadState = previousPad;
                Game1.currentCursorTile = previousTile;
                Game1.wasMouseVisibleThisFrame = previousVisible;
                Game1.mouseCursorTransparency = previousTransparency;
            }
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        bool shown = windowManager.Value.ShowContextMenu(cursor,
            new[] { new ContextMenuItem("Interact", Interact) }, isValid: IsCurrent,
            info: new ContextMenuInfo(() => subject, IsCurrent));
        if (shown)
            Helper.Input.Suppress(SButton.MouseRight);
        return shown;
    }

    private static bool ReplayCoordinate(bool uiScale, int? raw, ref int result)
    {
        if (!raw.HasValue)
            return true;
        result = uiScale ? (int)(raw.Value / Game1.options.uiScale) : (int)(raw.Value * (1f / Game1.options.zoomLevel));
        return false;
    }

    private static bool LookupMouseXPrefix(bool ui_scale, ref int __result)
        => ReplayCoordinate(ui_scale, Instance.lookupReplay.Value?.Mouse.X, ref __result);
    private static bool LookupMouseYPrefix(bool ui_scale, ref int __result)
        => ReplayCoordinate(ui_scale, Instance.lookupReplay.Value?.Mouse.Y, ref __result);
    private static bool LookupOldMouseXPrefix(bool ui_scale, ref int __result)
        => ReplayCoordinate(ui_scale, Instance.lookupReplay.Value?.OldMouse.X, ref __result);
    private static bool LookupOldMouseYPrefix(bool ui_scale, ref int __result)
        => ReplayCoordinate(ui_scale, Instance.lookupReplay.Value?.OldMouse.Y, ref __result);
    private static bool LookupRightClickPrefix(ref bool __result)
    {
        if (Instance.lookupReplay.Value == null)
            return true;
        __result = true;
        return false;
    }
}
