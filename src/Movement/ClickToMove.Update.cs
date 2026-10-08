using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using StardewValley.Characters;

namespace AgrariaOS.Movement;

partial class ClickToMove
{
    internal void BeforeUpdate(bool canMove, bool manualMovement)
    {
        if (manualMovement && IsBusy)
        {
            Cancel();
            return;
        }
        if (controller == null && !arrived)
            return;
        if (farmer != Game1.player || location != Game1.currentLocation || day != Game1.stats.DaysPlayed
            || farmer.controller != null && farmer.controller != controller)
        {
            Cancel();
            return;
        }
        // A warp fade restarts every time the farmer touches the trigger: release our
        // controller at once so vanilla can finish fading, otherwise the exit stalls for
        // seconds of replans (and warp spam) instead of warping instantly.
        if (Game1.locationRequest != null || Game1.fadeToBlack)
        {
            CancelActive();
            return;
        }
        // Pause for menus/dialogues/desktop focus: keep the controller so the walk
        // (and queue) resumes afterwards instead of dropping the current rock/NPC.
        if (!canMove)
            return;
        try
        {
            if (target?.IsValid() == false || interactValid?.Invoke() == false
                || (warpDestination is { } exit && WarpDestination.Resolve(location!, farmer, exit) != exit))
                CancelActive();
        }
        catch
        {
            CancelActive();
        }
    }

    internal void CancelCurrentLeg() => CancelActive();

    internal void AfterUpdate(bool canMove)
    {
        ReleaseTool();
        if (controller == null && !arrived)
        {
            if (repeatTool != null)
            {
                UpdateToolRepeat(canMove);
                return;
            }
            if (arrivedInteractTile != null)
                return;
            if (pending.Count == 0)
                return;
            // Don't start the next leg while a menu/dialogue/tool owns the player.
            if (!canMove || Game1.player.UsingTool || toolFarmer != null)
                return;
            if (farmer != null && (farmer != Game1.player || location != Game1.currentLocation || day != Game1.stats.DaysPlayed))
            {
                Cancel();
                return;
            }
            if (Game1.player.controller != null && Game1.player.controller != controller)
            {
                Cancel();
                return;
            }
            if (!StartNextQueued() && pending.Count == 0 && controller == null && repeatTool == null)
            {
                // All remaining legs were stale or unreachable; stay silent unless something was queued.
            }
            return;
        }
        if (farmer != Game1.player || location != Game1.currentLocation || day != Game1.stats.DaysPlayed)
        {
            Cancel();
            return;
        }
        if (farmer!.controller != null && farmer.controller != controller)
        {
            Cancel();
            return;
        }
        // Same warp-fade release as BeforeUpdate: never drive into a trigger mid-fade.
        if (Game1.locationRequest != null || Game1.fadeToBlack)
        {
            CancelActive();
            return;
        }
        // Pause (menu/dialogue/desktop/focus lost): keep the controller and skip stall
        // buildup so the walk resumes instead of replanning away the queue.
        if (!canMove)
            return;
        if (arrived)
        {
            Farmer who = farmer!;
            // Interaction arrivals retain their selected action. Right-click actions
            // go through ModEntry's routing flag; livestock tools start here and wait
            // for their native animation. Moving targets get bounded chase replans.
            if (interactTile != null && interactPerform != null)
            {
                Func<bool>? valid = interactValid;
                Func<Point>? tileGetter = interactTile;
                Action<Farmer>? perform = interactPerform;
                bool stillValid;
                try { stillValid = valid?.Invoke() != false; }
                catch { stillValid = false; }
                if (!stillValid)
                {
                    CancelActive();
                    return;
                }
                Point want;
                try { want = tileGetter!(); }
                catch
                {
                    CancelActive();
                    return;
                }
                if (!(interactRequest?.InReach?.Invoke(who)
                        ?? Utility.tileWithinRadiusOfPlayer(want.X, want.Y, 1, who)))
                {
                    if (++replans > 3)
                    {
                        CancelActive();
                        Game1.playSound("cancel");
                    }
                    else if (target?.LiveTiles != null)
                    {
                        // Keep the exact selected animal and its action callbacks as it moves.
                        // Resolving a new generic right-click here could inspect an animal
                        // or select a different animal sharing the new tile.
                        destination = want;
                        if (!Plan())
                        {
                            CancelActive();
                            Game1.playSound("cancel");
                        }
                    }
                    else if (InteractProbe.TryResolve(location!, who, want) is { } chase)
                    {
                        target = chase.Move;
                        destination = want;
                        interactTile = chase.Tile;
                        interactValid = chase.Valid;
                        interactPerform = chase.Perform;
                        interactRequest = chase;
                        if (!Plan())
                        {
                            CancelActive();
                            Game1.playSound("cancel");
                        }
                    }
                    else
                    {
                        CancelActive();
                        Game1.playSound("cancel");
                    }
                    return;
                }
                InteractRequest? request = interactRequest;
                CancelActive();
                if (request?.Tool is { } harvestTool)
                {
                    perform(who);
                    toolFarmer = who;
                    startedTool = harvestTool;
                    toolRequest = request;
                    releaseTicks = 0;
                    return;
                }
                arrivedInteractTile = want;
                arrivedInteractPerform = perform;
                arrivedInteractRequest = request;
                return;
            }
            ApproachTarget? approached = target;
            ToolTarget? action = target as ToolTarget;
            bool approachValid;
            try { approachValid = approached?.IsValid() == true; }
            catch { approachValid = false; }
            CancelActive();
            if (approachValid && approached != null)
            {
                try
                {
                    Point hit = approached.HitTile(who.TilePoint);
                    who.faceDirection(who.getGeneralDirectionTowards(new Vector2(hit.X * 64 + 32, hit.Y * 64 + 32)));
                }
                catch { }
            }
            bool toolValid;
            try { toolValid = action?.IsValid() == true && who.Items.Contains(action.Tool); }
            catch { toolValid = false; }
            if (toolValid && action != null)
            {
                Point hit;
                try { hit = action.HitTile(who.TilePoint); }
                catch { return; }
                if (!action.CanReach(who))
                    return;
                // Water refills stay single-use: the tile remains refillable after filling.
                bool singleUse = action.Tool is WateringCan
                    && Game1.currentLocation.CanRefillWateringCanOnTile(hit.X, hit.Y);
                if (SwingOnce(who, action, hit))
                {
                    toolFarmer = who;
                    startedTool = action.Tool;
                    releaseTicks = 0;
                    if (!singleUse)
                    {
                        // Keep the session alive across swings: UpdateToolRepeat validates
                        // against these (CancelActive above cleared them).
                        farmer = who;
                        location = Game1.currentLocation;
                        day = Game1.stats.DaysPlayed;
                        repeatTool = action;
                        repeatSwings = 1;
                        LastDebug = $"Repeat start tool={action.Tool?.Name} target={hit}";
                    }
                }
            }
            return;
        }
        if (farmer!.controller != controller)
        {
            CancelActive();
            return;
        }
        if (Vector2.DistanceSquared(previousPosition, farmer.Position) < 0.01f)
            stalledMilliseconds += Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
        else
            stalledMilliseconds = 0;
        previousPosition = farmer.Position;
        // Recompute around moving NPCs or newly placed obstacles; never wait indefinitely.
        if (stalledMilliseconds >= 600 && (++replans > 3 || !Plan()))
        {
            CancelActive();
            Game1.playSound("cancel");
        }
    }

    internal bool TryConsumeInteractArrival(out Point tile, out Action<Farmer> perform)
    {
        if (arrivedInteractTile is { } want && arrivedInteractPerform is { } action)
        {
            tile = want;
            perform = action;
            arrivedInteractTile = null;
            arrivedInteractPerform = null;
            arrivedInteractRequest = null;
            return true;
        }
        tile = default;
        perform = _ => { };
        return false;
    }

    internal bool IsToolActive => repeatTool != null || toolFarmer != null;

    private void CancelActive()
    {
        if (controller != null && farmer?.controller == controller)
        {
            farmer.controller = null;
            farmer.Halt();
        }
        farmer = null;
        location = null;
        controller = null;
        target = null;
        warpDestination = null;
        arrived = false;
        stalledMilliseconds = 0;
        replans = 0;
        interactTile = null;
        interactValid = null;
        interactPerform = null;
        interactRequest = null;
        plannedInteractionTile = null;
    }

    internal void Cancel()
    {
        CancelActive();
        pending.Clear();
        repeatTool = null;
        repeatSwings = 0;
        arrivedInteractTile = null;
        arrivedInteractPerform = null;
        arrivedInteractRequest = null;
    }

}
