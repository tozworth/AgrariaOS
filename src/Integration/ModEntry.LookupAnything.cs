using AgrariaOS.Interactions;
using AgrariaOS.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Reflection.Emit;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private void InitializeLookupAnything()
    {
        try
        {
            lookupAnything.Initialize();
            if (!lookupAnything.Available)
                return;
            lookupAnything.Harmony.Patch(AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.pressActionButton)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(LookupWorldActionTranspiler)));
        }
        catch (Exception ex) { lookupAnything.Disable(ex); }
    }

    private static IEnumerable<CodeInstruction> LookupWorldActionTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        // Only the final fallthrough means no action was handled. Earlier true returns
        // include animal petting and item placement, so a result-only postfix is unsafe.
        if (codes.Count < 2 || codes[^2].opcode != OpCodes.Ldc_I4_1 || codes[^1].opcode != OpCodes.Ret)
            throw new InvalidOperationException("The vanilla unhandled world-action return wasn't recognized.");
        // Mutate in place to preserve branch labels on the original fallthrough.
        codes[^2].opcode = OpCodes.Call;
        codes[^2].operand = AccessTools.Method(typeof(ModEntry), nameof(LookupUnhandledWorldAction));
        return codes;
    }

    private static bool LookupUnhandledWorldAction() => !Instance.TryShowLookupWorldMenu();

    private bool TryShowLookupWorldMenu()
    {
        if (!lookupAnything.Available || !CanRouteWorldInteraction
            || Game1.IsChatting || Game1.dialogueUp)
            return false;
        object? subject = lookupAnything.GetWorldSubject();
        if (subject == null)
            return false;
        // Leave registered choice providers in charge even if their action is unavailable.
        if (worldContextMenus.OwnsTarget(new WorldMenuTarget(Game1.currentLocation,
                lookupAnything.GetSubjectEntity(subject), Game1.currentCursorTile)))
            return false;

        Farmer farmer = Game1.player;
        GameLocation location = Game1.currentLocation;
        Point tile = farmer.TilePoint;
        uint day = Game1.stats.DaysPlayed;
        Item? held = farmer.CurrentItem;
        xTile.Dimensions.Rectangle viewport = Game1.viewport;
        bool IsCurrent() => UsePelicom && pelicom.Value.IsInteractive && Context.IsPlayerFree
            && Game1.player == farmer && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && farmer.TilePoint == tile && ReferenceEquals(farmer.CurrentItem, held) && Game1.viewport.Equals(viewport);

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        bool shown = windowManager.Value.ShowContextMenu(cursor,
            Array.Empty<ContextMenuItem>(), isValid: IsCurrent,
            info: new ContextMenuInfo(() => subject, IsCurrent));
        if (shown)
            Helper.Input.Suppress(SButton.MouseRight);
        return shown;
    }
}
