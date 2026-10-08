using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

// Desktop mail viewer: the list stays on top and the selected letter reads in
// the pane below. Selection never pops the vanilla letter viewer, so the
// window works while the game is paused or another menu has focus.
internal sealed class MailWindow : OsWindow
{
    internal const string WindowId = "Mail";
    private const int Padding = GridView<MailRow>.Padding;
    private const int IconSize = 20;
    private const int ScrollbarWidth = 10;
    private const int Divider = 4;
    private readonly GridView<MailRow> grid = new(
        GridColumn<MailRow>.Create("Title", 200, 44, row => row.Title, row => row.Title,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawTitleCell, id: "title"),
        GridColumn<MailRow>.Create("Body", 360, 60, row => row.Preview, row => row.Preview,
            textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase, id: "body"));

    private string? selectedId;
    private Rectangle listBounds = Rectangle.Empty;
    private Rectangle previewBounds = Rectangle.Empty;
    private Rectangle previewRowsBounds = Rectangle.Empty;
    private Rectangle previewScrollTrack = Rectangle.Empty;
    private Rectangle previewScrollThumb = Rectangle.Empty;
    private readonly List<string> previewLines = new();
    private string previewTitle = "";
    private int previewFirstLine;
    private int previewVisibleLines;
    private int? previewScrollDragOffset;
    private (string Id, int Width, float Scale, SpriteFont Font, Season Season, int Day)? previewKey;
    private int MaxPreviewFirstLine => Math.Max(0, previewLines.Count - previewVisibleLines);

    internal override string Id => WindowId;
    internal override string Title => "Mail";
    internal override string TitleSubtext
    {
        get
        {
            int count = GetRows().Length;
            return count == 1 ? "1 letter" : $"{count} letters";
        }
    }
    internal override int Icon => UiStyle.MailIcon;
    internal override Point DefaultSize => new(600, 480);

    protected override Point GetMinimumSize(ModConfig config)
    {
        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int lineHeight = UiText.LineHeight(config.FontScale);
        int listMin = Padding * 2 + rowHeight * 3 + 1;
        int previewMin = Padding * 2 + lineHeight + 4 + lineHeight * 3;
        return new(320, TitleHeight + 2 + listMin + Divider + previewMin);
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        MailRow[] rows = GetRows();
        grid.SetRows(rows);
        if (rows.Length == 0)
            selectedId = null;
        else if (selectedId == null || rows.All(row => row.Id != selectedId))
            selectedId = grid.Rows.Count > 0 ? grid.Rows[0].Id : rows[0].Id;

        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int lineHeight = UiText.LineHeight(config.FontScale);
        int listMin = Padding * 2 + rowHeight * 3 + 1;
        int previewMin = Padding * 2 + lineHeight + 4 + lineHeight * 2;
        listBounds = previewBounds = previewRowsBounds = previewScrollTrack = previewScrollThumb = Rectangle.Empty;
        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return;
        if (contentBounds.Height < listMin + Divider + previewMin)
        {
            listBounds = contentBounds;
        }
        else
        {
            int listHeight = (contentBounds.Height - Divider) * 55 / 100;
            listHeight = Math.Clamp(listHeight, listMin, contentBounds.Height - Divider - previewMin);
            listBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, listHeight);
            previewBounds = new Rectangle(contentBounds.X, contentBounds.Y + listHeight + Divider,
                contentBounds.Width, contentBounds.Height - listHeight - Divider);
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        grid.Draw(batch, listBounds, rowHeight, config, cursor, "No mail.",
            isSelected: row => row.Id == selectedId);
        if (previewBounds.IsEmpty)
            return;
        batch.Draw(Game1.staminaRect, new Rectangle(contentBounds.X, listBounds.Bottom + 1,
            contentBounds.Width, 1), UiStyle.Border * config.Opacity);
        DrawPreview(batch, rows, cursor, config);
    }

    private void DrawPreview(SpriteBatch batch, MailRow[] rows, Point cursor, ModConfig config)
    {
        previewRowsBounds = previewScrollTrack = previewScrollThumb = Rectangle.Empty;
        if (rows.Length == 0)
        {
            DrawCenteredText(batch, "No mail.", previewBounds, UiStyle.Muted, config);
            return;
        }
        MailRow? selected = rows.FirstOrDefault(row => row.Id == selectedId);
        if (selected == null)
        {
            DrawCenteredText(batch, "Select a letter.", previewBounds, UiStyle.Muted, config);
            return;
        }

        int lineHeight = UiText.LineHeight(config.FontScale);
        int rowWidth = Math.Max(0, previewBounds.Width - Padding * 2 - ScrollbarWidth);
        int textWidth = rowWidth;
        RefreshPreview(selected, textWidth, config);
        int headerHeight = lineHeight + 4;
        int availableHeight = Math.Max(0, previewBounds.Height - Padding * 2 - headerHeight - 4);
        previewVisibleLines = availableHeight / Math.Max(1, lineHeight);
        previewFirstLine = Math.Clamp(previewFirstLine, 0, MaxPreviewFirstLine);
        previewRowsBounds = new Rectangle(previewBounds.X + Padding,
            previewBounds.Y + Padding + headerHeight + 4, rowWidth, previewVisibleLines * lineHeight);
        previewScrollTrack = new Rectangle(previewRowsBounds.Right + 3, previewRowsBounds.Y,
            ScrollbarWidth - 4, previewRowsBounds.Height);
        if (MaxPreviewFirstLine > 0 && previewScrollTrack.Height > 0)
        {
            int height = Math.Min(previewScrollTrack.Height,
                Math.Max(16, previewScrollTrack.Height * previewVisibleLines / Math.Max(1, previewLines.Count)));
            int y = previewScrollTrack.Y + (int)Math.Round(
                (previewScrollTrack.Height - height) * (double)previewFirstLine / MaxPreviewFirstLine);
            previewScrollThumb = new Rectangle(previewScrollTrack.X, y, previewScrollTrack.Width, height);
            batch.Draw(Game1.staminaRect, previewScrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, previewScrollThumb, UiStyle.Muted * config.Opacity);
        }

        string title = UiText.Fit(previewTitle, Math.Max(0, previewBounds.Width - Padding * 2 - 1), config.FontScale);
        if (!string.IsNullOrEmpty(title) && lineHeight + 1 <= headerHeight)
            UiText.DrawBold(batch, title, new Vector2(previewBounds.X + Padding, previewBounds.Y + Padding),
                UiStyle.Primary, config);
        for (int i = previewFirstLine; i < Math.Min(previewLines.Count, previewFirstLine + previewVisibleLines); i++)
        {
            string line = previewLines[i];
            if (string.IsNullOrEmpty(line))
                continue;
            int y = previewRowsBounds.Y + (i - previewFirstLine) * lineHeight;
            UiText.Draw(batch, line, new Vector2(previewRowsBounds.X, y), UiStyle.Primary, config);
        }
    }

    private void RefreshPreview(MailRow selected, int textWidth, ModConfig config)
    {
        var key = (selected.Id, textWidth, config.FontScale, Game1.smallFont, Game1.season, Game1.dayOfMonth);
        if (previewKey.Equals(key))
            return;
        // Keep the scroll position when only the width changes; reset it when the
        // selection changes.
        bool selectionChanged = !previewKey.HasValue || previewKey.Value.Id != key.Id;
        int previousFirstLine = previewFirstLine;
        previewKey = key;
        previewLines.Clear();
        (previewTitle, List<string> paragraphs) = GetLetter(selected.Id);
        if (textWidth <= 0)
        {
            previewFirstLine = 0;
            return;
        }
        foreach (string paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                previewLines.Add("");
                continue;
            }
            foreach (string line in UiText.Wrap(paragraph, Math.Max(0, textWidth - 1), config.FontScale))
                previewLines.Add(line);
            previewLines.Add("");
        }
        while (previewLines.Count > 0 && previewLines[^1] == "")
            previewLines.RemoveAt(previewLines.Count - 1);
        previewFirstLine = selectionChanged ? 0 : previousFirstLine;
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (grid.ReceiveClick(cursor, leftClick))
            return;
        if (!leftClick)
            return;
        if (!listBounds.IsEmpty && listBounds.Contains(cursor))
        {
            MailRow? row = grid.GetRow(cursor);
            if (row != null && row.Id != selectedId)
            {
                selectedId = row.Id;
                previewFirstLine = 0;
                previewScrollDragOffset = null;
            }
            return;
        }
        if (!previewScrollThumb.IsEmpty && previewScrollTrack.Contains(cursor))
        {
            previewScrollDragOffset = previewScrollThumb.Contains(cursor)
                ? cursor.Y - previewScrollThumb.Y : previewScrollThumb.Height / 2;
            DragContent(cursor);
        }
    }

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor) => grid.GetHeaderContextMenuItems(cursor);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);
    internal override void DragContent(Point cursor)
    {
        grid.Drag(cursor);
        if (previewScrollDragOffset.HasValue && previewScrollTrack.Height > previewScrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - previewScrollDragOffset.Value - previewScrollTrack.Y)
                / (previewScrollTrack.Height - previewScrollThumb.Height);
            previewFirstLine = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxPreviewFirstLine);
        }
    }
    internal override void EndContentDrag()
    {
        grid.EndDrag();
        previewScrollDragOffset = null;
    }
    internal override void ReceiveScroll(int delta)
    {
        if (delta == 0)
            return;
        Point cursor;
        try { cursor = new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true)); }
        catch { return; }
        if (!listBounds.IsEmpty && listBounds.Contains(cursor))
        {
            grid.ReceiveScroll(delta);
            return;
        }
        if (!previewBounds.IsEmpty && previewBounds.Contains(cursor))
        {
            int steps = Math.Max(1, Math.Abs(delta) / 120);
            previewFirstLine = (int)Math.Clamp((long)previewFirstLine - Math.Sign(delta) * steps * 3, 0, MaxPreviewFirstLine);
        }
    }

    // Mirrors CollectionsPage's letters tab: received IDs with mail data, in received
    // order. The title is the "[#]" suffix ("???" when absent); the body is the prefix.
    private static MailRow[] GetRows()
    {
        if (!Context.IsWorldReady)
            return Array.Empty<MailRow>();
        Dictionary<string, string> mail = DataLoader.Mail(Game1.content);
        string playerName = Game1.player.Name;
        Gender gender = Game1.player.Gender;
        var rows = new List<MailRow>();
        foreach (string id in Game1.player.mailReceived)
        {
            if (!mail.TryGetValue(id, out string? raw))
                continue;
            string[] parts = raw.Split("[#]");
            string title = parts.Length > 1 ? parts[1] : "???";
            rows.Add(new MailRow(id, title, GetPreview(parts[0], playerName, gender)));
        }
        return rows.ToArray();
    }

    // Full letter text, parsed like LetterViewerMenu with fromCollection:true:
    // tokens resolved, custom formatting and item/action commands stripped, line
    // breaks kept as paragraphs. Interactables are already claimed on first read,
    // so the pane is text-only just like the vanilla collection viewer.
    private static (string Title, List<string> Paragraphs) GetLetter(string id)
    {
        var empty = new List<string>();
        if (!Context.IsWorldReady)
            return ("", empty);
        Dictionary<string, string> mail = DataLoader.Mail(Game1.content);
        if (!mail.TryGetValue(id, out string? raw))
            return ("???", empty);
        string[] parts = raw.Split("[#]");
        string title = parts.Length > 1 ? parts[1] : "???";
        string body = parts[0].Replace("@", Game1.player.Name);
        body = Dialogue.applyGenderSwitch(Game1.player.Gender, body, altTokenOnly: true);
        body = StripCustomFormatting(body);
        body = StripCommands(body, "%action");
        body = StripCommands(body, "%item");
        if (body.Contains("%secretsanta", StringComparison.Ordinal))
        {
            string replacement = "???";
            try
            {
                if (Game1.season == Season.Winter && Game1.dayOfMonth >= 18 && Game1.dayOfMonth <= 25)
                    replacement = Utility.GetRandomWinterStarParticipant().displayName;
            }
            catch { }
            body = body.Replace("%secretsanta", replacement, StringComparison.Ordinal);
        }
        var paragraphs = new List<string>();
        foreach (string part in body.Split('^'))
            paragraphs.Add(string.Join(" ", part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        while (paragraphs.Count > 0 && paragraphs[0] == "")
            paragraphs.RemoveAt(0);
        while (paragraphs.Count > 0 && paragraphs[^1] == "")
            paragraphs.RemoveAt(paragraphs.Count - 1);
        return (title, paragraphs);
    }

    // Single-line preview of what the vanilla viewer shows: player/gender tokens
    // resolved, custom formatting and item/action commands stripped, line breaks
    // flattened. Secretsanta stays "???" for a stable sort outside Dec 18-25.
    private static string GetPreview(string body, string playerName, Gender gender)
    {
        body = body.Replace("@", playerName);
        body = Dialogue.applyGenderSwitch(gender, body, altTokenOnly: true);
        body = StripCustomFormatting(body);
        body = StripCommands(body, "%action");
        body = StripCommands(body, "%item");
        body = body.Replace("%secretsanta", "???").Replace('^', ' ');
        return string.Join(" ", body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string StripCommands(string text, string command)
    {
        int start = text.IndexOf(command, StringComparison.Ordinal);
        while (start >= 0)
        {
            int end = text.IndexOf("%%", start, StringComparison.Ordinal);
            if (end < 0)
                break;
            text = text.Remove(start, end + 2 - start);
            start = text.IndexOf(command, StringComparison.Ordinal);
        }
        return text;
    }

    // Mirrors LetterViewerMenu.ApplyCustomFormatting tag removal ([letterbg ...]
    // and [textcolor ...]) without touching viewer background or text colors.
    private static string StripCustomFormatting(string text)
    {
        for (int start = text.IndexOf('['); start >= 0; start = text.IndexOf('[', start + 1))
        {
            int end = text.IndexOf(']', start);
            if (end < 0)
                break;
            string first = text.Substring(start + 1, end - start - 1).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (first == "letterbg" || first == "textcolor")
            {
                text = text.Remove(start, end - start + 1);
                start--;
            }
        }
        return text;
    }

    private static void DrawTitleCell(SpriteBatch batch, MailRow row, Rectangle bounds, ModConfig config)
    {
        // Vanilla letters-tab envelope: Game1.mouseCursors (190, 423, 14, 11).
        Rectangle source = new(190, 423, 14, 11);
        float scale = Math.Min((float)IconSize / source.Width, (float)IconSize / source.Height);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + Padding + (IconSize - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
        batch.Draw(Game1.mouseCursors, destination, source, Color.White * config.Opacity);
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.Title, bounds, UiStyle.Primary, config);
    }

    private sealed class MailRow
    {
        internal string Id { get; }
        internal string Title { get; }
        internal string Preview { get; }

        internal MailRow(string id, string title, string preview)
        {
            Id = id;
            Title = title;
            Preview = preview;
        }
    }
}
