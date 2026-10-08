using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using StardewValley.Characters;

namespace AgrariaOS.Movement;

partial class ClickToMove
{
    internal bool Start(Point tile)
    {
        Cancel();
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        // Never replace an event's or another mod's movement controller.
        if (who.controller != null)
        {
            LastDebug = $"Start {tile} blocked: controller owned by {who.controller?.GetType().Name}";
            return false;
        }
        Point? exit = WarpDestination.Resolve(here, who, tile);
        if (!here.isTileOnMap(tile) && exit == null)
        {
            LastDebug = $"Start {tile} off-map, no warp";
            return true;
        }
        farmer = who;
        location = here;
        day = Game1.stats.DaysPlayed;
        warpDestination = exit;
        destination = exit ?? tile;
        // Exit clicks take priority over toolable ground at the edge of the map.
        target = exit == null ? ToolTarget.Resolve(here, who, tile)
            ?? ApproachTarget.ResolveSolid(here, who, tile, CanWalk) : null;
        bool planned = Plan();
        LastDebug = $"Start click={tile} exit={exit} dest={destination} target={target?.GetType().Name ?? "move"} planned={planned} loc={here.Name} playerTile={who.TilePoint}";
        if (!planned)
        {
            Cancel();
            Game1.playSound("cancel");
        }
        return true;
    }

    internal bool StartInteract(InteractRequest request, Point clicked)
    {
        Cancel();
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        // Never replace an event's or another mod's movement controller.
        if (who.controller != null)
            return false;
        if (!here.isTileOnMap(clicked))
            return true;
        farmer = who;
        location = here;
        day = Game1.stats.DaysPlayed;
        warpDestination = null;
        destination = clicked;
        // Stored interactions don't cross map-edge warps; their callbacks perform
        // the selected native action on arrival, including single livestock tool uses.
        target = request.Move;
        interactTile = request.Tile;
        interactValid = request.Valid;
        interactPerform = request.Perform;
        interactRequest = request;
        if (!Plan())
        {
            Cancel();
            Game1.playSound("cancel");
            // Unreachable: fall through to vanilla facing-tile plus Lookup info.
            return false;
        }
        return true;
    }

    /// <summary>Queue a left-click tile without replacing the active route. Warps and
    /// plain ground are not auto-queued; the caller should Start those instead.</summary>
    internal bool TryEnqueueToolOrApproach(Point tile)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null && who.controller != controller)
            return false;
        if (!here.isTileOnMap(tile))
            return false;
        if (WarpDestination.Resolve(here, who, tile) != null)
            return false;
        ApproachTarget? resolved = ToolTarget.Resolve(here, who, tile)
            ?? ApproachTarget.ResolveSolid(here, who, tile, (candidate) => CanWalkFor(here, who, candidate));
        if (resolved == null)
            return false;
        return EnqueueStep(new QueuedStep(tile, resolved, null, null, here, Game1.stats.DaysPlayed));
    }

    /// <summary>Walk to a tile's reachable side without swinging tools. Used by the
    /// overview's approach action (Ctrl-click, double-click, Approach menu).</summary>
    internal bool StartApproach(Point tile)
    {
        Cancel();
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null)
            return false;
        Point? exit = WarpDestination.Resolve(here, who, tile);
        if (!here.isTileOnMap(tile) && exit == null)
            return true;
        farmer = who;
        location = here;
        day = Game1.stats.DaysPlayed;
        warpDestination = exit;
        destination = exit ?? tile;
        target = exit == null ? ApproachTarget.ResolveSolid(here, who, tile, CanWalk) : null;
        if (!Plan())
        {
            Cancel();
            Game1.playSound("cancel");
        }
        return true;
    }

    internal bool EnqueueRock(Point tile, bool startImmediately)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (!here.isTileOnMap(tile) || who.controller != null && who.controller != controller)
            return false;
        ToolTarget? rock = ToolTarget.ResolveRock(here, who, tile);
        if (rock == null)
            return false;
        return HasQueuedTool(rock) || EnqueueStep(
            new QueuedStep(tile, rock, null, null, here, Game1.stats.DaysPlayed), startImmediately);
    }

}
