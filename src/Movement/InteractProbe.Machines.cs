using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace AgrariaOS.Movement;

internal static partial class InteractProbe
{
    /// <summary>Collect ready output only, without dropping held items into a machine.</summary>
    internal static InteractRequest? TryResolveMachineOutput(GameLocation location, Farmer farmer, Point clicked)
    {
        Vector2 key = new(clicked.X, clicked.Y);
        if (!location.isTileOnMap(clicked) || !location.Objects.TryGetValue(key, out var machine)
            || machine is Chest or Furniture
            || !machine.readyForHarvest.Value || machine.heldObject.Value == null
            || (machine is not CrabPot && !machine.IsTapper() && machine.GetMachineData() == null))
            return null;
        Rectangle box = machine.GetBoundingBox();
        bool Valid() => Game1.currentLocation == location
            && location.Objects.TryGetValue(key, out var live) && ReferenceEquals(live, machine)
            && machine.GetBoundingBox() == box && !machine.isTemporarilyInvisible
            && machine.readyForHarvest.Value && machine.heldObject.Value != null;
        if (!Valid() || !machine.checkForAction(farmer, justCheckingForActivity: true))
            return null;
        void Perform(Farmer who)
        {
            if (!Valid() || !Utility.tileWithinRadiusOfPlayer(clicked.X, clicked.Y, 1, who))
                return;
            who.faceDirection(who.getGeneralDirectionTowards(new Vector2(clicked.X * 64 + 32, clicked.Y * 64 + 32)));
            // Native collection owns inventory limits, drops, XP, tapper regeneration,
            // crab-pot animation and hopper reloads. Never use checkAction's drop-in path.
            machine.checkForAction(who);
        }
        return ForStatic(location, farmer, clicked, Valid, Perform);
    }

}
