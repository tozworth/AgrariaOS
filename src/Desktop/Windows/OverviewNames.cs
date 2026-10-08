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
    private static string GetAnimalTypeName(FarmAnimal animal)
    {
        try
        {
            if (!string.IsNullOrEmpty(animal.displayType))
                return animal.displayType;
        }
        catch { }
        try
        {
            if (!string.IsNullOrEmpty(animal.type.Value))
                return animal.type.Value;
        }
        catch { }
        return "Animal";
    }

    private static string GetCropName(HoeDirt dirt)
    {
        try
        {
            Crop? crop = dirt.crop;
            if (crop == null)
                return "Crop";
            string produce = "";
            try
            {
                string id = $"(O){crop.indexOfHarvest.Value}";
                produce = ItemRegistry.GetDataOrErrorItem(id).DisplayName;
            }
            catch { }
            if (string.IsNullOrWhiteSpace(produce))
                produce = "Crop";
            bool dead;
            try { dead = crop.dead.Value; }
            catch { dead = false; }
            bool ready;
            try { ready = dirt.readyForHarvest(); }
            catch { ready = false; }
            return dead ? $"{produce} (Dead)" : ready ? $"{produce} (Ready)" : produce;
        }
        catch { return "Crop"; }
    }

    private static string GetClumpName(ResourceClump clump)
    {
        try
        {
            if (clump is GiantCrop)
                return "Giant Crop";
            int index = clump.parentSheetIndex.Value;
            return index switch
            {
                600 or 602 => "Stump",
                44 or 46 => "Fallen Log",
                622 => "Meteorite",
                672 or 752 or 754 or 756 or 758 or 148 => "Boulder",
                _ => "Resource Clump"
            };
        }
        catch { return "Resource Clump"; }
    }

}
