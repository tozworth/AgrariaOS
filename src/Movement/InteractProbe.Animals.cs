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
    /// <summary>Pet the selected live animal once, never open its inspection menu.</summary>
    internal static InteractRequest? TryResolvePetting(GameLocation location, Farmer farmer, Point clicked)
    {
        Rectangle cursorTile = new(clicked.X * 64, clicked.Y * 64, 64, 64);
        foreach (FarmAnimal animal in location.animals.Values)
        {
            if (CanPet(location, farmer, animal) && animal.GetCursorPetBoundingBox().Intersects(cursorTile)
                && TryResolvePetting(location, farmer, animal) is { } request)
                return request;
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
            return ForAnimal(pet, Tile, Valid, Perform) with { Kind = InteractionKind.Pet };
        }
        return null;
    }

    internal static InteractRequest? TryResolvePetting(GameLocation location, Farmer farmer, FarmAnimal animal)
    {
        bool Valid() => CanPet(location, farmer, animal);
        if (!Valid())
            return null;
        Point Tile() => animal.TilePoint;
        void Perform(Farmer who)
        {
            if (who != farmer || !Valid() || who.FarmerSprite.PauseForSingleAnimation || !InCollectionReach(who, Tile()))
                return;
            // Native petting owns hearts, friendship, happiness, profession bonuses and XP.
            animal.pet(who);
        }
        return ForAnimal(animal, Tile, Valid, Perform) with { Kind = InteractionKind.Pet };
    }

    internal static bool CanPet(GameLocation location, Farmer farmer, FarmAnimal animal)
        => Game1.currentLocation == location && !animal.wasPet.Value
            && location.animals.Values.Any(live => ReferenceEquals(live, animal))
            && farmer.ActiveObject?.QualifiedItemId != "(O)GoldenAnimalCracker"
            && (Game1.timeOfDay < 1900 || animal.isMoving());

    /// <summary>Harvest the selected animal once with the explicitly equipped pail/shears.</summary>
    internal static InteractRequest? TryResolveAnimalHarvest(GameLocation location, Farmer farmer, Point clicked)
    {
        if (!location.isTileOnMap(clicked) || farmer.CurrentTool is not (MilkPail or Shears))
            return null;
        Tool tool = farmer.CurrentTool;
        Rectangle cursorTile = new(clicked.X * 64, clicked.Y * 64, 64, 64);
        foreach (FarmAnimal animal in location.animals.Values)
        {
            bool Valid() => Game1.currentLocation == location && farmer.Items.Contains(tool)
                && location.animals.Values.Any(live => ReferenceEquals(live, animal))
                && animal.isAdult() && animal.currentProduce.Value != null && animal.CanGetProduceWithTool(tool)
                && (Game1.timeOfDay < 1900 || animal.isMoving());
            if (!Valid() || !animal.GetCursorPetBoundingBox().Intersects(cursorTile))
                continue;
            Point Tile()
            {
                Point center = animal.GetHarvestBoundingBox().Center;
                return new Point((int)MathF.Floor(center.X / 64f), (int)MathF.Floor(center.Y / 64f));
            }
            bool InReach(Farmer who)
            {
                Point live = Tile();
                return who.TilePoint != live && Utility.tileWithinRadiusOfPlayer(live.X, live.Y, 1, who);
            }
            void Perform(Farmer who)
            {
                if (!Valid() || !InReach(who) || who.UsingTool || who.FarmerSprite.PauseForSingleAnimation)
                    return;
                who.CurrentToolIndex = who.Items.IndexOf(tool);
                Rectangle box = animal.GetHarvestBoundingBox();
                // Try the center and edges: overlapping livestock must never cause the
                // native pail/shears selector to harvest a different queued animal.
                Point[] aims = { box.Center, new(box.Left, box.Center.Y), new(box.Right - 1, box.Center.Y),
                    new(box.Center.X, box.Top), new(box.Center.X, box.Bottom - 1) };
                foreach (Point aim in aims)
                {
                    Vector2 pixel = new(aim.X, aim.Y);
                    who.faceDirection(who.getGeneralDirectionTowards(pixel));
                    who.lastClick = pixel;
                    Vector2 hit = who.GetToolLocation();
                    if (!ReferenceEquals(Utility.GetBestHarvestableFarmAnimal(location.animals.Values, tool,
                            new Rectangle((int)hit.X - 32, (int)hit.Y - 32, 64, 64)), animal))
                        continue;
                    who.toolPower.Value = 0;
                    who.toolHold.Value = 0;
                    // Native net events and animation own produce, quality, crackers,
                    // inventory limits, stamina, friendship, XP and completion.
                    who.BeginUsingTool();
                    return;
                }
            }
            return ForAnimal(animal, Tile, Valid, Perform) with
            {
                Kind = tool is MilkPail ? InteractionKind.Milk : InteractionKind.Shear,
                Tool = tool,
                InReach = InReach
            };
        }
        return null;
    }

    private static InteractRequest ForAnimal(object identity, Func<Point> tile, Func<bool> valid, Action<Farmer> perform)
    {
        Rectangle Bounds()
        {
            Point live = tile();
            return new Rectangle(live.X, live.Y, 1, 1);
        }
        return new InteractRequest(new ApproachTarget(Bounds(), valid, Bounds), tile, valid, perform) { Identity = identity };
    }

}
