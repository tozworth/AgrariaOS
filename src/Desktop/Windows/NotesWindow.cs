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
internal sealed partial class NotesWindow : OsWindow
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



}
