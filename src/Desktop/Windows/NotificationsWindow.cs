using AgrariaOS.Configuration;
using AgrariaOS.Notifications;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

internal sealed class NotificationsWindow : OsWindow
{
    internal const string WindowId = "Notifications";
    private const int Padding = 8;
    private const int ScrollbarWidth = 10;
    private readonly NotificationHistory history;
    private readonly List<(NotificationEntry Entry, string Text, Color Color)> lines = new();
    private readonly List<(NotificationEntry Entry, Rectangle Bounds)> dismissButtons = new();
    private Rectangle clearBounds;
    private Rectangle rowsBounds;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private int firstLine;
    private int visibleLines;
    private int? scrollDragOffset;
    private (int Count, int RemovalVersion, int Width, float Scale, SpriteFont Font, bool Clock)? layoutKey;

    internal NotificationsWindow(NotificationHistory history) => this.history = history;
    internal override string Id => WindowId;
    internal override string Title => "Notifications";
    internal override int Icon => UiStyle.NotificationIcon;
    internal override Point DefaultSize => new(580, 420);
    private int MaxFirstLine => Math.Max(0, lines.Count - visibleLines);

    protected override Point GetMinimumSize(ModConfig config)
        => new(280, TitleHeight + Padding * 2 + UiText.LineHeight(config.FontScale) * 4);

    private void RefreshLines(int textWidth, ModConfig config)
    {
        var key = (history.Entries.Count, history.RemovalVersion, textWidth, config.FontScale, Game1.smallFont, config.Use24HourClock);
        if (layoutKey != key)
        {
            bool arrivalsOnly = layoutKey.HasValue && layoutKey.Value.RemovalVersion == history.RemovalVersion
                && layoutKey.Value.Width == textWidth
                && layoutKey.Value.Scale == config.FontScale && ReferenceEquals(layoutKey.Value.Font, Game1.smallFont)
                && layoutKey.Value.Clock == config.Use24HourClock;
            // Wrap only the new arrivals; a long session shouldn't re-measure its entire history.
            int oldestNewEntry = arrivalsOnly ? layoutKey!.Value.Count : 0;
            var newLines = new List<(NotificationEntry Entry, string Text, Color Color)>();
            for (int i = history.Entries.Count - 1; i >= oldestNewEntry; i--)
            {
                NotificationEntry entry = history.Entries[i];
                foreach (string line in UiText.Wrap(entry.Timestamp(config.Use24HourClock), textWidth - 1, config.FontScale))
                    newLines.Add((entry, line, UiStyle.Muted));
                foreach (string line in UiText.Wrap(entry.Text, textWidth - 1, config.FontScale))
                    newLines.Add((entry, line, NotificationHistory.GetColor(entry.Kind)));
                newLines.Add((entry, "", UiStyle.Border));
            }
            if (!arrivalsOnly)
                lines.Clear();
            lines.InsertRange(0, newLines);
            // Keep older messages anchored when new arrivals prepend to a scrolled history.
            if (arrivalsOnly && firstLine > 0)
                firstLine += newLines.Count;
            layoutKey = key;
        }
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        int lineHeight = UiText.LineHeight(config.FontScale);
        int dismissSize = Math.Max(18, lineHeight);
        int rowWidth = Math.Max(0, contentBounds.Width - Padding * 2 - ScrollbarWidth);
        int textWidth = Math.Max(0, rowWidth - dismissSize - Padding);
        RefreshLines(textWidth, config);
        int buttonHeight = lineHeight + 4;
        int availableHeight = Math.Max(0, contentBounds.Height - Padding * 3 - buttonHeight);
        visibleLines = availableHeight / lineHeight;
        firstLine = Math.Clamp(firstLine, 0, MaxFirstLine);
        rowsBounds = new Rectangle(contentBounds.X + Padding, contentBounds.Y + Padding, rowWidth, visibleLines * lineHeight);
        scrollTrack = new Rectangle(rowsBounds.Right + 3, rowsBounds.Y, ScrollbarWidth - 4, rowsBounds.Height);
        scrollThumb = Rectangle.Empty;
        if (MaxFirstLine > 0 && scrollTrack.Height > 0)
        {
            int height = Math.Min(scrollTrack.Height, Math.Max(16, scrollTrack.Height * visibleLines / lines.Count));
            int y = scrollTrack.Y + (int)Math.Round((scrollTrack.Height - height) * (double)firstLine / MaxFirstLine);
            scrollThumb = new Rectangle(scrollTrack.X, y, scrollTrack.Width, height);
            batch.Draw(Game1.staminaRect, scrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, scrollThumb, UiStyle.Muted * config.Opacity);
        }

        if (history.Entries.Count == 0)
            DrawCenteredText(batch, "No notifications this session.", rowsBounds, UiStyle.Muted, config);
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        DrawDismissButtons(batch, cursor, lineHeight, dismissSize, config);
        for (int i = firstLine; i < Math.Min(lines.Count, firstLine + visibleLines); i++)
        {
            var line = lines[i];
            int y = rowsBounds.Y + (i - firstLine) * lineHeight;
            if (line.Text.Length == 0)
                batch.Draw(Game1.staminaRect, new Rectangle(rowsBounds.X, y + lineHeight / 2, rowsBounds.Width, 1), UiStyle.Border * config.Opacity);
            else
                UiText.Draw(batch, line.Text, new Vector2(rowsBounds.X, y), line.Color, config);
        }
        int buttonWidth = Math.Min(rowWidth, (int)MathF.Ceiling(Game1.smallFont.MeasureString("Clear").X * config.FontScale) + Padding * 2);
        clearBounds = new Rectangle(contentBounds.X + Padding, contentBounds.Bottom - Padding - buttonHeight, buttonWidth, buttonHeight);
        bool canClear = history.Entries.Count > 0;
        batch.Draw(Game1.staminaRect, clearBounds, UiStyle.Border * (config.Opacity * (canClear && clearBounds.Contains(cursor) ? 1f : 0.45f)));
        UiStyle.DrawBorder(batch, clearBounds, config.Opacity);
        DrawCenteredText(batch, "Clear", clearBounds, canClear ? UiStyle.Primary : UiStyle.Muted, config);
        if (visibleLines > 0 && textWidth > 0)
            history.MarkRead();
    }

    private void DrawDismissButtons(SpriteBatch batch, Point cursor, int lineHeight, int dismissSize, ModConfig config)
    {
        dismissButtons.Clear();
        int end = Math.Min(lines.Count, firstLine + visibleLines);
        for (int start = firstLine; start < end;)
        {
            NotificationEntry entry = lines[start].Entry;
            int next = start + 1;
            while (next < end && ReferenceEquals(lines[next].Entry, entry))
                next++;
            int contentEnd = lines[next - 1].Text.Length == 0 ? next - 1 : next;
            Rectangle row = new(rowsBounds.X, rowsBounds.Y + (start - firstLine) * lineHeight,
                rowsBounds.Width, (contentEnd - start) * lineHeight);
            if (row.Contains(cursor))
            {
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.3f));
                Rectangle button = new(row.Right - dismissSize, row.Y, dismissSize, lineHeight);
                dismissButtons.Add((entry, button));
                if (button.Contains(cursor))
                    batch.Draw(Game1.staminaRect, button, UiStyle.Border * config.Opacity);
                DrawCenteredText(batch, "x", button, UiStyle.Primary, config);
            }
            start = next;
        }
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return;
        if (clearBounds.Contains(cursor))
        {
            history.Clear();
            firstLine = 0;
            dismissButtons.Clear();
            EndContentDrag();
            return;
        }
        foreach (var button in dismissButtons)
        {
            if (!button.Bounds.Contains(cursor))
                continue;
            int start = lines.FindIndex(line => ReferenceEquals(line.Entry, button.Entry));
            if (history.Remove(button.Entry))
                firstLine = Math.Min(firstLine, Math.Max(0, start));
            dismissButtons.Clear();
            EndContentDrag();
            return;
        }
        if (!scrollThumb.IsEmpty && scrollTrack.Contains(cursor))
        {
            scrollDragOffset = scrollThumb.Contains(cursor) ? cursor.Y - scrollThumb.Y : scrollThumb.Height / 2;
            DragContent(cursor);
        }
    }

    internal override void DragContent(Point cursor)
    {
        if (scrollDragOffset.HasValue && scrollTrack.Height > scrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - scrollDragOffset.Value - scrollTrack.Y) / (scrollTrack.Height - scrollThumb.Height);
            firstLine = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxFirstLine);
        }
    }

    internal override void EndContentDrag() => scrollDragOffset = null;
    internal override void ReceiveScroll(int delta)
    {
        if (delta != 0)
            firstLine = (int)Math.Clamp((long)firstLine - Math.Sign(delta) * Math.Max(1L, Math.Abs((long)delta) / 120) * 3, 0, MaxFirstLine);
    }
}
