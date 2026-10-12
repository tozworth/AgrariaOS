using AgrariaOS.Configuration;
using AgrariaOS.UI;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Hud;

internal sealed partial class StatusReadoutRenderer : IDisposable
{
    private static readonly Color PrimaryColor = new(226, 233, 239);
    private static readonly Color SecondaryColor = new(166, 187, 204);
    private static readonly Color WarningColor = new(255, 170, 95);
    private Snapshot? previous;
    private string timeText = "";
    private string dateText = "";
    private string moneyText = "";
    private Texture2D? cornerShadow;
    private Rectangle drawnBounds;
    private Point drawnViewport;

    internal bool Contains(Point cursor) => drawnViewport == new Point(Game1.uiViewport.Width, Game1.uiViewport.Height)
        && drawnBounds.Contains(cursor);

    public void Draw(SpriteBatch batch, DayTimeMoneyBox clock, ModConfig config, Rectangle workArea)
    {
        GameLocation location = Game1.currentLocation;
        string phase = Game1.isDarkOut(location) ? "NIGHT" : Game1.isStartingToGetDarkOut(location) ? "DUSK" : "DAY";
        string weather = location.IsGreenRainingHere() ? "GREEN RAIN"
            : location.IsLightningHere() ? "STORM"
            : location.IsSnowingHere() ? "SNOW"
            : location.IsRainingHere() ? "RAIN"
            : location.IsDebrisWeatherHere() ? "WIND"
            : "CLEAR";

        var snapshot = new Snapshot(Game1.timeOfDay, Game1.dayOfMonth, Game1.currentSeason, Game1.year,
            Game1.player.Money, phase, weather, LocalizedContentManager.CurrentLanguageCode, !Game1.shouldTimePass(),
            config.Use24HourClock);
        if (snapshot != previous)
        {
            previous = snapshot;
            timeText = FormatTime(snapshot.Time, config.Use24HourClock);
            dateText = FormatDate(snapshot.Day, snapshot.Season, snapshot.Year);
            moneyText = snapshot.Money.ToString("N0", CultureInfo.InvariantCulture) + " g";
        }

        SpriteFont font = Game1.smallFont;
        float scale = config.FontScale;
        float lineHeight = (float)Math.Ceiling(font.LineSpacing * scale) + 3;
        string locationText = location.DisplayName;
        if (location is StardewValley.Locations.MineShaft mine)
            locationText = mine.getMineArea() == StardewValley.Locations.MineShaft.desertArea
                ? $"Skull Cavern - Floor {mine.mineLevel - 120}"
                : mine.getMineArea() == StardewValley.Locations.MineShaft.quarryMineShaft ? "Quarry Mine" : $"The Mines - Floor {mine.mineLevel}";
        float security = SecurityStatus.GetRating(location);
        string securityText = config.ShowSecurity
            ? (config.ShowLocation ? " " : "") + SecurityStatus.Format(security) : "";
        string conditionsAndMoney = string.Join(" | ", new[]
        {
            config.ShowDayPhase ? phase : "",
            config.ShowWeather ? weather : "",
            config.ShowMoney ? moneyText : ""
        }.Where(text => text.Length > 0));
        bool showLocationLine = config.ShowLocation || config.ShowSecurity;
        bool showDateLine = config.ShowTime || config.ShowDate;
        bool showConditionsLine = conditionsAndMoney.Length > 0;
        bool showJournal = config.ShowJournal && Game1.player.hasVisibleQuests;
        bool showZoom = config.ShowZoomControls && Game1.options.zoomButtons;
        string journal = showJournal ? (Game1.player.hasNewQuestActivity() ? "[Journal !]" : "[Journal]") : "";
        Vector2 timeSize = font.MeasureString(timeText) * scale;
        float timeWidth = MathF.Ceiling(timeSize.X);
        int iconScale = Math.Max(1, (int)MathF.Round(scale));
        int pauseWidth = 6 * iconScale;
        int pauseHeight = 8 * iconScale;
        int pausePadding = 3 * iconScale;
        // Reserve the same compact gap while running so the clock/date never jump.
        int pauseSpace = config.ShowTime ? pauseWidth + pausePadding * 2 : 0;
        string visibleDate = config.ShowDate ? (config.ShowTime ? "| " : "") + dateText : "";
        float width = Math.Max((config.ShowTime ? timeWidth + pauseSpace : 0)
            + font.MeasureString(visibleDate).X * scale, font.MeasureString(conditionsAndMoney).X * scale);
        if (showLocationLine)
            width = Math.Max(width, ((config.ShowLocation ? font.MeasureString(locationText).X : 0)
                + font.MeasureString(securityText).X) * scale);
        // Include controls in the layout so the full readout stays inside the work area.
        float controlsWidth = showJournal ? MathF.Ceiling(font.MeasureString(journal).X * scale) + 4 : 0;
        if (showZoom)
            controlsWidth += (showJournal ? 14 : 0) + MathF.Ceiling(font.MeasureString("[-]").X * scale)
                + MathF.Ceiling(font.MeasureString("[+]").X * scale) + 18;
        width = Math.Max(width, controlsWidth);

        int lineCount = (showLocationLine ? 1 : 0) + (showDateLine ? 1 : 0) + (showConditionsLine ? 1 : 0)
            + (showJournal || showZoom ? 1 : 0);
        // Keep a visible way to restore sections even when all available rows are hidden.
        bool showRestoreLabel = lineCount == 0;
        if (showRestoreLabel)
        {
            lineCount = 1;
            width = font.MeasureString("[Status]").X * scale;
        }
        float x = MathF.Floor(Math.Clamp(Math.Max(config.X, workArea.Left), 0, Math.Max(0, workArea.Right - width)));
        float y = MathF.Floor(Math.Clamp(Math.Max(config.Y, workArea.Top), 0, Math.Max(0, workArea.Bottom - lineCount * lineHeight)));
        drawnBounds = new Rectangle((int)x - 2, (int)y - 2, (int)MathF.Ceiling(width) + 4,
            (int)MathF.Ceiling(lineCount * lineHeight) + 4);
        drawnViewport = new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);
        DrawCornerShadow(batch, x + width, y + lineCount * lineHeight, config.Opacity);
        if (showRestoreLabel)
            UiText.Draw(batch, "[Status]", new Vector2(x, y), SecondaryColor, config);
        if (showLocationLine)
        {
            float securityX = x;
            if (config.ShowLocation)
            {
                UiText.Draw(batch, locationText, new Vector2(x, y), SecondaryColor, config);
                securityX += MathF.Ceiling(font.MeasureString(locationText).X * scale);
            }
            if (config.ShowSecurity)
                UiText.Draw(batch, securityText, new Vector2(securityX, y), SecurityStatus.GetColor(security), config);
            y += lineHeight;
        }
        Color timeColor = Game1.timeOfDay >= 2400 ? WarningColor : PrimaryColor;
        if (showDateLine)
        {
            if (config.ShowTime)
            {
                UiText.Draw(batch, timeText, new Vector2(x, y), timeColor, config);
                if (snapshot.Paused)
                {
                    int iconX = (int)(x + timeWidth) + pausePadding;
                    int iconY = (int)MathF.Round(y + (timeSize.Y - pauseHeight) / 2);
                    for (int bar = 0; bar < 2; bar++)
                    {
                        Rectangle bounds = new(iconX + bar * 4 * iconScale, iconY, 2 * iconScale, pauseHeight);
                        Rectangle shadow = new(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height);
                        batch.Draw(Game1.staminaRect, shadow, null, Color.Black * (config.Opacity * 0.8f),
                            0f, Vector2.Zero, SpriteEffects.None, 0.9f);
                        batch.Draw(Game1.staminaRect, bounds, null, timeColor * config.Opacity,
                            0f, Vector2.Zero, SpriteEffects.None, 0.91f);
                    }
                }
            }
            if (config.ShowDate)
                UiText.Draw(batch, visibleDate, new Vector2(x + (config.ShowTime ? timeWidth + pauseSpace : 0), y), timeColor, config);
            y += lineHeight;
        }
        if (showConditionsLine)
        {
            UiText.Draw(batch, conditionsAndMoney, new Vector2(x, y), PrimaryColor, config);
            y += lineHeight;
        }

        // Keep vanilla journal/zoom actions, with hit boxes attached to their visible text.
        // Vanilla draw() restores its own positions when the replacement is switched off.
        Rectangle hidden = new(-10000, -10000, 0, 0);
        clock.questButton.bounds = hidden;
        clock.zoomOutButton.bounds = hidden;
        clock.zoomInButton.bounds = hidden;
        if (showJournal)
        {
            clock.questButton.bounds = DrawControl(batch, journal, new Vector2(x, y), config);
            x += clock.questButton.bounds.Width + 14;
        }
        if (showZoom)
        {
            clock.zoomOutButton.bounds = DrawControl(batch, "[-]", new Vector2(x, y), config);
            x += clock.zoomOutButton.bounds.Width + 10;
            clock.zoomInButton.bounds = DrawControl(batch, "[+]", new Vector2(x, y), config);
        }
    }

    private void DrawCornerShadow(SpriteBatch batch, float right, float bottom, float opacity)
    {
        if (cornerShadow == null || cornerShadow.IsDisposed || cornerShadow.GraphicsDevice != batch.GraphicsDevice)
        {
            cornerShadow?.Dispose();
            // Cache a radial fade in the existing HUD batch; keep its PointClamp sampler
            // and pixel-aligned text intact. Only the bottom-right quarter is needed.
            const int size = 512;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = MathF.Sqrt(x * x + y * y) / (size - 1);
                    float fade = Math.Clamp((distance - 0.2f) / 0.8f, 0f, 1f);
                    pixels[y * size + x] = Color.Black * (0.65f * (1f - fade * fade * (3f - 2f * fade)));
                }
            }
            cornerShadow = new Texture2D(batch.GraphicsDevice, size, size);
            cornerShadow.SetData(pixels);
        }

        // Stay anchored to the viewport corner, with a soft edge beyond the readout.
        int radius = (int)MathF.Ceiling(Math.Max(420f, MathF.Sqrt(right * right + bottom * bottom) + 160f));
        batch.Draw(cornerShadow, new Rectangle(0, 0, radius, radius), null, Color.White * opacity,
            0f, Vector2.Zero, SpriteEffects.None, 0.89f);
    }

    public void Dispose()
    {
        cornerShadow?.Dispose();
        cornerShadow = null;
    }

    private static Rectangle DrawControl(SpriteBatch batch, string text, Vector2 position, ModConfig config)
    {
        Vector2 size = Game1.smallFont.MeasureString(text) * config.FontScale;
        var bounds = new Rectangle((int)position.X - 2, (int)position.Y - 2, (int)Math.Ceiling(size.X) + 4, (int)Math.Ceiling(size.Y) + 4);
        Color color = bounds.Contains(Game1.getMouseX(), Game1.getMouseY()) ? PrimaryColor : SecondaryColor;
        UiText.Draw(batch, text, position, color, config);
        return bounds;
    }

    internal static string FormatDate(int day, string season, int year)
    {
        string seasonName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(season);
        string weekday = Game1.shortDayDisplayNameFromDayOfSeason(day);
        return $"{seasonName} {weekday} {day} Y{year}";
    }

    internal static string FormatTime(int gameTime, bool use24HourClock)
    {
        // Stardew represents the same day's post-midnight hours as 2400..2600.
        int hour = gameTime / 100 % 24;
        int minute = gameTime % 100;
        if (use24HourClock)
            return $"{hour:00}:{minute:00}";
        int displayHour = hour % 12;
        return $"{(displayHour == 0 ? 12 : displayHour)}:{minute:00} {(hour < 12 ? "AM" : "PM")}";
    }

    private sealed record Snapshot(int Time, int Day, string Season, int Year, int Money, string Phase, string Weather,
        LocalizedContentManager.LanguageCode Language, bool Paused, bool Use24HourClock);
}
