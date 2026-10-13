using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Hud;

internal static class BoxSelectRenderer
{
    private const int Cell = 2;
    private const int Border = Cell * 2;

    // RenderedWorld's existing world batch owns zoom/sampler state. Clip geometry
    // before drawing, without changing its batch or creating any textures.
    internal static void Draw(SpriteBatch batch, Rectangle bounds, Rectangle viewport)
    {
        Rectangle visible = Rectangle.Intersect(bounds, viewport);
        if (visible.IsEmpty)
            return;
        batch.Draw(Game1.staminaRect, visible, Color.White * 0.12f);
        int border = Math.Min(Border, Math.Min(bounds.Width, bounds.Height));
        Checker(new Rectangle(bounds.X, bounds.Y, bounds.Width, border));
        Checker(new Rectangle(bounds.X, bounds.Bottom - border, bounds.Width, border));
        int height = Math.Max(0, bounds.Height - border * 2);
        Checker(new Rectangle(bounds.X, bounds.Y + border, border, height));
        Checker(new Rectangle(bounds.Right - border, bounds.Y + border, border, height));

        void Checker(Rectangle strip)
        {
            Rectangle clip = Rectangle.Intersect(strip, viewport);
            if (clip.IsEmpty)
                return;
            int left = strip.X + (clip.X - strip.X) / Cell * Cell;
            int top = strip.Y + (clip.Y - strip.Y) / Cell * Cell;
            for (int y = top; y < clip.Bottom; y += Cell)
                for (int x = left; x < clip.Right; x += Cell)
                {
                    Rectangle cell = Rectangle.Intersect(new Rectangle(x, y, Cell, Cell), clip);
                    bool white = (((x - bounds.X) / Cell + (y - bounds.Y) / Cell) & 1) == 0;
                    batch.Draw(Game1.staminaRect, cell, white ? Color.White * 0.85f : Color.Black * 0.65f);
                }
        }
    }
}
