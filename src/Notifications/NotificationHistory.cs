using AgrariaOS.Hud;
using Microsoft.Xna.Framework;
using StardewValley;

namespace AgrariaOS.Notifications;

// Immutable arrivals, separate from vanilla's mutable, merged live HUD messages.
internal sealed record NotificationEntry(string Text, int Kind, int Day, string Season, int Year, int Time)
{
    internal string Timestamp(bool use24HourClock)
        => $"{Season} {Day} Y{Year}  |  {StatusReadoutRenderer.FormatTime(Time, use24HourClock)}";
}

internal sealed class NotificationHistory
{
    private readonly List<NotificationEntry> entries = new();
    private int readCount;
    internal IReadOnlyList<NotificationEntry> Entries => entries;
    internal int UnreadCount => entries.Count - readCount;
    internal int RemovalVersion { get; private set; }

    internal void Record(HUDMessage message)
    {
        string season = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Game1.currentSeason);
        entries.Add(new NotificationEntry(GetText(message), message.whatType,
            Game1.dayOfMonth, season, Game1.year, Game1.timeOfDay));
    }

    internal void MarkRead() => readCount = entries.Count;

    internal bool Remove(NotificationEntry entry)
    {
        // Equal messages can arrive at the same game time; dismiss this exact arrival.
        int index = entries.FindIndex(candidate => ReferenceEquals(candidate, entry));
        if (index < 0)
            return false;
        entries.RemoveAt(index);
        if (index < readCount)
            readCount--;
        RemovalVersion++;
        return true;
    }

    internal void Clear()
    {
        entries.Clear();
        readCount = 0;
        RemovalVersion++;
    }

    internal static string GetText(HUDMessage message)
    {
        // Corner textboxes arrive pre-wrapped for vanilla's dialogue font. Reflow for our width.
        string text = string.Join(" ", (message.message ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return message.number > 0 ? $"{text} ×{message.number}" : text;
    }

    internal static Color GetColor(int kind) => kind switch
    {
        HUDMessage.error_type => new Color(255, 170, 95),
        HUDMessage.achievement_type => new Color(104, 214, 255),
        HUDMessage.newQuest_type => new Color(230, 209, 128),
        _ => UI.UiStyle.Primary
    };
}
