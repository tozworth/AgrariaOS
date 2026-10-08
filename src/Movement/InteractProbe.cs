using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace AgrariaOS.Movement;

/// <summary>An out-of-range right-click target: how to walk there and what to do on arrival.</summary>
internal sealed record InteractRequest(ApproachTarget? Move, Func<Point> Tile, Func<bool> Valid, Action<Farmer> Perform)
{
    internal object? Identity { get; init; }
}

/// <summary>Finds right-clickable targets without triggering side effects.</summary>
internal static class InteractProbe
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

    /// <summary>Pet the selected live animal once, never open its inspection menu.</summary>
    internal static InteractRequest? TryResolvePetting(GameLocation location, Farmer farmer, Point clicked)
    {
        Rectangle cursorTile = new(clicked.X * 64, clicked.Y * 64, 64, 64);
        foreach (FarmAnimal animal in location.animals.Values)
        {
            bool Valid() => Game1.currentLocation == location && !animal.wasPet.Value
                && location.animals.Values.Any(live => ReferenceEquals(live, animal))
                && farmer.ActiveObject?.QualifiedItemId != "(O)GoldenAnimalCracker"
                && (Game1.timeOfDay < 1900 || animal.isMoving());
            if (!Valid() || !animal.GetCursorPetBoundingBox().Intersects(cursorTile))
                continue;
            Point Tile() => animal.TilePoint;
            void Perform(Farmer who)
            {
                if (!Valid() || who.FarmerSprite.PauseForSingleAnimation || !InCollectionReach(who, Tile()))
                    return;
                // Native petting owns hearts, friendship, happiness, profession bonuses and XP.
                animal.pet(who);
            }
            return ForPetting(animal, Tile, Valid, Perform);
        }
        foreach (Pet pet in location.characters.OfType<Pet>())
        {
            bool Valid() => Game1.currentLocation == location && !pet.IsInvisible
                && location.characters.Contains(pet)
                && (!pet.lastPetDay.TryGetValue(farmer.UniqueMultiplayerID, out int lastDay) || lastDay != Game1.Date.TotalDays)
                && farmer.CurrentItem is not Hat && farmer.CurrentItem?.QualifiedItemId != "(O)ButterflyPowder";
            if (!Valid() || !pet.GetBoundingBox().Intersects(cursorTile))
                continue;
            Point Tile() => pet.TilePoint;
            void Perform(Farmer who)
            {
                if (Valid() && !who.FarmerSprite.PauseForSingleAnimation && InCollectionReach(who, Tile()))
                    pet.checkAction(who, location);
            }
            return ForPetting(pet, Tile, Valid, Perform);
        }
        return null;
    }

    private static InteractRequest ForPetting(object identity, Func<Point> tile, Func<bool> valid, Action<Farmer> perform)
    {
        Rectangle Bounds()
        {
            Point live = tile();
            return new Rectangle(live.X, live.Y, 1, 1);
        }
        return new InteractRequest(new ApproachTarget(Bounds(), valid, Bounds), tile, valid, perform) { Identity = identity };
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

    // Use the perimeter even for passable crops/pickups. Native checkAction has a
    // standing-on-object tool fallback, and trellis crops need an adjacent harvest.
    private static InteractRequest ForCollectionTile(Point tile, Func<bool> valid, Action<Farmer> perform)
        => new(new ApproachTarget(new Rectangle(tile.X, tile.Y, 1, 1), valid), () => tile, valid, perform);

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

    internal static InteractRequest? TryResolve(GameLocation location, Farmer farmer, Point clicked, bool includeInRange = false)
    {
        // In-range clicks stay with vanilla so facing, horses and HUD priority are unchanged.
        // Overview reuses the same probe for distant and adjacent rows, so it opts in explicitly.
        if (!includeInRange && Utility.tileWithinRadiusOfPlayer(clicked.X, clicked.Y, 1, farmer))
            return null;
        if (!location.isTileOnMap(clicked))
            return null;

        Rectangle pixel = new(clicked.X * 64, clicked.Y * 64, 64, 64);
        Vector2 key = new(clicked.X, clicked.Y);

        // Bus ticket machine and minecarts first: the location override handles them
        // before the base checks below.
        if (location is BusStop bus)
        {
            int index = bus.getTileIndexAt(new xTile.Dimensions.Location(clicked.X, clicked.Y), "Buildings", "outdoors");
            if (index is 1057 or 958 or 1080 or 1081)
            {
                GameLocation clickLoc = location;
                Point want = clicked;
                bool Valid() => Game1.currentLocation == clickLoc && clickLoc is BusStop live
                    && live.getTileIndexAt(new xTile.Dimensions.Location(want.X, want.Y), "Buildings", "outdoors") == index;
                void Perform(Farmer who)
                {
                    if (Game1.currentLocation != clickLoc)
                        return;
                    clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
                }
                return ForStatic(location, farmer, clicked, Valid, Perform);
            }
        }

        // Mine ladders, shafts, elevator and exit: map tiles on the "mine" sheet,
        // not objects. Covers Skull Cavern too (same MineShaft object, level > 120).
        if (location is MineShaft)
        {
            InteractRequest? hit = ForTileIndex(location, farmer, clicked, "Buildings", "mine",
                static index => index is 284 or 112 or 115 or 173 or 174 or 194 or 315 or 316 or 317);
            if (hit != null)
                return hit;
        }

        // Volcano shortcut-out and dwarf shop.
        if (location is VolcanoDungeon)
        {
            InteractRequest? hit = ForTileIndex(location, farmer, clicked, "Buildings", "dungeon",
                static index => index is 367 or 77);
            if (hit != null)
                return hit;
        }

        // Community Center bundles.
        if (location is CommunityCenter)
        {
            InteractRequest? hit = ForTileIndex(location, farmer, clicked, "Buildings", "indoors",
                static index => index is 1799 or (>= 1824 and <= 1833));
            if (hit != null)
                return hit;
        }

        // Sewer stairs to town and Krobus' shop.
        if (location is Sewer)
        {
            InteractRequest? hit = ForTileIndex(location, farmer, clicked, "Buildings", "st",
                static index => index is 21 or 84);
            if (hit != null)
                return hit;
        }

        // Farmhouse fridge is a map tile, not an entry in Objects.
        if (location is FarmHouse house && house.fridgePosition != Point.Zero
            && (clicked == house.fridgePosition || clicked == house.fridgePosition + new Point(0, -1)))
        {
            GameLocation clickLoc = location;
            Point fridge = house.fridgePosition;
            Point want = fridge;
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc is FarmHouse live && live.fridgePosition == fridge;
            void Perform(Farmer who)
            {
                if (Game1.currentLocation != clickLoc)
                    return;
                clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
            }
            return ForStatic(location, farmer, clicked, Valid, Perform);
        }

        // Animals: petting needs no click state, inspecting does. Perform both directly
        // so arrival doesn't depend on a fresh mouse press.
        foreach (FarmAnimal animal in location.animals.Values)
        {
            if (!animal.GetBoundingBox().Intersects(pixel))
                continue;
            FarmAnimal target = animal;
            GameLocation clickLoc = location;
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc.animals.Values.Any(candidate => ReferenceEquals(candidate, target));
            Point Tile() => Valid() ? target.TilePoint : clicked;
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                Point live = target.TilePoint;
                Rectangle rect = new(live.X * 64, live.Y * 64, 64, 64);
                if (clickLoc.CheckPetAnimal(rect, who))
                    return;
                clickLoc.CheckInspectAnimal(rect, who);
            }
            return ForMoving(location, farmer, target.TilePoint, clicked, Valid, Tile, Perform);
        }

        // Buildings (doors, shipping bins, etc.). Approach the clicked tile itself, not
        // the whole footprint, so door clicks route to the door side. Plain walls walk
        // then silently stop when the native action declines.
        if (location.getBuildingAt(key) is { } building)
        {
            Building target = building;
            GameLocation clickLoc = location;
            Point want = clicked;
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc.buildings.Contains(target)
                && ReferenceEquals(clickLoc.getBuildingAt(new Vector2(want.X, want.Y)), target);
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
            }
            var move = new ApproachTarget(new Rectangle(want.X, want.Y, 1, 1), Valid);
            if (!move.InteractTiles().Any(approach => approach == farmer.TilePoint || CanWalk(location, farmer, approach)))
            {
                // Walled in (or unwalkable): fall through to vanilla plus Lookup info.
                if (!CanWalk(location, farmer, want))
                    return null;
                return new InteractRequest(null, () => want, Valid, Perform);
            }
            return new InteractRequest(move, () => want, Valid, Perform);
        }

        // NPCs (non-monsters). Live tile tracks them if they wander during the walk.
        foreach (NPC npc in location.characters)
        {
            if (npc == null || npc.IsMonster || !npc.GetBoundingBox().Intersects(pixel))
                continue;
            NPC target = npc;
            GameLocation clickLoc = location;
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc.characters.Any(candidate => ReferenceEquals(candidate, target));
            Point Tile() => Valid() ? target.TilePoint : clicked;
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                Point live = target.TilePoint;
                clickLoc.checkAction(new xTile.Dimensions.Location(live.X, live.Y), Game1.viewport, who);
            }
            return ForMoving(location, farmer, target.TilePoint, clicked, Valid, Tile, Perform);
        }

        // Objects: machines, chests and forage. Twigs, stones and weeds fail every
        // probe below and stay with lookup instead of walking.
        if (location.Objects.TryGetValue(key, out var obj))
        {
            StardewValley.Object target = obj;
            Vector2 tileKey = key;
            Rectangle box = target.GetBoundingBox();
            bool actionable = false;
            try { actionable = target.checkForAction(farmer, justCheckingForActivity: true); }
            catch { actionable = false; }
            if (!actionable && target.IsSpawnedObject && farmer.couldInventoryAcceptThisItem(target))
                actionable = true;
            if (!actionable && farmer.CurrentItem is { } held)
            {
                try { actionable = target.performObjectDropInAction(held, probe: true, farmer); }
                catch { actionable = false; }
            }
            if (!actionable)
                return null;
            GameLocation clickLoc = location;
            Point want = clicked;
            bool Valid()
            {
                if (Game1.currentLocation != clickLoc)
                    return false;
                if (!clickLoc.Objects.TryGetValue(tileKey, out var live) || !ReferenceEquals(live, target))
                    return false;
                if (target.IsSpawnedObject && !farmer.couldInventoryAcceptThisItem(target))
                    return false;
                return live.GetBoundingBox() == box;
            }
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
            }
            return ForStatic(location, farmer, clicked, Valid, Perform);
        }

        // Ready crops walk to harvest. Growing crops, trees, grass and fruit trees stay
        // with immediate lookup so far inspection keeps working.
        if (location.terrainFeatures.TryGetValue(key, out var feature) && feature is HoeDirt dirt && dirt.readyForHarvest())
        {
            TerrainFeature target = feature;
            Vector2 tileKey = key;
            GameLocation clickLoc = location;
            Point want = clicked;
            bool Valid()
            {
                if (Game1.currentLocation != clickLoc)
                    return false;
                return clickLoc.terrainFeatures.TryGetValue(tileKey, out var live) && ReferenceEquals(live, target)
                    && live is HoeDirt liveDirt && liveDirt.readyForHarvest();
            }
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
            }
            return ForStatic(location, farmer, clicked, Valid, Perform);
        }

        // Berry bushes ready to pick. Other large terrain stays with lookup.
        foreach (LargeTerrainFeature large in location.largeTerrainFeatures)
        {
            if (large is not Bush bush || !bush.getBoundingBox().Intersects(pixel) || !bush.readyForHarvest())
                continue;
            LargeTerrainFeature target = large;
            Rectangle box = target.getBoundingBox();
            GameLocation clickLoc = location;
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc.largeTerrainFeatures.Contains(target)
                && target is Bush live && live.readyForHarvest() && live.getBoundingBox() == box;
            Point Tile()
            {
                if (!Valid())
                    return clicked;
                Rectangle liveBox = target.getBoundingBox();
                return new Point((liveBox.Center.X) / 64, (liveBox.Center.Y) / 64);
            }
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                Point live = Tile();
                clickLoc.checkAction(new xTile.Dimensions.Location(live.X, live.Y), Game1.viewport, who);
            }
            return ForStatic(location, farmer, clicked, Valid, Perform);
        }

        // Map action tiles: doors and other scripted tiles.
        if (location.doesTileHaveProperty(clicked.X, clicked.Y, "Action", "Buildings") != null)
        {
            GameLocation clickLoc = location;
            Point want = clicked;
            string? action = location.doesTileHaveProperty(clicked.X, clicked.Y, "Action", "Buildings");
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc.isTileOnMap(want)
                && clickLoc.doesTileHaveProperty(want.X, want.Y, "Action", "Buildings") == action;
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
            }
            return ForStatic(location, farmer, clicked, Valid, Perform);
        }

        // Benches and other sittable tiles.
        foreach (MapSeat seat in location.mapSeats)
        {
            if (!seat.OccupiesTile(clicked.X, clicked.Y) || seat.IsBlocked(location))
                continue;
            MapSeat target = seat;
            GameLocation clickLoc = location;
            Point want = clicked;
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc.mapSeats.Contains(target) && !target.IsBlocked(clickLoc);
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
            }
            return ForStatic(location, farmer, clicked, Valid, Perform);
        }

        // Furniture always answers the right-click probe. Perform the right-click path
        // directly: generic checkAction would take the left-click branch without a fresh
        // mouse press on arrival.
        foreach (Furniture furniture in location.furniture)
        {
            if (!furniture.boundingBox.Value.Contains(clicked.X * 64, clicked.Y * 64) || furniture.furniture_type.Value == 12)
                continue;
            Furniture target = furniture;
            Rectangle box = target.GetBoundingBox();
            GameLocation clickLoc = location;
            bool Valid() => Game1.currentLocation == clickLoc && clickLoc.furniture.Contains(target)
                && target.GetBoundingBox() == box;
            void Perform(Farmer who)
            {
                if (!Valid() || Game1.currentLocation != clickLoc)
                    return;
                if (who.ActiveObject != null && target.performObjectDropInAction(who.ActiveObject, probe: false, who))
                    return;
                target.checkForAction(who);
            }
            return ForStatic(location, farmer, clicked, Valid, Perform);
        }

        return null;
    }

    /// <summary>Probe a location-override tile index (ladders, shops, exits).</summary>
    private static InteractRequest? ForTileIndex(GameLocation location, Farmer farmer, Point clicked,
        string layer, string sheet, Func<int, bool> matches)
    {
        int index = location.getTileIndexAt(new xTile.Dimensions.Location(clicked.X, clicked.Y), layer, sheet);
        if (!matches(index))
            return null;
        GameLocation clickLoc = location;
        Point want = clicked;
        bool Valid() => Game1.currentLocation == clickLoc
            && clickLoc.getTileIndexAt(new xTile.Dimensions.Location(want.X, want.Y), layer, sheet) == index;
        void Perform(Farmer who)
        {
            if (Game1.currentLocation != clickLoc)
                return;
            clickLoc.checkAction(new xTile.Dimensions.Location(want.X, want.Y), Game1.viewport, who);
        }
        return ForStatic(location, farmer, clicked, Valid, Perform);
    }

    private static InteractRequest ForStatic(GameLocation location, Farmer farmer, Point clicked,
        Func<bool> valid, Action<Farmer> perform)
    {
        ApproachTarget? move = ApproachTarget.ResolveSolid(location, farmer, clicked,
            tile => CanWalk(location, farmer, tile));
        // Solid footprints report the nearest tile so large furniture and the fridge
        // upper tile validate against the side actually reached. Walkable targets are
        // stood upon, so the clicked tile itself is the interaction tile.
        Func<Point> tile = move == null ? () => clicked : () => move.HitTile(farmer.TilePoint);
        return new InteractRequest(move, tile, valid, perform);
    }

    private static InteractRequest ForMoving(GameLocation location, Farmer farmer, Point entityTile, Point clicked,
        Func<bool> valid, Func<Point> tile, Action<Farmer> perform)
    {
        // Entity tiles block pathfinding like NPC hitboxes do; approach the neighbors.
        var move = new ApproachTarget(new Rectangle(entityTile.X, entityTile.Y, 1, 1), valid);
        if (!move.InteractTiles().Any(approach => approach == farmer.TilePoint || CanWalk(location, farmer, approach)))
            return new InteractRequest(null, tile, valid, perform);
        return new InteractRequest(move, tile, valid, perform);
    }

    private static bool CanWalk(GameLocation location, Farmer farmer, Point tile)
    {
        if (!location.isTileOnMap(tile))
            return false;
        Rectangle box = new(tile.X * 64 + 1, tile.Y * 64 + 1, 62, 62);
        if (location.isCollidingPosition(box, Game1.viewport, isFarmer: true, 0, glider: false,
                farmer, pathfinding: true, skipCollisionEffects: true))
            return false;
        return !location.characters.Any(npc => !npc.IsInvisible && !npc.farmerPassesThrough
            && npc.GetBoundingBox().Intersects(box) && !npc.GetBoundingBox().Intersects(farmer.GetBoundingBox()));
    }
}
