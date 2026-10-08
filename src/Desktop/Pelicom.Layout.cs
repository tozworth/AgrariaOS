using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop;

partial class Pelicom : IClickableMenu
{
    private void SetPosition(PelicomEdge edge)
    {
        config.PelicomPositions[Game1.player.UniqueMultiplayerID] = edge;
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
        saveConfig();
    }

    internal static PelicomEdge GetPosition(ModConfig config)
        => config.PelicomPositions.GetValueOrDefault(Game1.player.UniqueMultiplayerID, PelicomEdge.Left);

    internal static Rectangle GetWorkArea(ModConfig config, int margin = 8)
    {
        PelicomEdge edge = GetPosition(config);
        int left = margin + (edge == PelicomEdge.Left ? StripWidth : 0);
        int top = margin + (edge == PelicomEdge.Top ? StripWidth : 0);
        int right = Game1.uiViewport.Width - margin - (edge == PelicomEdge.Right ? StripWidth : 0);
        int bottom = Game1.uiViewport.Height - margin - (edge == PelicomEdge.Bottom ? StripWidth : 0);
        return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public override bool isWithinBounds(int x, int y) => IsInteractive && drawnBounds.Contains(x, y);

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        // Drawing owns UI-space geometry; discard stale bounds until the next frame.
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
    }

}
