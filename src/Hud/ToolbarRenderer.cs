using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Hud;

internal sealed class ToolbarRenderer
{
    private const int SlotSize = 40;
    private const float ItemScale = 0.5f;
    private const float CountScale = 2f;
    private const int Padding = 2;
    private static readonly Color Background = new(38, 38, 38);
    private static readonly Color SelectedBackground = new(78, 78, 78);
    private Toolbar? compactToolbar;
    private Rectangle drawnBounds;

    private Rectangle UpdateLayout(Toolbar toolbar)
    {
        compactToolbar = toolbar;
        int height = SlotSize + Padding * 2;
        int width = toolbar.buttons.Count * SlotSize + Padding * 2;
        int x = (Game1.uiViewport.Width - width) / 2;
        int y = Game1.uiViewport.Height - height;

        // Keep the vanilla anchor valid so switching back restores its own layout immediately.
        toolbar.yPositionOnScreen = Game1.uiViewport.Height + 8;
        drawnBounds = new Rectangle(x, y, width, height);
        ApplyDrawnLayout(toolbar);
        return drawnBounds;
    }

    public Rectangle GetDrawnBounds(Toolbar toolbar) => compactToolbar == toolbar ? drawnBounds : Rectangle.Empty;

    public void ApplyDrawnLayout(Toolbar toolbar)
    {
        if (compactToolbar != toolbar)
            return;

        // Reuse the geometry that was actually drawn, including after vanilla's first-hover resize.
        for (int i = 0; i < toolbar.buttons.Count; i++)
            toolbar.buttons[i].bounds = new Rectangle(drawnBounds.X + Padding + i * SlotSize,
                drawnBounds.Y + Padding, SlotSize, SlotSize);
    }

    public void RestoreLayout(Toolbar toolbar)
    {
        if (compactToolbar != toolbar)
            return;

        compactToolbar = null;
        drawnBounds = Rectangle.Empty;
        toolbar.hoverItem = null;
        Rectangle viewport = new(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height);
        toolbar.gameWindowSizeChanged(viewport, viewport);
    }

    public void Draw(SpriteBatch batch, Toolbar toolbar, ModConfig config, bool showVitals)
    {
        if (Game1.activeClickableMenu != null)
            return;

        Rectangle strip = UpdateLayout(toolbar);
        if (Game1.options.pinToolbarToggle)
        {
            // Match vanilla's fade when the player approaches a pinned toolbar.
            Vector2 playerPosition = Game1.GlobalToLocal(Game1.viewport, Game1.player.StandingPixel.ToVector2());
            toolbar.transparency = Math.Min(1f, toolbar.transparency + 0.075f);
            if (playerPosition.Y > Game1.viewport.Height - 192)
                toolbar.transparency = Math.Max(0.33f, toolbar.transparency - 0.15f);
        }
        else
            toolbar.transparency = 1f;

        // Reuse the game's solid texture and active UI batch without changing render state.
        batch.Draw(Game1.staminaRect, strip, Background * toolbar.transparency);
        for (int i = 0; i < toolbar.buttons.Count; i++)
        {
            if (Game1.player.CurrentToolIndex == i)
                batch.Draw(Game1.staminaRect, toolbar.buttons[i].bounds, SelectedBackground * toolbar.transparency);
        }

        for (int i = 0; i < toolbar.buttons.Count; i++)
        {
            ClickableComponent button = toolbar.buttons[i];
            button.scale = 1f;
            if (i >= Game1.player.Items.Count || Game1.player.Items[i] is not Item item)
                continue;

            // Fixed 32px sprites: selection and hover only change the background, never the scale.
            Vector2 position = new(button.bounds.X + (SlotSize - 64) / 2, button.bounds.Y + (SlotSize - 64) / 2);
            item.drawInMenu(batch, position, ItemScale, toolbar.transparency, 0.88f,
                StackDrawType.Hide, Color.White, drawShadow: false);
            DrawBadges(batch, item, button.bounds, toolbar.transparency);
        }

        if (showVitals)
            VitalsRenderer.Draw(batch, strip, config);

        if (toolbar.hoverItem is Item hovered)
        {
            IClickableMenu.drawToolTip(batch, hovered.getDescription(), hovered.DisplayName, hovered);
            toolbar.hoverItem = null;
        }
    }

    private static void DrawBadges(SpriteBatch batch, Item item, Rectangle slot, float opacity)
    {
        // Vanilla anchors its badges to 64px slots even when the sprite is scaled down.
        // Place the smaller badges inside our slot instead.
        if (!item.IsRecipe && item.maximumStackSize() > 1 && item.Stack > 1 && item.Stack != int.MaxValue)
        {
            int width = Utility.getWidthOfTinyDigitString(item.Stack, CountScale);
            Vector2 position = new(slot.Right - Padding - width, slot.Bottom - Padding - 7 * CountScale);
            Utility.drawTinyDigits(item.Stack, batch, position + new Vector2(1, 1), CountScale, 0.89f, Color.Black * opacity);
            Utility.drawTinyDigits(item.Stack, batch, position, CountScale, 0.9f, Color.White * opacity);
        }

        if (item.Quality > 0)
        {
            Rectangle source = item.Quality < 4 ? new Rectangle(338 + (item.Quality - 1) * 8, 400, 8, 8)
                : new Rectangle(346, 392, 8, 8);
            batch.Draw(Game1.mouseCursors, new Vector2(slot.X + Padding, slot.Bottom - Padding - 8), source,
                Color.White * opacity, 0, Vector2.Zero, 1f, SpriteEffects.None, 0.9f);
        }
    }
}
