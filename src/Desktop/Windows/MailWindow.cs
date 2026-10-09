using AgrariaOS.Configuration;
using AgrariaOS.Mail;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

// Desktop mail viewer: the list stays on top and the selected letter reads in
// the pane below. Explicitly clicking an unread row processes the delivery;
// drawing and previewing the default selection never grant rewards.
internal sealed partial class MailWindow : OsWindow
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
            textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase, id: "body"),
        GridColumn<MailRow>.Create("Received", 110, 50, row => row.Record.Date, row => row.Record.DateOrder,
            textColor: UiStyle.Muted, id: "received"));

    private readonly TreeView tree = new();
    private Rectangle treeBounds;
    private Rectangle attachmentsBounds;
    private readonly List<(Rectangle Bounds, MailAttachment Attachment)> attachmentButtons = new();
    private string? selectedId;

    internal MailWindow()
    {
        var all = new TreeNode("all", "All");
        all.Children.Add(new TreeNode("inbox", "Inbox"));
        all.Children.Add(new TreeNode("updates", "Updates"));
        tree.SetRoots(new[] { all });
        tree.Select("all");
    }
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
            int count = MailArchive.Active?.Letters.Count ?? 0;
            int unread = MailArchive.Active?.UnreadCount ?? 0;
            string total = count == 1 ? "1 letter" : $"{count} letters";
            return unread > 0 ? $"{total} · {unread} unread" : total;
        }
    }
    internal override int Icon => UiStyle.MailIcon;
    internal override Point DefaultSize => new(760, 480);

    protected override Point GetMinimumSize(ModConfig config)
    {
        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int lineHeight = UiText.LineHeight(config.FontScale);
        int listMin = Padding * 2 + rowHeight * 3 + 1;
        int previewMin = Padding * 2 + lineHeight + 4 + lineHeight * 3;
        return new(480, TitleHeight + 2 + listMin + Divider + previewMin);
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        MailRow[] rows = GetRows();
        grid.SetRows(rows);
        if (rows.Length == 0)
            selectedId = null;
        else if (selectedId == null || rows.All(row => row.Id != selectedId))
            selectedId = rows[0].Id;

        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int lineHeight = UiText.LineHeight(config.FontScale);
        int listMin = Padding * 2 + rowHeight * 3 + 1;
        int previewMin = Padding * 2 + lineHeight + 4 + lineHeight * 2;
        listBounds = previewBounds = previewRowsBounds = previewScrollTrack = previewScrollThumb = attachmentsBounds = Rectangle.Empty;
        attachmentButtons.Clear();
        previousAttachmentBounds = nextAttachmentBounds = Rectangle.Empty;
        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return;
        int treeWidth = Math.Clamp(contentBounds.Width / 5, 110, 160);
        treeBounds = new Rectangle(contentBounds.X, contentBounds.Y, treeWidth, contentBounds.Height);
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        tree.Draw(batch, treeBounds, rowHeight, config, cursor, "");
        batch.Draw(Game1.staminaRect, new Rectangle(treeBounds.Right + 1, contentBounds.Y, 1, contentBounds.Height), UiStyle.Border * config.Opacity);
        contentBounds = new Rectangle(treeBounds.Right + Divider, contentBounds.Y,
            Math.Max(0, contentBounds.Width - treeWidth - Divider), contentBounds.Height);
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
        int attachmentHeight = selected.Record.Attachments.Count > 0 || selected.Record.HasItemCommand
            ? Math.Max(26, lineHeight + Padding * 2) : 0;
        if (attachmentHeight > 0)
        {
            attachmentsBounds = new Rectangle(previewBounds.X, previewBounds.Bottom - attachmentHeight,
                previewBounds.Width, attachmentHeight);
            DrawAttachments(batch, selected.Record, config, cursor);
        }
        int availableHeight = Math.Max(0, previewBounds.Height - Padding * 2 - headerHeight - 4 - attachmentHeight);
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
        previewTitle = selected.Title;
        List<string> paragraphs = selected.Record.Body.Split('^')
            .Select(part => string.Join(" ", part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))).ToList();
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
        if (treeBounds.Contains(cursor) && tree.ReceiveClick(cursor, leftClick))
        {
            selectedId = null;
            previewKey = null;
            grid.Clear();
            return;
        }
        if (leftClick)
        {
            if (previousAttachmentBounds.Contains(cursor)) { firstAttachment = Math.Max(0, firstAttachment - 1); return; }
            if (nextAttachmentBounds.Contains(cursor)) { firstAttachment++; return; }
            foreach (var button in attachmentButtons)
            {
                if (!button.Bounds.Contains(cursor)) continue;
                MailArchive.Active?.Claim(button.Attachment);
                return;
            }
        }
        if (grid.ReceiveClick(cursor, leftClick))
            return;
        if (!leftClick)
            return;
        if (!listBounds.IsEmpty && listBounds.Contains(cursor))
        {
            MailRow? row = grid.GetRow(cursor);
            if (row != null)
            {
                selectedId = row.Id;
                firstAttachment = 0;
                if (row.Record.Pending)
                {
                    MailArchive.Active?.Open(row.Record);
                    previewKey = null;
                }
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
        tree.Drag(cursor);
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
        tree.EndDrag();
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
        if (treeBounds.Contains(cursor))
        {
            tree.ReceiveScroll(delta);
            return;
        }
        if (attachmentsBounds.Contains(cursor))
        {
            firstAttachment = Math.Max(0, firstAttachment - Math.Sign(delta));
            return;
        }
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

    private MailRow[] GetRows()
    {
        IEnumerable<MailRecord> letters = MailArchive.Active?.Letters ?? Array.Empty<MailRecord>();
        if (tree.SelectedId == "inbox") letters = letters.Where(letter => !letter.Updates);
        if (tree.SelectedId == "updates") letters = letters.Where(letter => letter.Updates);
        return letters.Reverse().Select(letter => new MailRow(letter)).ToArray();
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
        if (row.Record.Pending)
            UiStyle.DrawIcon(batch, UiStyle.UnreadMailIcon, new Point(destination.Right - 5, destination.Y - 2), 1, config.Opacity);
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.Title, bounds, UiStyle.Primary, config);
    }

    private sealed class MailRow
    {
        internal MailRecord Record { get; }
        internal string Id => Record.Id;
        internal string Title => Record.Title;
        internal string Preview => string.Join(" ", Record.Body.Replace('^', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        internal MailRow(MailRecord record) => Record = record;
    }
}
