using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

// Player notes and to-do lists. The left tree is a flat list (roots without
// children); the right pane edits the selected document with the shared
// TextEntry control. To-do items toggle on click and offer a hover delete.
internal sealed class NotesWindow : OsWindow
{
    internal const string WindowId = "Notes";
    internal const int NotesIcon = 139;
    private const int Padding = 8;

    private readonly ModConfig config;
    private readonly Action saveConfig;
    private readonly Action<Point, ContextMenuItem[]> showContextMenu;
    private readonly TreeView tree = new();
    private readonly TextEntry titleEntry = new() { Placeholder = "Title..." };
    private readonly TextEntry bodyEntry = new() { Placeholder = "Write a note...", Multiline = true };
    private readonly TextEntry addEntry = new() { Placeholder = "Add new item..." };
    private long farmerId;
    private string? boundDocId;
    private bool boundIsTodo;
    private Rectangle addButtonBounds = Rectangle.Empty;
    private Rectangle treeBounds = Rectangle.Empty;
    private Rectangle detailBounds = Rectangle.Empty;
    private Rectangle titleBounds = Rectangle.Empty;
    private Rectangle bodyBounds = Rectangle.Empty;
    private Rectangle newItemBounds = Rectangle.Empty;
    private Rectangle newItemButtonBounds = Rectangle.Empty;
    private Rectangle itemsBounds = Rectangle.Empty;
    private Rectangle itemScrollTrack = Rectangle.Empty;
    private Rectangle itemScrollThumb = Rectangle.Empty;
    private readonly List<(TodoRecord Item, Rectangle Row, Rectangle Delete)> drawnItems = new();
    private int firstItem;
    private int lastItemCount;
    private int lastVisibleItems = 1;
    private int? itemScrollDragOffset;
    private int rowHeight;

    internal NotesWindow(ModConfig config, Action saveConfig, Action<Point, ContextMenuItem[]> showContextMenu)
    {
        this.config = config;
        this.saveConfig = saveConfig;
        this.showContextMenu = showContextMenu;
        titleEntry.Changed = text =>
        {
            NoteRecord? doc = SelectedDoc();
            if (doc != null && doc.Id == boundDocId)
            {
                doc.Title = text;
                saveConfig();
            }
        };
        bodyEntry.Changed = text =>
        {
            NoteRecord? doc = SelectedDoc();
            if (doc != null && doc.Id == boundDocId && doc.Kind != "todo")
            {
                doc.Body = text;
                saveConfig();
            }
        };
        titleEntry.Submitted = () => Focus(boundIsTodo ? addEntry : bodyEntry);
        addEntry.Submitted = AddTodoItem;
    }

    internal override string Id => WindowId;
    internal override string Title => "Notes";
    internal override string TitleSubtext
    {
        get
        {
            try
            {
                List<NoteRecord> docs = Docs();
                int lists = docs.Count(doc => doc.Kind == "todo");
                int notes = docs.Count - lists;
                return $"{notes} note{(notes == 1 ? "" : "s")} | {lists} list{(lists == 1 ? "" : "s")}";
            }
            catch
            {
                return "";
            }
        }
    }
    internal override int Icon => NotesIcon;
    internal override Point DefaultSize => new(640, 440);

    protected override Point GetMinimumSize(ModConfig config)
        => new(360, TitleHeight + 2 + Padding * 2 + (UiText.LineHeight(config.FontScale) + 8) * 4);

    // The game clears the dispatcher subscriber every tick outside menus (which also
    // flips Selected off), so focus is tracked explicitly and re-subscribed by the
    // manager each tick, like the chat composer. Never derive this from Selected.
    private TextEntry? focusedEntry;
    internal override IKeyboardSubscriber? KeyboardSubscriber => focusedEntry;

    internal override void ReceiveKey(Keys key)
    {
        focusedEntry?.Navigate(key);
    }

    internal override bool IsTextFieldAt(Point cursor)
        => titleBounds.Contains(cursor) || bodyBounds.Contains(cursor) || newItemBounds.Contains(cursor);

    internal override void Close()
    {
        saveConfig();
        UnfocusAll();
    }

    private List<NoteRecord> Docs()
    {
        if (!Context.IsWorldReady || Game1.player == null)
            return new List<NoteRecord>();
        long id = Game1.player.UniqueMultiplayerID;
        if (id != farmerId)
        {
            farmerId = id;
            boundDocId = null;
            firstItem = 0;
            UnfocusAll();
        }
        if (!config.Notes.TryGetValue(id, out List<NoteRecord>? docs) || docs == null)
            config.Notes[id] = docs = new List<NoteRecord>();
        return docs;
    }

    private void UnfocusAll()
    {
        focusedEntry = null;
        titleEntry.Selected = bodyEntry.Selected = addEntry.Selected = false;
    }

    private void Focus(TextEntry entry)
    {
        UnfocusAll();
        focusedEntry = entry;
        entry.Selected = true;
        try { Game1.keyboardDispatcher.Subscriber = entry; }
        catch { }
    }

    private NoteRecord? SelectedDoc()
    {
        string? id = tree.SelectedId;
        if (id == null || !Context.IsWorldReady || Game1.player == null)
            return null;
        if (!config.Notes.TryGetValue(Game1.player.UniqueMultiplayerID, out List<NoteRecord>? docs) || docs == null)
            return null;
        return docs.FirstOrDefault(doc => doc.Id == id);
    }

    private void EnsureBound(NoteRecord? doc)
    {
        string? id = doc?.Id;
        bool isTodo = doc?.Kind == "todo";
        if (id == boundDocId && isTodo == boundIsTodo)
            return;
        boundDocId = id;
        boundIsTodo = isTodo;
        UnfocusAll();
        firstItem = 0;
        itemScrollDragOffset = null;
        titleEntry.Bind(doc?.Title ?? "");
        bodyEntry.Bind(doc?.Body ?? "");
        addEntry.Bind("");
    }

    private static List<TreeNode> BuildRoots(List<NoteRecord> docs)
    {
        var roots = new List<TreeNode>(docs.Count);
        foreach (NoteRecord doc in docs)
        {
            bool isTodo = doc.Kind == "todo";
            string title = string.IsNullOrWhiteSpace(doc.Title)
                ? isTodo ? "Untitled list" : "Untitled note" : doc.Title.Trim();
            if (!isTodo)
            {
                roots.Add(new TreeNode(doc.Id, title));
                continue;
            }
            int total = doc.Items.Count;
            int done = doc.Items.Count(item => item.Done);
            string label = total == 0 ? title : $"{title} ({done}/{total})";
            roots.Add(new TreeNode(doc.Id, label, total > 0 && done == total));
        }
        return roots;
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        List<NoteRecord> docs = Docs();
        tree.SetRoots(BuildRoots(docs));
        tree.EnsureSelection();
        NoteRecord? doc = tree.SelectedId == null ? null : docs.FirstOrDefault(candidate => candidate.Id == tree.SelectedId);
        EnsureBound(doc);
        Point cursor;
        try { cursor = new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true)); }
        catch { cursor = Point.Zero; }

        addButtonBounds = treeBounds = detailBounds = titleBounds = bodyBounds
            = newItemBounds = newItemButtonBounds = itemsBounds
            = itemScrollTrack = itemScrollThumb = Rectangle.Empty;
        drawnItems.Clear();

        int lineHeight = UiText.LineHeight(config.FontScale);
        rowHeight = lineHeight + 6;
        int headerHeight = lineHeight + 10;
        int addWidth = (int)MathF.Ceiling(Game1.smallFont.MeasureString("Add +").X * config.FontScale) + Padding * 2;
        addButtonBounds = new Rectangle(contentBounds.X, contentBounds.Y + 2,
            Math.Min(addWidth, contentBounds.Width), Math.Max(0, headerHeight - 4));
        batch.Draw(Game1.staminaRect, addButtonBounds,
            UiStyle.Border * (config.Opacity * (addButtonBounds.Contains(cursor) ? 1f : 0.45f)));
        UiStyle.DrawBorder(batch, addButtonBounds, config.Opacity);
        DrawCenteredText(batch, "Add +", addButtonBounds, UiStyle.Primary, config);

        Rectangle split = new(contentBounds.X, contentBounds.Y + headerHeight + 4, contentBounds.Width,
            Math.Max(0, contentBounds.Bottom - contentBounds.Y - headerHeight - 4));
        int treeWidth = Math.Clamp(split.Width * 35 / 100, 160, 260);
        if (split.Width < 320)
            treeWidth = Math.Max(0, split.Width / 2 - 8);
        treeBounds = new Rectangle(split.X, split.Y, Math.Max(0, treeWidth), split.Height);
        int dividerX = split.X + treeWidth + 3;
        detailBounds = new Rectangle(Math.Min(split.Right, dividerX + 4), split.Y,
            Math.Max(0, split.Right - dividerX - 4), split.Height);

        tree.Draw(batch, treeBounds, rowHeight, config, cursor,
            docs.Count == 0 ? "No notes yet." : "No notes.");
        if (detailBounds.Width <= 0 || detailBounds.Height <= 0)
            return;
        batch.Draw(Game1.staminaRect, new Rectangle(dividerX, split.Y, 1, split.Height), UiStyle.Border * config.Opacity);
        if (doc == null)
        {
            DrawCenteredText(batch, docs.Count == 0 ? "Click Add to create a note." : "Select a note.",
                detailBounds, UiStyle.Muted, config);
            return;
        }

        bool isTodo = doc.Kind == "todo";
        int fieldHeight = lineHeight + 8;
        titleBounds = new Rectangle(detailBounds.X + Padding, detailBounds.Y + Padding,
            Math.Max(0, detailBounds.Width - Padding * 2), fieldHeight);
        titleEntry.Draw(batch, titleBounds, config);
        if (!isTodo)
        {
            bodyBounds = new Rectangle(detailBounds.X + Padding, titleBounds.Bottom + Padding,
                Math.Max(0, detailBounds.Width - Padding * 2),
                Math.Max(0, detailBounds.Bottom - Padding - titleBounds.Bottom - Padding));
            bodyEntry.Draw(batch, bodyBounds, config);
            return;
        }

        int addButtonWidth = Math.Min(detailBounds.Width - Padding * 2,
            (int)MathF.Ceiling(Game1.smallFont.MeasureString("Add").X * config.FontScale) + Padding * 2);
        newItemButtonBounds = new Rectangle(detailBounds.Right - Padding - Math.Max(0, addButtonWidth),
            titleBounds.Bottom + Padding, Math.Max(0, addButtonWidth), fieldHeight);
        newItemBounds = new Rectangle(detailBounds.X + Padding, titleBounds.Bottom + Padding,
            Math.Max(0, newItemButtonBounds.X - Padding - detailBounds.X - Padding), fieldHeight);
        addEntry.Draw(batch, newItemBounds, config);
        bool canAdd = !string.IsNullOrWhiteSpace(addEntry.Text);
        batch.Draw(Game1.staminaRect, newItemButtonBounds,
            UiStyle.Border * (config.Opacity * (canAdd && newItemButtonBounds.Contains(cursor) ? 1f : 0.45f)));
        UiStyle.DrawBorder(batch, newItemButtonBounds, config.Opacity);
        DrawCenteredText(batch, "Add", newItemButtonBounds, canAdd ? UiStyle.Primary : UiStyle.Muted, config);

        itemsBounds = new Rectangle(detailBounds.X + Padding, newItemBounds.Bottom + Padding,
            Math.Max(0, detailBounds.Width - Padding * 2),
            Math.Max(0, detailBounds.Bottom - Padding - newItemBounds.Bottom - Padding));
        DrawTodoItems(batch, doc, itemsBounds, cursor, config);
    }

    private void DrawTodoItems(SpriteBatch batch, NoteRecord doc, Rectangle bounds, Point cursor, ModConfig config)
    {
        lastItemCount = doc.Items.Count;
        lastVisibleItems = 1;
        if (bounds.Width <= 0 || bounds.Height <= 0 || rowHeight <= 0)
            return;
        int visible = Math.Max(1, bounds.Height / rowHeight);
        lastVisibleItems = visible;
        firstItem = Math.Clamp(firstItem, 0, Math.Max(0, doc.Items.Count - visible));
        int end = Math.Min(doc.Items.Count, firstItem + visible);
        if (doc.Items.Count == 0)
        {
            DrawCenteredText(batch, "Nothing to do.", bounds, UiStyle.Muted, config);
            return;
        }
        bool needsScroll = doc.Items.Count > visible;
        int rowsWidth = needsScroll ? Math.Max(0, bounds.Width - 8) : bounds.Width;
        for (int i = firstItem; i < end; i++)
        {
            TodoRecord item = doc.Items[i];
            Rectangle row = new(bounds.X, bounds.Y + (i - firstItem) * rowHeight, rowsWidth,
                Math.Min(rowHeight, bounds.Bottom - (bounds.Y + (i - firstItem) * rowHeight)));
            bool hovered = row.Contains(cursor);
            if (hovered)
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.55f));
            else if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));

            Rectangle check = new(row.X + 6, row.Center.Y - 5, 10, 10);
            UiStyle.DrawBorder(batch, check, config.Opacity);
            if (item.Done)
                batch.Draw(Game1.staminaRect, new Rectangle(check.X + 3, check.Y + 3, 4, 4), UiStyle.Primary * config.Opacity);

            int deleteWidth = 18;
            Rectangle delete = new(row.Right - deleteWidth - 2, row.Y, deleteWidth, row.Height);
            Rectangle textBounds = new(check.Right + 6, row.Y, Math.Max(0, delete.X - check.Right - 6), row.Height);
            Color color = item.Done ? UiStyle.Muted : UiStyle.Primary;
            string fitted = UiText.Fit(item.Text, Math.Max(0, textBounds.Width - Padding * 2 - 1), config.FontScale);
            Vector2 size = Game1.smallFont.MeasureString(fitted) * config.FontScale;
            if (!string.IsNullOrEmpty(fitted) && size.Y + 1 <= textBounds.Height)
            {
                Vector2 position = new(textBounds.X + Padding, textBounds.Y + (textBounds.Height - size.Y) / 2);
                UiText.Draw(batch, fitted, position, color, config);
                if (item.Done && size.X > 0)
                {
                    float y = textBounds.Y + (textBounds.Height - 1) / 2f;
                    batch.Draw(Game1.staminaRect, new Rectangle(textBounds.X + Padding, (int)MathF.Round(y),
                        (int)MathF.Ceiling(size.X), 1), color * config.Opacity);
                }
            }
            if (hovered)
            {
                string glyph = "X";
                Vector2 glyphSize = Game1.smallFont.MeasureString(glyph) * config.FontScale;
                UiText.Draw(batch, glyph, new Vector2(delete.Center.X - glyphSize.X / 2,
                    delete.Center.Y - glyphSize.Y / 2),
                    delete.Contains(cursor) ? UiStyle.Primary : UiStyle.Muted, config);
            }
            drawnItems.Add((item, row, delete));
        }
        if (needsScroll && bounds.Height > 0)
        {
            itemScrollTrack = new Rectangle(bounds.Right - 6, bounds.Y, 4, visible * rowHeight);
            int maxFirst = Math.Max(1, doc.Items.Count - visible);
            int thumbHeight = Math.Min(itemScrollTrack.Height, Math.Max(16, itemScrollTrack.Height * visible / Math.Max(1, doc.Items.Count)));
            int thumbY = itemScrollTrack.Y + (int)Math.Round((itemScrollTrack.Height - thumbHeight) * (double)firstItem / maxFirst);
            itemScrollThumb = new Rectangle(itemScrollTrack.X, thumbY, itemScrollTrack.Width, thumbHeight);
            batch.Draw(Game1.staminaRect, itemScrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, itemScrollThumb, UiStyle.Muted * config.Opacity);
        }
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return;
        if (addButtonBounds.Contains(cursor))
        {
            showContextMenu(new Point(addButtonBounds.X, addButtonBounds.Bottom), new[]
            {
                new ContextMenuItem("New Note", () => CreateDoc("note")),
                new ContextMenuItem("New To-Do List", () => CreateDoc("todo"))
            });
            return;
        }
        if (treeBounds.Contains(cursor))
        {
            string? before = tree.SelectedId;
            if (tree.ReceiveClick(cursor, leftClick: true) && tree.SelectedId != before)
                EnsureBound(SelectedDoc());
            return;
        }
        NoteRecord? doc = SelectedDoc();
        if (doc == null || doc.Id != boundDocId)
            return;
        if (titleBounds.Contains(cursor))
        {
            Focus(titleEntry);
            return;
        }
        if (doc.Kind != "todo")
        {
            if (bodyBounds.Contains(cursor))
                Focus(bodyEntry);
            return;
        }
        if (newItemBounds.Contains(cursor))
        {
            Focus(addEntry);
            return;
        }
        if (newItemButtonBounds.Contains(cursor))
        {
            AddTodoItem();
            return;
        }
        if (!itemScrollThumb.IsEmpty && itemScrollTrack.Contains(cursor))
        {
            itemScrollDragOffset = itemScrollThumb.Contains(cursor) ? cursor.Y - itemScrollThumb.Y : itemScrollThumb.Height / 2;
            DragContent(cursor);
            return;
        }
        foreach ((TodoRecord item, Rectangle row, Rectangle delete) in drawnItems.ToArray())
        {
            if (!row.Contains(cursor))
                continue;
            if (delete.Contains(cursor))
                doc.Items.Remove(item);
            else
                item.Done = !item.Done;
            saveConfig();
            break;
        }
    }

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        if (!treeBounds.Contains(cursor))
            return null;
        TreeNode? node = tree.GetNodeAt(cursor);
        if (node == null)
            return null;
        string id = node.Id;
        if (!Context.IsWorldReady || Game1.player == null)
            return null;
        if (!config.Notes.TryGetValue(Game1.player.UniqueMultiplayerID, out List<NoteRecord>? docs) || docs == null)
            return null;
        if (docs.All(doc => doc.Id != id))
            return null;
        return new[] { new ContextMenuItem("Delete", () => DeleteDoc(id)) };
    }

    internal override void DragContent(Point cursor)
    {
        tree.Drag(cursor);
        if (itemScrollDragOffset.HasValue && !itemScrollTrack.IsEmpty && itemScrollTrack.Height > itemScrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - itemScrollDragOffset.Value - itemScrollTrack.Y)
                / (itemScrollTrack.Height - itemScrollThumb.Height);
            firstItem = (int)Math.Round(Math.Clamp(fraction, 0, 1) * Math.Max(0, lastItemCount - lastVisibleItems));
        }
    }

    internal override void EndContentDrag()
    {
        tree.EndDrag();
        itemScrollDragOffset = null;
    }

    internal override void ReceiveScroll(int delta)
    {
        if (delta == 0)
            return;
        Point cursor;
        try { cursor = new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true)); }
        catch { return; }
        int steps = Math.Max(1, Math.Abs(delta) / 120);
        if (treeBounds.Contains(cursor))
            tree.ReceiveScroll(delta);
        else if (itemsBounds.Contains(cursor))
            firstItem = Math.Max(0, firstItem - Math.Sign(delta) * steps * 3);
        else if (bodyBounds.Contains(cursor))
            bodyEntry.ReceiveScroll(delta);
    }

    private void CreateDoc(string kind)
    {
        List<NoteRecord> docs = Docs();
        var doc = new NoteRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Title = kind == "todo" ? "Untitled list" : "Untitled note"
        };
        docs.Add(doc);
        saveConfig();
        tree.Select(doc.Id);
        EnsureBound(doc);
    }

    private void DeleteDoc(string id)
    {
        List<NoteRecord> docs = Docs();
        NoteRecord? doc = docs.FirstOrDefault(candidate => candidate.Id == id);
        if (doc == null)
            return;
        docs.Remove(doc);
        saveConfig();
        if (tree.SelectedId == id)
            tree.Select(null);
        if (boundDocId == id)
        {
            boundDocId = null;
            UnfocusAll();
        }
    }

    private void AddTodoItem()
    {
        NoteRecord? doc = SelectedDoc();
        if (doc == null || doc.Kind != "todo" || doc.Id != boundDocId)
            return;
        string text = addEntry.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;
        doc.Items.Add(new TodoRecord { Id = Guid.NewGuid().ToString("N"), Text = text });
        addEntry.Bind("");
        saveConfig();
        firstItem = int.MaxValue;
    }
}
