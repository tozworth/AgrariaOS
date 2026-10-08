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
    private void EnsureStaticTiles(GameLocation loc)
    {
        if (ReferenceEquals(loc, staticLocation))
            return;
        staticLocation = loc;
        staticTiles = new List<StaticTile>();
        int width, height;
        try
        {
            width = loc.Map.Layers[0].LayerWidth;
            height = loc.Map.Layers[0].LayerHeight;
        }
        catch { return; }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                try
                {
                    string? action = loc.doesTileHaveProperty(x, y, "Action", "Buildings");
                    if (action != null)
                    {
                        string first = action.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Action";
                        if (string.IsNullOrWhiteSpace(first))
                            first = "Action";
                        staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Action, first, "Door", action, 0, "", ""));
                    }
                }
                catch { }
            }
        }
        try
        {
            if (loc is BusStop)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "outdoors"); }
                        catch { continue; }
                        if (index == 1057)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.BusTicket, "Bus Ticket Machine", "Service", null, index, "Buildings", "outdoors"));
                        else if (index is 958 or 1080 or 1081)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Minecart", "Warp", null, index, "Buildings", "outdoors"));
                    }
                }
            }
            else if (loc is VolcanoDungeon)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "dungeon"); }
                        catch { continue; }
                        if (index == 367)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Volcano Exit", "Warp", null, index, "Buildings", "dungeon"));
                        else if (index == 77)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Dwarf Shop", "Service", null, index, "Buildings", "dungeon"));
                    }
                }
            }
            else if (loc is CommunityCenter)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "indoors"); }
                        catch { continue; }
                        if (index == 1799 || (index >= 1824 && index <= 1833))
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Bundle", "Quest", null, index, "Buildings", "indoors"));
                    }
                }
            }
            else if (loc is Sewer)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "st"); }
                        catch { continue; }
                        if (index == 21)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Sewer Exit", "Warp", null, index, "Buildings", "st"));
                        else if (index == 84)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Krobus Shop", "Service", null, index, "Buildings", "st"));
                    }
                }
            }
        }
        catch { }
    }

    private static List<OverviewRow> ScanDynamicTiles(GameLocation loc, Point farmerTile)
    {
        var rows = new List<OverviewRow>();
        if (loc is not MineShaft)
            return rows;
        int width, height;
        try
        {
            width = loc.Map.Layers[0].LayerWidth;
            height = loc.Map.Layers[0].LayerHeight;
        }
        catch { return rows; }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index;
                try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "mine"); }
                catch { continue; }
                if (index is 284 or 112 or 115 or 173 or 174 or 194 or 315 or 316 or 317)
                {
                    string name = index switch
                    {
                        284 => "Ladder",
                        112 or 115 => "Shaft",
                        173 or 174 => "Elevator",
                        _ => "Mine Exit"
                    };
                    rows.Add(OverviewRow.ForDynamicTile(loc, new Point(x, y), name, "Warp", "Buildings", "mine", index, farmerTile));
                }
            }
        }
        return rows;
    }

}
