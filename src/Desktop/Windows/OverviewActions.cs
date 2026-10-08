using AgrariaOS.Configuration;
using AgrariaOS.Integration;
using AgrariaOS.Interactions;
using AgrariaOS.Movement;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System.Runtime.CompilerServices;

namespace AgrariaOS.Desktop.Windows;

partial class OverviewWindow : OsWindow
{
    private static bool IsControlHeld()
    {
        try
        {
            KeyboardState keys = Game1.GetKeyboardState();
            return keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        }
        catch { return false; }
    }

    private static bool IsShiftHeld()
    {
        try
        {
            KeyboardState keys = Game1.GetKeyboardState();
            return keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        }
        catch { return false; }
    }

    private static bool RowIsValid(OverviewRow row)
    {
        try { return row.IsValid(); }
        catch { return false; }
    }

    private string[] ValidRowKeys() => grid.Rows.Where(RowIsValid).Select(row => row.StableKey).ToArray();

    private void RefreshSelection()
    {
        Farmer? farmer = Context.IsWorldReady ? Game1.player : null;
        uint day = Context.IsWorldReady ? Game1.stats.DaysPlayed : 0;
        if (selectionFarmer != farmer || selectionDay != day)
        {
            selection.Clear();
            selectionFarmer = farmer;
            selectionDay = day;
        }
        selection.Refresh(ValidRowKeys());
    }

    private void QueueInteractions(IReadOnlyList<InteractRequest> requests)
    {
        if (CanAct() && getMovement().EnqueueInteractions(requests))
        {
            try { releaseFocus(); }
            catch { }
        }
    }

    private void QueueInteraction(InteractRequest request)
    {
        if (!CanAct() || !request.Valid())
            return;
        Point tile = request.Tile();
        ClickToMove mover = getMovement();
        if (mover.IsBusy)
            mover.EnqueueInteract(request, tile);
        else
            mover.StartInteract(request, tile);
        try { releaseFocus(); }
        catch { }
    }

    private void Approach(OverviewRow row)
    {
        if (!CanAct())
            return;
        Point tile = row.GetTile();
        ClickToMove mover = getMovement();
        if (mover.IsBusy)
        {
            if (!mover.EnqueueApproach(tile))
                mover.StartApproach(tile);
        }
        else
            mover.StartApproach(tile);
        try { releaseFocus(); }
        catch { }
    }

    private static bool CanAct()
    {
        try
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree || !Context.CanPlayerMove)
                return false;
            if (Game1.eventUp || Game1.currentMinigame != null || Game1.farmEvent != null
                || Game1.paused || Game1.HostPaused || Game1.fadeToBlack || Game1.freezeControls)
                return false;
            Farmer player = Game1.player;
            if (player.UsingTool || player.isEating || player.isRidingHorse() || player.IsSitting()
                || player.swimming.Value || player.isRafting)
                return false;
            return true;
        }
        catch { return false; }
    }

    private InteractRequest? TryProbe(OverviewRow row)
    {
        if (!row.IsValid())
            return null;
        GameLocation loc = Game1.currentLocation;
        Farmer farmer = Game1.player;
        Point tile = row.GetTile();
        return InteractProbe.TryResolve(loc, farmer, tile, includeInRange: true);
    }

}
