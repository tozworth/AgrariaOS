using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Desktop;

// Window instances live until closed. Minimizing preserves their content and position.
internal abstract class OsWindow
{
    internal abstract string Id { get; }
    internal abstract string Title { get; }
    internal abstract int Icon { get; }
    internal virtual Point DefaultSize => new(420, 260);
    internal bool IsMinimized { get; set; }
    internal Rectangle Bounds { get; private set; }
    internal int TitleHeight { get; private set; }
    internal Rectangle TitleBounds => new(Bounds.X, Bounds.Y, Bounds.Width, TitleHeight);
    internal Rectangle CloseBounds => new(Bounds.Right - TitleHeight - 1, Bounds.Y + 1, TitleHeight, TitleHeight - 1);
    internal Rectangle MinimizeBounds => new(CloseBounds.X - TitleHeight, CloseBounds.Y, TitleHeight, CloseBounds.Height);
    private Point? position;

    internal bool Layout(Rectangle workArea, int titleHeight)
    {
        TitleHeight = titleHeight;
        if (workArea.Width < Math.Max(160, titleHeight * 4) || workArea.Height < titleHeight + 40)
        {
            Bounds = Rectangle.Empty;
            return false;
        }

        int width = Math.Min(DefaultSize.X, workArea.Width);
        int height = Math.Min(Math.Max(DefaultSize.Y, titleHeight + 80), workArea.Height);
        Point origin = position ?? new Point(workArea.X + (workArea.Width - width) / 2, workArea.Y + (workArea.Height - height) / 2);
        position = new Point(Math.Clamp(origin.X, workArea.Left, workArea.Right - width),
            Math.Clamp(origin.Y, workArea.Top, workArea.Bottom - height));
        Bounds = new Rectangle(position.Value.X, position.Value.Y, width, height);
        return true;
    }

    internal void MoveTo(Point origin) => position = origin;

    internal virtual void ReceiveContentClick(Point cursor, bool leftClick) { }
    internal virtual void DragContent(Point cursor) { }
    internal virtual void EndContentDrag() { }
    internal virtual void ReceiveScroll(int delta) { }

    internal void Draw(SpriteBatch batch, ModConfig config, Point cursor)
    {
        batch.Draw(Game1.staminaRect, Bounds, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, Bounds, config.Opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(Bounds.X + 1, Bounds.Y + TitleHeight, Bounds.Width - 2, 1),
            UiStyle.Border * config.Opacity);

        const int iconSize = 18;
        UiStyle.DrawIcon(batch, Icon, new Point(Bounds.X + 6, Bounds.Y + (TitleHeight - iconSize) / 2), 2, config.Opacity);
        DrawCenteredText(batch, Title, TitleBounds, UiStyle.Primary, config);
        DrawButton(batch, MinimizeBounds, cursor, config, close: false);
        DrawButton(batch, CloseBounds, cursor, config, close: true);
        DrawContent(batch, new Rectangle(Bounds.X + 1, Bounds.Y + TitleHeight + 1,
            Bounds.Width - 2, Bounds.Height - TitleHeight - 2), config);
    }

    protected abstract void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config);

    protected static void DrawCenteredText(SpriteBatch batch, string text, Rectangle bounds, Color color, ModConfig config)
    {
        Vector2 size = Game1.smallFont.MeasureString(text) * config.FontScale;
        UiText.Draw(batch, text, new Vector2(bounds.X + (bounds.Width - size.X) / 2,
            bounds.Y + (bounds.Height - size.Y) / 2), color, config);
    }

    private static void DrawButton(SpriteBatch batch, Rectangle bounds, Point cursor, ModConfig config, bool close)
    {
        if (bounds.Contains(cursor))
            batch.Draw(Game1.staminaRect, bounds, UiStyle.Border * config.Opacity);

        // Whole-pixel symbols keep these small controls independent of font scaling.
        int x = bounds.Center.X - 4;
        int y = bounds.Center.Y - 4;
        Color color = UiStyle.Primary * config.Opacity;
        if (close)
        {
            for (int i = 0; i < 9; i++)
            {
                batch.Draw(Game1.staminaRect, new Rectangle(x + i, y + i, 1, 1), color);
                batch.Draw(Game1.staminaRect, new Rectangle(x + 8 - i, y + i, 1, 1), color);
            }
        }
        else
            batch.Draw(Game1.staminaRect, new Rectangle(x, y + 7, 9, 1), color);
    }
}
