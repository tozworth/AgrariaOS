using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.UI;

internal static class UiStyle
{
    internal static readonly Color Background = new(38, 38, 38);
    internal static readonly Color Border = new(78, 78, 78);
    internal static readonly Color Primary = new(226, 233, 239);
    internal static readonly Color Muted = new(150, 150, 150);
    internal const int MailIcon = 144;
    internal const int InventoryIcon = 152;
    internal const int NotificationIcon = 151;

    internal static void DrawIcon(SpriteBatch batch, int index, Point position, int scale, float opacity)
    {
        const int size = 9;
        Texture2D icons = ChatBox.emojiTexture ?? Game1.content.Load<Texture2D>("LooseSprites\\emojis");
        Rectangle source = new(index * size % icons.Width, index * size / icons.Width * size, size, size);
        batch.Draw(icons, position.ToVector2(), source, Color.White * opacity, 0f, Vector2.Zero,
            scale, SpriteEffects.None, 0.91f);
    }

    internal static void DrawBorder(SpriteBatch batch, Rectangle bounds, float opacity)
    {
        Color color = Border * opacity;
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
    }
}
