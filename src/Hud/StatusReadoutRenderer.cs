using AgrariaOS.Configuration;
using AgrariaOS.UI;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Hud;

internal sealed class StatusReadoutRenderer
{
    private static readonly Color PrimaryColor = new(226, 233, 239);
    private static readonly Color SecondaryColor = new(166, 187, 204);
    private static readonly Color WarningColor = new(255, 170, 95);
    private Snapshot? previous;
    private string dateAndTime = "";
    private string conditionsAndMoney = "";

    public void Draw(SpriteBatch batch, DayTimeMoneyBox clock, ModConfig config, int leftInset)
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
            Game1.player.Money, phase, weather, LocalizedContentManager.CurrentLanguageCode);
        if (snapshot != previous)
        {
            previous = snapshot;
            string season = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(snapshot.Season);
            string weekday = Game1.shortDayDisplayNameFromDayOfSeason(snapshot.Day);
            dateAndTime = $"{FormatTime(snapshot.Time, config.Use24HourClock)} | {season} {weekday} {snapshot.Day} Y{snapshot.Year}";
            string money = snapshot.Money.ToString("N0", CultureInfo.InvariantCulture);
            conditionsAndMoney = config.ShowWeather ? $"{phase} | {weather} | {money} g" : $"{phase} | {money} g";
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
        string securityText = config.ShowSecurity ? " " + SecurityStatus.Format(security) : "";
        float width = Math.Max(font.MeasureString(dateAndTime).X, font.MeasureString(conditionsAndMoney).X) * scale;
        if (config.ShowLocation)
            width = Math.Max(width, (font.MeasureString(locationText).X + font.MeasureString(securityText).X) * scale);

        int lineCount = (config.ShowLocation ? 1 : 0) + 2 + (Game1.player.hasVisibleQuests || Game1.options.zoomButtons ? 1 : 0);
        float x = MathF.Floor(Math.Clamp(Math.Max(config.X, leftInset), 0, Math.Max(0, Game1.uiViewport.Width - width - 8)));
        float y = MathF.Floor(Math.Clamp(config.Y, 0, Math.Max(0, Game1.uiViewport.Height - lineCount * lineHeight - 8)));
        if (config.ShowLocation)
        {
            UiText.Draw(batch, locationText, new Vector2(x, y), SecondaryColor, config);
            if (config.ShowSecurity)
                UiText.Draw(batch, securityText, new Vector2(x + MathF.Ceiling(font.MeasureString(locationText).X * scale), y), SecurityStatus.GetColor(security), config);
            y += lineHeight;
        }
        UiText.Draw(batch, dateAndTime, new Vector2(x, y), Game1.timeOfDay >= 2400 ? WarningColor : PrimaryColor, config);
        y += lineHeight;
        UiText.Draw(batch, conditionsAndMoney, new Vector2(x, y), PrimaryColor, config);
        y += lineHeight;

        // Keep vanilla journal/zoom actions, with hit boxes attached to their visible text.
        // Vanilla draw() restores its own positions when the replacement is switched off.
        Rectangle hidden = new(-10000, -10000, 0, 0);
        clock.questButton.bounds = hidden;
        clock.zoomOutButton.bounds = hidden;
        clock.zoomInButton.bounds = hidden;
        if (Game1.player.hasVisibleQuests)
        {
            string journal = Game1.player.hasNewQuestActivity() ? "[Journal !]" : "[Journal]";
            clock.questButton.bounds = DrawControl(batch, journal, new Vector2(x, y), config);
            x += clock.questButton.bounds.Width + 14;
        }
        if (Game1.options.zoomButtons)
        {
            clock.zoomOutButton.bounds = DrawControl(batch, "[-]", new Vector2(x, y), config);
            x += clock.zoomOutButton.bounds.Width + 10;
            clock.zoomInButton.bounds = DrawControl(batch, "[+]", new Vector2(x, y), config);
        }
    }

    private static Rectangle DrawControl(SpriteBatch batch, string text, Vector2 position, ModConfig config)
    {
        Vector2 size = Game1.smallFont.MeasureString(text) * config.FontScale;
        var bounds = new Rectangle((int)position.X - 2, (int)position.Y - 2, (int)Math.Ceiling(size.X) + 4, (int)Math.Ceiling(size.Y) + 4);
        Color color = bounds.Contains(Game1.getMouseX(), Game1.getMouseY()) ? PrimaryColor : SecondaryColor;
        UiText.Draw(batch, text, position, color, config);
        return bounds;
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
        LocalizedContentManager.LanguageCode Language);
}
