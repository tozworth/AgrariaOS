using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS;

internal sealed class Pelicom : IClickableMenu
{
    internal const int StripWidth = 35;
    private const int SlotHeight = 35;
    private const int TopPadding = 8;
    private const int BottomEntryCount = 2;
    private const int IconSize = 9;
    private const int IconScale = 3;
    private static readonly Color Background = new(38, 38, 38);
    private static readonly Color HoverBackground = new(78, 78, 78);
    private static readonly (int Emoji, string Label)[] Entries =
    {
        (107, "Dashboard"),
        (152, "Inventory"),
        (74, "Livestock"),
        (121, "Mining Ledger"),
        (41, "Wallet"),
        (144, "Mail"),
        (151, "Notifications"),
        (131, "Exit")
    };

    private readonly ModConfig config;
    private readonly Func<bool> isEnabled;
    private readonly Action<Exception> onDrawFailed;
    private Rectangle drawnBounds;

    public Pelicom(ModConfig config, Func<bool> isEnabled, Action<Exception> onDrawFailed)
    {
        this.config = config;
        this.isEnabled = isEnabled;
        this.onDrawFailed = onDrawFailed;
    }

    internal bool IsVisible => isEnabled() && Game1.IsHudDrawn && !Game1.eventUp && Game1.farmEvent == null
        && Game1.activeClickableMenu == null && Game1.currentMinigame == null;

    // Native HUD dispatch consumes clicks inside this rail before they reach world/tool actions.
    // Every entry, including Exit, is deliberately a silent placeholder.
    public override void receiveLeftClick(int x, int y, bool playSound = true) { }
    public override void receiveRightClick(int x, int y, bool playSound = true) { }
    public override bool isWithinBounds(int x, int y) => IsVisible && drawnBounds.Contains(x, y);

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        // Drawing owns UI-space geometry; discard stale bounds until the next frame.
        drawnBounds = Rectangle.Empty;
    }

    public override void draw(SpriteBatch b)
    {
        drawnBounds = Rectangle.Empty;
        if (!IsVisible)
            return;

        try
        {
            DrawStrip(b);
        }
        catch (Exception ex)
        {
            drawnBounds = Rectangle.Empty;
            onDrawFailed(ex);
        }
    }

    private void DrawStrip(SpriteBatch batch)
    {
        Rectangle strip = new(0, 0, StripWidth, Game1.uiViewport.Height);
        Texture2D icons = ChatBox.emojiTexture ?? Game1.content.Load<Texture2D>("LooseSprites\\emojis");
        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);
        int hovered = -1;
        int hoveredY = 0;
        int bottomGroupY = strip.Bottom - TopPadding - BottomEntryCount * SlotHeight;
        int separatorY = bottomGroupY - TopPadding;

        // Reuse the game's PointClamp UI batch and sprites; never change shared render state.
        batch.Draw(Game1.staminaRect, strip, Background * config.Opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(StripWidth - 1, 0, 1, strip.Height), HoverBackground * config.Opacity);
        if (separatorY >= 0)
            batch.Draw(Game1.staminaRect, new Rectangle(4, separatorY, StripWidth - 8, 1), HoverBackground * config.Opacity);
        for (int i = 0; i < Entries.Length; i++)
        {
            bool isBottomEntry = i >= Entries.Length - BottomEntryCount;
            int slotY = isBottomEntry ? bottomGroupY + (i - (Entries.Length - BottomEntryCount)) * SlotHeight
                : TopPadding + i * SlotHeight;
            Rectangle slot = new(0, slotY, StripWidth - 1, SlotHeight);
            // On unusually short viewports, keep the bottom group clear of the top entries.
            if (slot.Top < 0 || slot.Bottom > strip.Bottom || (!isBottomEntry && slot.Bottom > separatorY - TopPadding))
                continue;

            if (slot.Contains(mouseX, mouseY))
            {
                hovered = i;
                hoveredY = slot.Y;
                batch.Draw(Game1.staminaRect, slot, HoverBackground * config.Opacity);
            }

            int index = Entries[i].Emoji;
            Rectangle source = new(index * IconSize % icons.Width, index * IconSize / icons.Width * IconSize, IconSize, IconSize);
            Vector2 position = new((StripWidth - IconSize * IconScale) / 2, slot.Y + (SlotHeight - IconSize * IconScale) / 2);
            batch.Draw(icons, position, source, Color.White * config.Opacity, 0f, Vector2.Zero,
                IconScale, SpriteEffects.None, 0.91f);
        }

        if (hovered >= 0)
        {
            string label = Entries[hovered].Label;
            Vector2 size = Game1.smallFont.MeasureString(label) * config.FontScale;
            int y = Math.Clamp(hoveredY + (SlotHeight - (int)MathF.Ceiling(size.Y)) / 2,
                0, Math.Max(0, strip.Height - (int)MathF.Ceiling(size.Y) - 4));
            Rectangle tooltip = new(StripWidth + 4, y, (int)MathF.Ceiling(size.X) + 12, (int)MathF.Ceiling(size.Y) + 4);
            batch.Draw(Game1.staminaRect, tooltip, Background * config.Opacity);
            HudRenderer.DrawText(batch, label, new Vector2(tooltip.X + 6, tooltip.Y + 2), Color.White, config);
        }

        drawnBounds = strip;
    }
}
