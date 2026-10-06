using System.Globalization;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace AgrariaOS.Hud;

/// <summary>Entirely fictional CONCORD jurisdiction. Changes no game mechanics.</summary>
internal static class SecurityStatus
{
    public static float GetRating(GameLocation location)
    {
        if (location is MineShaft mine)
        {
            // Quarry uses the special level 77377, so check the mine area before subtracting 120.
            if (mine.getMineArea() == MineShaft.desertArea)
                return SkullRating(mine.mineLevel - 120);
            if (mine.getMineArea() == MineShaft.quarryMineShaft)
                return 0.1f;
            return mine.mineLevel <= 40 ? 0.4f : mine.mineLevel <= 80 ? 0.3f : 0.2f;
        }

        string name = location.Name;
        if (name == "SkullCave")
            return 0f;
        if (name == "FarmCave")
            return 0.4f;
        if (location is VolcanoDungeon || name is "VolcanoEntrance" or "BugLand")
            return 0.1f;
        if (name is "Mine" or "Sewer" or "WitchSwamp" or "WitchHut")
            return 0.3f;

        // Includes player cabins and farm building interiors with a farm parent.
        if (location.IsFarm || location is FarmHouse || location.GetRootLocation().IsFarm || name == "Greenhouse")
            return 1f;

        return name switch
        {
            "SeedShop" or "Hospital" or "CommunityCenter" => 1f,
            "Town" or "Blacksmith" or "ScienceHouse" or "ArchaeologyHouse" or "JojaMart" => 0.9f,
            "Saloon" or "JoshHouse" or "HaleyHouse" or "SamHouse" or "ManorHouse" or "Trailer" => 0.8f,
            "Beach" or "BeachNightMarket" or "FishShop" or "WizardHouse" or "AdventureGuild" => 0.7f,
            "Forest" or "Mountain" or "Railroad" or "BusStop" or "Backwoods" => 0.6f,
            "Desert" or "SandyHouse" => 0.5f,
            "Woods" => 0.4f,
            _ when name.Contains("Cave", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tunnel", StringComparison.OrdinalIgnoreCase) => 0.3f,
            _ => 0.7f
        };
    }

    internal static float SkullRating(int floor) => -Math.Clamp(floor, 0, 100) / 100f;

    public static string Format(float rating)
    {
        float rounded = MathF.Round(rating, 1, MidpointRounding.AwayFromZero);
        // Early Skull floors still display 0.0 rather than the confusing "-0.0".
        return (rounded == 0 ? 0 : rounded).ToString("0.0", CultureInfo.InvariantCulture);
    }

    public static Color GetColor(float rating)
    {
        rating = Math.Clamp(rating, -1f, 1f);
        Color cyan = new(70, 235, 245);
        Color green = new(100, 225, 100);
        Color yellow = new(245, 230, 70);
        Color orange = new(245, 145, 50);
        Color red = new(235, 60, 60);
        Color purple = new(165, 80, 235);
        if (rating < 0) return Color.Lerp(red, purple, -rating);
        if (rating < 0.3f) return Color.Lerp(red, orange, rating / 0.3f);
        if (rating < 0.5f) return Color.Lerp(orange, yellow, (rating - 0.3f) / 0.2f);
        if (rating < 0.8f) return Color.Lerp(yellow, green, (rating - 0.5f) / 0.3f);
        return Color.Lerp(green, cyan, (rating - 0.8f) / 0.2f);
    }
}
