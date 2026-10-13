using AgrariaOS.Configuration;
using AgrariaOS.UI;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Hud;

internal static class VitalsRenderer
{
    internal const int ToolbarGap = 4;
    private const int IconSize = 9;
    private const int IconGap = 4;
    private const int StatGap = 12;
    internal static readonly Color EmptyColor = new(205, 150, 150);
    internal static readonly Color MidColor = new(205, 194, 150);
    internal static readonly Color FullColor = new(155, 199, 163);

    public static int GetHeight(ModConfig config) =>
        (int)MathF.Ceiling(Math.Max(Game1.smallFont.LineSpacing * config.FontScale, IconSize * GetIconScale(config)));

    private static float GetIconScale(ModConfig config) => Math.Max(1f, MathF.Round(2f * config.FontScale));

    public static void Draw(SpriteBatch batch, Rectangle toolbar, ModConfig config)
    {
        Farmer player = Game1.player;
        string health = string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, player.health)}/{player.maxHealth}");
        string energy = string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Max(0f, player.Stamina)}/{player.MaxStamina}");
        float iconScale = GetIconScale(config);
        float iconWidth = IconSize * iconScale;
        float healthWidth = MathF.Ceiling(UiText.Measure(health).X * config.FontScale);
        float energyWidth = MathF.Ceiling(UiText.Measure(energy).X * config.FontScale);
        float width = 2 * (iconWidth + IconGap) + healthWidth + StatGap + energyWidth;
        int height = GetHeight(config);
        Vector2 position = new(MathF.Floor(toolbar.Right - width), toolbar.Top - ToolbarGap - height);

        // The chat picker's 9px glyphs: solid heart (43) and lightning bolt (102).
        // Load through game content so asset edits apply; never bundle the game's textures.
        Texture2D icons = ChatBox.emojiTexture ?? Game1.content.Load<Texture2D>("LooseSprites\\emojis");
        DrawIcon(batch, icons, 43, position, height, iconScale, config.Opacity);
        position.X += iconWidth + IconGap;
        UiText.Draw(batch, health, position, GetColor(player.health, player.maxHealth), config);
        position.X += healthWidth + StatGap;
        DrawIcon(batch, icons, 102, position, height, iconScale, config.Opacity);
        position.X += iconWidth + IconGap;
        UiText.Draw(batch, energy, position, GetColor(player.Stamina, player.MaxStamina), config);
    }

    private static void DrawIcon(SpriteBatch batch, Texture2D texture, int index, Vector2 position, int height,
        float scale, float opacity)
    {
        Rectangle source = new(index * IconSize % texture.Width, index * IconSize / texture.Width * IconSize, IconSize, IconSize);
        position.Y += MathF.Floor((height - IconSize * scale) / 2f);
        batch.Draw(texture, position, source, Color.White * opacity, 0f, Vector2.Zero, scale, SpriteEffects.None, 0.91f);
    }

    internal static Color GetColor(float current, float maximum)
    {
        float ratio = maximum > 0 ? Math.Clamp(current / maximum, 0f, 1f) : 0f;
        return ratio <= 0.5f ? Color.Lerp(EmptyColor, MidColor, ratio * 2f)
            : Color.Lerp(MidColor, FullColor, (ratio - 0.5f) * 2f);
    }
}
