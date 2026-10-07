using Microsoft.Xna.Framework;
using StardewValley;

namespace AgrariaOS.Movement;

/// <summary>Turns a click on an exit's last visible tile into a step across its warp boundary.</summary>
internal static class WarpDestination
{
    internal static Point? Resolve(GameLocation location, Farmer farmer, Point clicked)
    {
        int width = location.Map.Layers[0].LayerWidth;
        int height = location.Map.Layers[0].LayerHeight;
        foreach (Warp warp in location.warps)
        {
            if (warp.npcOnly.Value)
                continue;
            Point trigger = new(warp.X, warp.Y);
            // Only the actual exit row/column qualifies; arbitrary map-edge clicks remain ground.
            if (clicked == trigger || (warp.X == -1 && clicked == new Point(0, warp.Y))
                || (warp.X == width && clicked == new Point(width - 1, warp.Y))
                || (warp.Y == -1 && clicked == new Point(warp.X, 0))
                || (warp.Y == height && clicked == new Point(warp.X, height - 1)))
            {
                // Ask the public collision API so locations which disable warps stay disabled.
                Rectangle box = new(trigger.X * 64 + 1, trigger.Y * 64 + 1, 62, 62);
                if (location.isCollidingWithWarp(box, farmer) != null)
                    return trigger;
            }
        }
        return null;
    }
}
