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
    internal static readonly Color DockHighlight = Color.Lerp(Border, Primary, 0.2f);
    private static readonly Color FullHearts = new(155, 199, 163);
    internal const int MailIcon = 144;
    internal const int UnreadMailIcon = 132;
    internal const int InventoryIcon = 152;
    internal const int NotificationIcon = 151;
    internal const int ChatIcon = 135;
    internal const int SocialIcon = 43;
    // A custom pixel icon, outside the emoji index range.
    internal const int OptionsIcon = -1;
    // Eight teeth and a transparent hub, in the same 9x9 footprint as emojis.
    // Light and dark pixels give the gear a small metallic bevel.
    private static readonly string[] OptionsGear =
    {
        "...111...",
        ".1.111.1.",
        ".1111111.",
        "111...112",
        "111...122",
        "112...222",
        ".1222222.",
        ".2.222.2.",
        "...222..."
    };
    private static readonly Color[] SpeakerColors =
    {
        new(104, 214, 255),
        new(137, 221, 151),
        new(238, 210, 126),
        new(255, 178, 127),
        new(196, 169, 255),
        new(244, 161, 209)
    };

    internal static Color SpeakerColor(string speaker)
    {
        // String.GetHashCode varies between launches; keep each name's color stable.
        uint hash = 2166136261;
        foreach (char character in speaker)
            hash = unchecked((hash ^ character) * 16777619);
        return SpeakerColors[hash % (uint)SpeakerColors.Length];
    }

    // White-to-green heart scale for the Social and Livestock grids. The full
    // end matches the vitals green above the toolbar.
    internal static Color HeartColor(float ratio)
        => Color.Lerp(Primary, FullHearts, Math.Clamp(ratio, 0f, 1f));

    internal static void DrawIcon(SpriteBatch batch, int index, Point position, int scale, float opacity)
    {
        if (index == OptionsIcon)
        {
            for (int y = 0; y < OptionsGear.Length; y++)
            {
                for (int x = 0; x < OptionsGear[y].Length; x++)
                {
                    char pixel = OptionsGear[y][x];
                    if (pixel == '.')
                        continue;
                    Color color = (pixel == '1' ? Primary : Muted) * opacity;
                    batch.Draw(Game1.staminaRect,
                        new Rectangle(position.X + x * scale, position.Y + y * scale, scale, scale),
                        null, color, 0f, Vector2.Zero, SpriteEffects.None, 0.91f);
                }
            }
            return;
        }
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

    internal static void DrawTrashIcon(SpriteBatch batch, Rectangle bounds, Color color)
    {
        // A 9x9 silhouette, matching the titlebar's whole-pixel close/minimize controls.
        int x = bounds.Center.X - 4;
        int y = bounds.Center.Y - 4;
        batch.Draw(Game1.staminaRect, new Rectangle(x + 3, y, 3, 2), color);
        batch.Draw(Game1.staminaRect, new Rectangle(x, y + 2, 9, 1), color);
        batch.Draw(Game1.staminaRect, new Rectangle(x + 1, y + 3, 1, 5), color);
        batch.Draw(Game1.staminaRect, new Rectangle(x + 7, y + 3, 1, 5), color);
        batch.Draw(Game1.staminaRect, new Rectangle(x + 3, y + 4, 1, 3), color);
        batch.Draw(Game1.staminaRect, new Rectangle(x + 5, y + 4, 1, 3), color);
        batch.Draw(Game1.staminaRect, new Rectangle(x + 2, y + 8, 5, 1), color);
    }
}
