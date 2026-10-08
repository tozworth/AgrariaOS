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
    /// <summary>Ready output, spawned pickups, hand-harvest crops and unpetted animals.</summary>
    internal static InteractRequest? TryResolveCollection(GameLocation location, Farmer farmer, Point clicked)
    {
        if (!location.isTileOnMap(clicked))
            return null;
        Vector2 key = new(clicked.X, clicked.Y);
        if (!location.Objects.ContainsKey(key) && TryResolvePetting(location, farmer, clicked) is { } petting)
            return petting;
        if (location.Objects.TryGetValue(key, out var obj))
        {
            Rectangle box = obj.GetBoundingBox();
            bool Placed() => Game1.currentLocation == location
                && location.Objects.TryGetValue(key, out var live) && ReferenceEquals(live, obj)
                && obj.GetBoundingBox() == box && !obj.isTemporarilyInvisible;
            if (obj is IndoorPot pot)
            {
                if (pot.heldObject.Value != null)
                {
                    bool Valid() => Placed() && pot.heldObject.Value != null;
                    void Perform(Farmer who)
                    {
                        if (!Valid() || !InCollectionReach(who, clicked))
                            return;
                        pot.checkForAction(who);
                    }
                    return Placed() ? ForStatic(location, farmer, clicked, Valid, Perform) : null;
                }
                HoeDirt? soil = pot.hoeDirt.Value;
                return soil == null ? null : ForCrop(clicked, soil,
                    () => Placed() && ReferenceEquals(pot.hoeDirt.Value, soil));
            }
            if (obj is not Chest && obj is not Furniture && obj.IsSpawnedObject
                && obj.Type is not ("Crafting" or "interactive"))
            {
                bool Valid() => Placed() && obj.IsSpawnedObject && obj.Type is not ("Crafting" or "interactive")
                    && (!obj.questItem.Value || string.IsNullOrEmpty(obj.questId.Value)
                        || obj.questId.Value == "0" || farmer.hasQuest(obj.questId.Value));
                void Perform(Farmer who)
                {
                    if (!Valid() || !InCollectionReach(who, clicked))
                        return;
                    // The location owns forage quality, Gatherer bonuses, quest rules,
                    // barn/coop XP, inventory checks and removal of the spawned item.
                    location.checkAction(new xTile.Dimensions.Location(clicked.X, clicked.Y), Game1.viewport, who);
                }
                return Valid() ? ForCollectionTile(clicked, Valid, Perform) : null;
            }
            return TryResolveMachineOutput(location, farmer, clicked);
        }
        if (location.terrainFeatures.TryGetValue(key, out var feature) && feature is HoeDirt dirt)
            return ForCrop(clicked, dirt,
                () => Game1.currentLocation == location && !location.Objects.ContainsKey(key)
                    && location.terrainFeatures.TryGetValue(key, out var live) && ReferenceEquals(live, dirt));
        return null;
    }

    private static InteractRequest? ForCrop(Point clicked, HoeDirt dirt,
        Func<bool> placed)
    {
        Crop? crop = dirt.crop;
        bool Valid() => placed() && crop != null && ReferenceEquals(dirt.crop, crop)
            && dirt.readyForHarvest() && crop.GetHarvestMethod() == HarvestMethod.Grab;
        if (!Valid())
            return null;
        void Perform(Farmer who)
        {
            if (!Valid() || !InCollectionReach(who, clicked))
                return;
            // Native crop harvesting preserves quality, XP, regrowth and inventory limits.
            // Calling the dirt directly also avoids planting a carried seed on arrival.
            dirt.performUseAction(new Vector2(clicked.X, clicked.Y));
        }
        return ForCollectionTile(clicked, Valid, Perform);
    }

    private static bool InCollectionReach(Farmer who, Point tile)
    {
        if (!Utility.tileWithinRadiusOfPlayer(tile.X, tile.Y, 1, who))
            return false;
        who.faceDirection(who.getGeneralDirectionTowards(new Vector2(tile.X * 64 + 32, tile.Y * 64 + 32)));
        return true;
    }

    private static InteractRequest ForCollectionTile(Point tile, Func<bool> valid, Action<Farmer> perform)
        => new(new ApproachTarget(new Rectangle(tile.X, tile.Y, 1, 1), valid), () => tile, valid, perform);

}
