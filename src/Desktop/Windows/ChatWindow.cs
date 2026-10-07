using AgrariaOS.Chat;
using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

internal sealed class ChatWindow : OsWindow
{
    internal const string WindowId = "Chat";
    private const int Padding = 8;
    private const int ScrollbarWidth = 10;
    private readonly ChatSession session;
    private ChatHistory History => tabs.SelectedId == "Local" ? session.Local : session.Session;
    private readonly TabBar tabs = new(new WindowTab("Session", "Session"), new WindowTab("Local", "Local"));
    private readonly Dictionary<string, (int FirstLine, bool FollowTail)> scrollPositions = new();
    private readonly TextComposer composer;
    private readonly List<(ChatEntry Entry, string Text, int SpeakerLength, bool First)> lines = new();
    private Rectangle composerBounds;
    private Rectangle sendBounds;
    private Rectangle rowsBounds;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private int firstLine;
    private int visibleLines;
    private bool followTail = true;
    private int? scrollDragOffset;
    private (int Count, int Width, float Scale, SpriteFont Font)? layoutKey;

    internal ChatWindow(ChatSession session)
    {
        this.session = session;
        composer = new TextComposer(text =>
        {
            session.Session.Record(text);
            followTail = true;
        });
    }

    internal override string Id => WindowId;
    internal override string Title => "Chat";
    internal override int Icon => UiStyle.ChatIcon;
    internal override Point DefaultSize => new(580, 360);
    internal override IKeyboardSubscriber? KeyboardSubscriber => tabs.SelectedId == "Session" ? composer : null;
    internal override void ReceiveKey(Keys key) => composer.Navigate(key);
    private int MaxFirstLine => Math.Max(0, lines.Count - visibleLines);

    protected override Point GetMinimumSize(ModConfig config)
        => new(280, TitleHeight + TabBar.Height(config) + Padding * 3 + UiText.LineHeight(config.FontScale) * 4 + 8);

    private void RefreshLines(int width, ModConfig config)
    {
        ChatHistory history = History;
        var key = (history.Entries.Count, width, config.FontScale, Game1.smallFont);
        if (layoutKey == key)
            return;
        bool appendOnly = layoutKey.HasValue && layoutKey.Value.Width == width
            && layoutKey.Value.Scale == config.FontScale && ReferenceEquals(layoutKey.Value.Font, Game1.smallFont);
        ChatEntry? anchor = firstLine < lines.Count ? lines[firstLine].Entry : null;
        int anchorOffset = anchor == null ? 0 : firstLine - lines.FindIndex(line => ReferenceEquals(line.Entry, anchor));
        int start = appendOnly ? layoutKey!.Value.Count : 0;
        if (!appendOnly)
            lines.Clear();
        for (int i = start; i < history.Entries.Count; i++)
        {
            ChatEntry entry = history.Entries[i];
            int timestampWidth = (int)MathF.Ceiling(Game1.smallFont.MeasureString(entry.Timestamp).X * config.FontScale);
            string speaker = string.Join(" ", entry.Speaker.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) + ":";
            string text = string.Join(" ", $"{speaker} {entry.Text}".Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            int offset = 0;
            bool first = true;
            // Reserve the bold overdraw pixel. Track the prefix through wrapping, including long names.
            foreach (string line in UiText.Wrap(text, width - timestampWidth - Padding - 2, config.FontScale))
            {
                int speakerLength = Math.Clamp(speaker.Length - offset, 0, line.Length);
                lines.Add((entry, line, speakerLength, first));
                offset += line.Length;
                if (offset < text.Length && text[offset] == ' ')
                    offset++;
                first = false;
            }
        }
        if (!appendOnly && !followTail && anchor != null)
        {
            int index = lines.FindIndex(line => ReferenceEquals(line.Entry, anchor));
            if (index >= 0)
            {
                int count = lines.Count(line => ReferenceEquals(line.Entry, anchor));
                firstLine = index + Math.Min(anchorOffset, count - 1);
            }
        }
        layoutKey = key;
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        Rectangle body = tabs.Draw(batch, contentBounds, config, cursor);
        composerBounds = sendBounds = rowsBounds = scrollTrack = scrollThumb = Rectangle.Empty;
        int lineHeight = UiText.LineHeight(config.FontScale);
        bool canCompose = tabs.SelectedId == "Session";
        int composerHeight = canCompose ? lineHeight + 8 : 0;
        int bodyWidth = Math.Max(0, body.Width - Padding * 2);
        int rowsWidth = Math.Max(0, bodyWidth - ScrollbarWidth);
        visibleLines = Math.Max(0, body.Height - Padding * 2 - (canCompose ? Padding + composerHeight : 0)) / lineHeight;
        RefreshLines(rowsWidth, config);
        firstLine = followTail ? MaxFirstLine : Math.Clamp(firstLine, 0, MaxFirstLine);
        rowsBounds = new Rectangle(body.X + Padding, body.Y + Padding, rowsWidth, visibleLines * lineHeight);
        scrollTrack = new Rectangle(rowsBounds.Right + 3, rowsBounds.Y, ScrollbarWidth - 4, rowsBounds.Height);
        if (scrollTrack.Height > 0)
        {
            int height = Math.Min(scrollTrack.Height, Math.Max(16, scrollTrack.Height * visibleLines / Math.Max(1, lines.Count)));
            int y = scrollTrack.Y + (MaxFirstLine == 0 ? 0
                : (int)Math.Round((scrollTrack.Height - height) * (double)firstLine / MaxFirstLine));
            scrollThumb = new Rectangle(scrollTrack.X, y, scrollTrack.Width, height);
            batch.Draw(Game1.staminaRect, scrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, scrollThumb, UiStyle.Muted * config.Opacity);
        }
        if (History.Entries.Count == 0)
            DrawCenteredText(batch, canCompose ? "No messages this session." : "No local dialogue this session.", rowsBounds, UiStyle.Muted, config);
        for (int i = firstLine; i < Math.Min(lines.Count, firstLine + visibleLines); i++)
        {
            var line = lines[i];
            int y = rowsBounds.Y + (i - firstLine) * lineHeight;
            int timestampWidth = (int)MathF.Ceiling(Game1.smallFont.MeasureString(line.Entry.Timestamp).X * config.FontScale);
            string text = UiText.Fit(line.Text, rowsBounds.Width - timestampWidth - Padding - 2, config.FontScale);
            int speakerLength = Math.Min(line.SpeakerLength, text.Length);
            int messageX = rowsBounds.X;
            if (speakerLength > 0)
            {
                string speaker = text[..speakerLength];
                UiText.DrawBold(batch, speaker, new Vector2(rowsBounds.X, y), UiStyle.SpeakerColor(line.Entry.Speaker), config);
                messageX += (int)MathF.Ceiling(Game1.smallFont.MeasureString(speaker).X * config.FontScale) + 1;
            }
            UiText.Draw(batch, text[speakerLength..], new Vector2(messageX, y), UiStyle.Primary, config);
            if (line.First && timestampWidth < rowsBounds.Width)
                UiText.Draw(batch, line.Entry.Timestamp, new Vector2(rowsBounds.Right - timestampWidth - 1, y), UiStyle.Muted, config);
        }

        if (!canCompose || body.Height < composerHeight + Padding * 2 || bodyWidth < 32)
            return;
        int sendWidth = Math.Min(bodyWidth / 3, (int)MathF.Ceiling(Game1.smallFont.MeasureString("Send").X * config.FontScale) + Padding * 2);
        sendBounds = new Rectangle(body.Right - Padding - sendWidth, body.Bottom - Padding - composerHeight, sendWidth, composerHeight);
        composerBounds = new Rectangle(body.X + Padding, sendBounds.Y, Math.Max(0, bodyWidth - sendWidth - Padding), composerHeight);
        composer.Draw(batch, composerBounds, config);
        batch.Draw(Game1.staminaRect, sendBounds, UiStyle.Border * (config.Opacity * (composer.CanSubmit && sendBounds.Contains(cursor) ? 1f : 0.45f)));
        UiStyle.DrawBorder(batch, sendBounds, config.Opacity);
        DrawCenteredText(batch, "Send", sendBounds, composer.CanSubmit ? UiStyle.Primary : UiStyle.Muted, config);
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return;
        string previousTab = tabs.SelectedId;
        if (tabs.ReceiveClick(cursor))
        {
            if (tabs.SelectedId != previousTab)
                ChangeTab(previousTab);
            return;
        }
        if (tabs.SelectedId == "Session" && (composerBounds.Contains(cursor) || sendBounds.Contains(cursor)))
        {
            Game1.keyboardDispatcher.Subscriber = composer;
            if (sendBounds.Contains(cursor))
                composer.Submit();
        }
        else if (MaxFirstLine > 0 && !scrollThumb.IsEmpty && scrollTrack.Contains(cursor))
        {
            scrollDragOffset = scrollThumb.Contains(cursor) ? cursor.Y - scrollThumb.Y : scrollThumb.Height / 2;
            DragContent(cursor);
        }
    }

    internal void SelectTab(string id)
    {
        string previousTab = tabs.SelectedId;
        tabs.Select(id);
        if (tabs.SelectedId != previousTab)
            ChangeTab(previousTab);
    }

    private void ChangeTab(string previousTab)
    {
        scrollPositions[previousTab] = (firstLine, followTail);
        (firstLine, followTail) = scrollPositions.TryGetValue(tabs.SelectedId, out var position) ? position : (0, true);
        lines.Clear();
        layoutKey = null;
        composerBounds = sendBounds = rowsBounds = scrollTrack = scrollThumb = Rectangle.Empty;
        visibleLines = 0;
        composer.Selected = false;
        EndContentDrag();
    }

    internal override void DragContent(Point cursor)
    {
        if (scrollDragOffset.HasValue && scrollTrack.Height > scrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - scrollDragOffset.Value - scrollTrack.Y) / (scrollTrack.Height - scrollThumb.Height);
            firstLine = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxFirstLine);
            followTail = firstLine == MaxFirstLine;
        }
    }

    internal override void EndContentDrag() => scrollDragOffset = null;
    internal override void ReceiveScroll(int delta)
    {
        if (delta == 0)
            return;
        firstLine = (int)Math.Clamp((long)firstLine - Math.Sign(delta) * Math.Max(1L, Math.Abs((long)delta) / 120) * 3, 0, MaxFirstLine);
        followTail = firstLine == MaxFirstLine;
    }
}
