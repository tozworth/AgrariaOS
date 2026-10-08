using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using StardewValley.Characters;

namespace AgrariaOS.Movement;

partial class ClickToMove
{
    private static bool SwingOnce(Farmer who, ToolTarget action, Point hit)
    {
        try
        {
            if (!who.Items.Contains(action.Tool))
                return false;
            who.CurrentToolIndex = who.Items.IndexOf(action.Tool);
            Vector2 pixel = new(hit.X * 64 + 32, hit.Y * 64 + 32);
            who.faceDirection(who.getGeneralDirectionTowards(pixel));
            who.lastClick = pixel;
            who.toolPower.Value = 0;
            who.toolHold.Value = 0;
            // Use the game's animation/net-event path, preserving stamina, upgrades and drops.
            who.BeginUsingTool();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateToolRepeat(bool canMove)
    {
        Farmer who = Game1.player;
        if (location != Game1.currentLocation || day != Game1.stats.DaysPlayed)
        {
            LastDebug = "Repeat cleared: location/day changed";
            Cancel();
            return;
        }
        if (who.controller != null && who.controller != controller)
        {
            LastDebug = "Repeat cleared: foreign controller";
            Cancel();
            return;
        }
        // Wait for the previous swing's release before starting the next one.
        if (toolFarmer != null)
            return;
        if (who.UsingTool)
            return;
        if (!canMove)
            return;
        ToolTarget? tool = repeatTool;
        if (tool == null)
            return;
        bool valid;
        try { valid = tool.IsValid() && who.Items.Contains(tool.Tool); }
        catch { valid = false; }
        if (!valid || repeatSwings >= MaxSwingsPerTool)
        {
            LastDebug = $"Repeat done: valid={valid} swings={repeatSwings}";
            repeatTool = null;
            farmer = null;
            location = null;
            return;
        }
        Point hit;
        try { hit = tool.HitTile(who.TilePoint); }
        catch
        {
            repeatTool = null;
            farmer = null;
            location = null;
            return;
        }
        if (!tool.CanReach(who))
        {
            repeatTool = null;
            farmer = null;
            location = null;
            return;
        }
        if (SwingOnce(who, tool, hit))
        {
            toolFarmer = who;
            startedTool = tool.Tool;
            releaseTicks = 0;
            repeatSwings++;
        }
        else
        {
            repeatTool = null;
        }
    }

    private void ReleaseTool()
    {
        if (toolFarmer == null)
            return;
        releaseTicks++;
        if (toolFarmer == Game1.player && toolFarmer.CurrentTool == startedTool && toolFarmer.UsingTool)
        {
            // Livestock tools finish through their own animation and finish net event.
            // Keep ownership/identity throughout so drag selection can append safely.
            if (startedTool is MilkPail or Shears)
                return;
            // Upgraded hoes/cans normally wait for a mouse release; this is one uncharged use.
            if (toolFarmer.canReleaseTool)
                toolFarmer.EndUsingTool();
        }
        else if (releaseTicks < 3 && toolFarmer == Game1.player && toolFarmer.CurrentTool == startedTool)
            return;
        toolFarmer = null;
        startedTool = null;
        toolRequest = null;
    }

}
