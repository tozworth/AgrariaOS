using AgrariaOS.Hud;
using StardewValley;

namespace AgrariaOS.Chat;

internal sealed record ChatEntry(string Speaker, string Text, int Time)
{
    internal string Timestamp => StatusReadoutRenderer.FormatTime(Time, use24HourClock: false);
}

// Local session only. No vanilla chat, command execution, or network messages.
internal sealed class ChatHistory
{
    private readonly List<ChatEntry> entries = new();
    internal IReadOnlyList<ChatEntry> Entries => entries;

    internal void Record(string text) => Record(Game1.player.Name, text);

    internal void Record(string speaker, string text)
    {
        entries.Add(new ChatEntry(speaker, text, Game1.timeOfDay));
    }
}

internal sealed class ChatSession
{
    internal ChatHistory Session { get; } = new();
    internal ChatHistory Local { get; } = new();
}
