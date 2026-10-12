using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace AgrariaOS.Desktop;

[Flags]
internal enum WindowResizeEdges
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8
}

// Window instances live until closed. The manager persists placement separately from content.
internal abstract class OsWindow
{
    private const int ResizeBorder = 4;
    private const int ResizeGrip = 12;
    internal abstract string Id { get; }
    internal abstract string Title { get; }
    internal virtual string TitleSubtext => "";
    internal virtual IReadOnlyList<WindowTitlebarAction> TitlebarActions => Array.Empty<WindowTitlebarAction>();
    internal abstract int Icon { get; }
    internal virtual bool IsValid => true;
    internal virtual bool CanResize => true;
    internal virtual ContextMenuInfo? GetContextMenuInfo(Point cursor) => null;
    internal virtual Point DefaultSize => new(420, 260);
    protected virtual bool DrawContentBackground => true;
    internal bool IsMinimized { get; set; }
    internal Rectangle Bounds { get; private set; }
    internal int TitleHeight { get; private set; }
    internal Rectangle TitleBounds => new(Bounds.X, Bounds.Y, Bounds.Width, TitleHeight);
    internal Rectangle CloseBounds => new(Bounds.Right - TitleHeight - 1, Bounds.Y + 1, TitleHeight, TitleHeight - 1);
    internal Rectangle MinimizeBounds => new(CloseBounds.X - TitleHeight, CloseBounds.Y, TitleHeight, CloseBounds.Height);
    // Keep the leading window icon visible even in a very narrow viewport.
    private int VisibleTitlebarActionCount => TitleHeight <= 0 ? 0
        : Math.Min(TitlebarActions.Count, Math.Max(0, (MinimizeBounds.X - Bounds.X - 30) / TitleHeight));
    internal int TitlebarActionsLeft => MinimizeBounds.X - VisibleTitlebarActionCount * TitleHeight;
    internal WindowResizeEdges ActiveResizeEdges { get; set; }
    private Point? position;
    private Point? size;
    private Point minimumSize;
    private Rectangle workArea;

    protected virtual Point GetMinimumSize(ModConfig config) => new(220, TitleHeight + 80);
    internal bool ShowsContentBackground => DrawContentBackground;

    internal Point GetMinimumSizeForGroup(ModConfig config, int titleHeight)
    {
        int previous = TitleHeight;
        TitleHeight = titleHeight;
        try { return GetMinimumSize(config); }
        finally { TitleHeight = previous; }
    }

    internal void SetGroupedBounds(Rectangle bounds, int titleHeight, Rectangle workArea, Point? position, Point? size)
    {
        Bounds = bounds;
        TitleHeight = titleHeight;
        this.workArea = workArea;
        this.position = position;
        this.size = size;
    }

    internal void DrawGroupedContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
        => DrawContent(batch, contentBounds, config);

    internal bool Layout(Rectangle workArea, int titleHeight, ModConfig config)
    {
        this.workArea = workArea;
        TitleHeight = titleHeight;
        if (workArea.Width < Math.Max(160, titleHeight * 4) || workArea.Height < titleHeight + 40)
        {
            Bounds = Rectangle.Empty;
            return false;
        }

        Point contentMinimum = GetMinimumSize(config);
        minimumSize = new Point(Math.Min(Math.Max(contentMinimum.X, titleHeight * 4), workArea.Width),
            Math.Min(Math.Max(contentMinimum.Y, titleHeight + 40), workArea.Height));
        Point desiredSize = CanResize ? size ?? DefaultSize : DefaultSize;
        int width = Math.Clamp(desiredSize.X, minimumSize.X, workArea.Width);
        int height = Math.Clamp(desiredSize.Y, minimumSize.Y, workArea.Height);
        Point origin = position ?? new Point(workArea.X + (workArea.Width - width) / 2, workArea.Y + (workArea.Height - height) / 2);
        // Fit the current viewport without overwriting the user's preferred placement.
        Bounds = new Rectangle(Math.Clamp(origin.X, workArea.Left, workArea.Right - width),
            Math.Clamp(origin.Y, workArea.Top, workArea.Bottom - height), width, height);
        return true;
    }

    internal void RestorePlacement(WindowPlacement placement)
    {
        position = new Point(placement.X, placement.Y);
        if (CanResize)
            size = new Point(placement.Width, placement.Height);
    }

    internal WindowPlacement? GetPlacement()
    {
        if (Bounds.IsEmpty)
            return null;
        Point origin = position ?? Bounds.Location;
        Point dimensions = size ?? Bounds.Size;
        return new WindowPlacement { X = origin.X, Y = origin.Y, Width = dimensions.X, Height = dimensions.Y };
    }

    internal void MoveTo(Point origin)
    {
        position = new Point(Math.Clamp(origin.X, workArea.Left, workArea.Right - Bounds.Width),
            Math.Clamp(origin.Y, workArea.Top, workArea.Bottom - Bounds.Height));
        Bounds = new Rectangle(position.Value, Bounds.Size);
    }

    internal WindowResizeEdges GetResizeEdges(Point cursor)
    {
        if (!CanResize || !Bounds.Contains(cursor) || CloseBounds.Contains(cursor) || MinimizeBounds.Contains(cursor)
            || GetTitlebarActionAt(cursor) != null)
            return WindowResizeEdges.None;
        if (cursor.X >= Bounds.Right - ResizeGrip && cursor.Y >= Bounds.Bottom - ResizeGrip)
            return WindowResizeEdges.Right | WindowResizeEdges.Bottom;

        WindowResizeEdges edges = WindowResizeEdges.None;
        if (cursor.X < Bounds.Left + ResizeBorder) edges |= WindowResizeEdges.Left;
        if (cursor.X >= Bounds.Right - ResizeBorder) edges |= WindowResizeEdges.Right;
        if (cursor.Y < Bounds.Top + ResizeBorder) edges |= WindowResizeEdges.Top;
        if (cursor.Y >= Bounds.Bottom - ResizeBorder) edges |= WindowResizeEdges.Bottom;
        return edges;
    }

    internal void ResizeFrom(Rectangle startBounds, Point delta, WindowResizeEdges edges)
    {
        if (!CanResize)
            return;
        int left = startBounds.Left;
        int right = startBounds.Right;
        int top = startBounds.Top;
        int bottom = startBounds.Bottom;
        // Clamp the dragged edges, keeping the opposite edges anchored throughout the gesture.
        if (edges.HasFlag(WindowResizeEdges.Left))
            left = Math.Clamp(left + delta.X, workArea.Left, right - minimumSize.X);
        if (edges.HasFlag(WindowResizeEdges.Right))
            right = Math.Clamp(right + delta.X, left + minimumSize.X, workArea.Right);
        if (edges.HasFlag(WindowResizeEdges.Top))
            top = Math.Clamp(top + delta.Y, workArea.Top, bottom - minimumSize.Y);
        if (edges.HasFlag(WindowResizeEdges.Bottom))
            bottom = Math.Clamp(bottom + delta.Y, top + minimumSize.Y, workArea.Bottom);
        position = new Point(left, top);
        size = new Point(right - left, bottom - top);
        Bounds = new Rectangle(position.Value, size.Value);
    }

    internal virtual void ReceiveContentClick(Point cursor, bool leftClick) { }
    internal virtual ContextMenuItem[]? GetContextMenuItems(Point cursor) => null;
    internal virtual void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged) { }
    internal virtual void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged) { }
    internal virtual void DragContent(Point cursor) { }
    internal virtual void EndContentDrag() { }
    internal virtual void ReceiveScroll(int delta) { }
    internal virtual IKeyboardSubscriber? KeyboardSubscriber => null;
    internal virtual bool WantsKeyboardFocus => KeyboardSubscriber?.Selected == true;
    internal virtual void ReleaseKeyboardFocus()
    {
        if (KeyboardSubscriber is { } subscriber)
            subscriber.Selected = false;
    }
    internal virtual bool IsTextFieldAt(Point cursor) => false;
    internal virtual void ReceiveKey(Keys key) { }
    internal virtual bool ReceiveShortcut(StardewModdingAPI.SButton button) => false;
    internal virtual void Update() { }
    internal virtual void Close() { }

    private Rectangle GetTitlebarActionBounds(int index)
        => new(TitlebarActionsLeft + index * TitleHeight, MinimizeBounds.Y, TitleHeight, MinimizeBounds.Height);

    internal WindowTitlebarAction? GetTitlebarActionAt(Point cursor)
    {
        for (int i = 0; i < VisibleTitlebarActionCount; i++)
        {
            if (GetTitlebarActionBounds(i).Contains(cursor))
                return TitlebarActions[i];
        }
        return null;
    }

    internal void DrawTitlebarActions(SpriteBatch batch, ModConfig config, Point cursor)
    {
        for (int i = 0; i < VisibleTitlebarActionCount; i++)
        {
            WindowTitlebarAction action = TitlebarActions[i];
            Rectangle bounds = GetTitlebarActionBounds(i);
            bool enabled = action.IsEnabled;
            if (enabled && bounds.Contains(cursor))
                batch.Draw(Game1.staminaRect, bounds, UiStyle.Border * config.Opacity);
            action.DrawIcon(batch, bounds, (enabled ? UiStyle.Primary : UiStyle.Muted) * config.Opacity);
        }
    }

    internal void Draw(SpriteBatch batch, ModConfig config, Point cursor, bool isDockPreview = false)
    {
        Rectangle background = DrawContentBackground ? Bounds : new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, TitleHeight + 1);
        batch.Draw(Game1.staminaRect, background, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, Bounds, config.Opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(Bounds.X + 1, Bounds.Y + TitleHeight, Bounds.Width - 2, 1),
            UiStyle.Border * config.Opacity);

        if (isDockPreview)
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.X + 1, Bounds.Y + 1,
                Math.Max(0, TitlebarActionsLeft - Bounds.X - 1), TitleHeight - 1), UiStyle.DockHighlight * config.Opacity);

        const int iconSize = 18;
        DrawTitleIcon(batch, new Point(Bounds.X + 6, Bounds.Y + (TitleHeight - iconSize) / 2), config);
        Rectangle titleTextBounds = new(Bounds.X + 30, Bounds.Y, Math.Max(0, TitlebarActionsLeft - Bounds.X - 36), TitleHeight);
        DrawTitle(batch, titleTextBounds, config);
        DrawTitlebarActions(batch, config, cursor);
        DrawButton(batch, MinimizeBounds, cursor, config, close: false);
        DrawButton(batch, CloseBounds, cursor, config, close: true);
        DrawContent(batch, new Rectangle(Bounds.X + 1, Bounds.Y + TitleHeight + 1,
            Bounds.Width - 2, Bounds.Height - TitleHeight - 2), config);
        DrawResizeHandles(batch, cursor, config);
    }

    protected abstract void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config);

    internal virtual void DrawTitleIcon(SpriteBatch batch, Point position, ModConfig config)
        => UiStyle.DrawIcon(batch, Icon, position, 2, config.Opacity);

    private void DrawTitle(SpriteBatch batch, Rectangle bounds, ModConfig config)
    {
        string subtext = TitleSubtext;
        if (string.IsNullOrEmpty(subtext))
        {
            DrawCenteredText(batch, Title, bounds, UiStyle.Primary, config);
            return;
        }

        // Keep the subtext visible when a long title (such as a chest name) is clipped.
        int width = Math.Max(0, bounds.Width - 1);
        subtext = UiText.Fit(subtext, width, config.FontScale);
        Vector2 subtextSize = Game1.smallFont.MeasureString(subtext) * config.FontScale;
        float gap = string.IsNullOrEmpty(subtext) ? 0 : Game1.smallFont.MeasureString(" ").X * config.FontScale;
        string title = UiText.Fit(Title, Math.Max(0, width - (int)MathF.Ceiling(subtextSize.X + gap)), config.FontScale);
        Vector2 titleSize = Game1.smallFont.MeasureString(title) * config.FontScale;
        if (string.IsNullOrEmpty(title))
            gap = 0;
        float height = Math.Max(titleSize.Y, subtextSize.Y);
        if (height + 1 > bounds.Height)
            return;
        float x = bounds.X + (bounds.Width - titleSize.X - gap - subtextSize.X) / 2;
        float y = bounds.Y + (bounds.Height - height) / 2;
        UiText.Draw(batch, title, new Vector2(x, y), UiStyle.Primary, config);
        UiText.Draw(batch, subtext, new Vector2(x + titleSize.X + gap, y), UiStyle.Muted, config);
    }

    protected static void DrawCenteredText(SpriteBatch batch, string text, Rectangle bounds, Color color, ModConfig config)
    {
        text = UiText.Fit(text, Math.Max(0, bounds.Width - 1), config.FontScale);
        Vector2 size = Game1.smallFont.MeasureString(text) * config.FontScale;
        if (string.IsNullOrEmpty(text) || size.Y + 1 > bounds.Height)
            return;
        UiText.Draw(batch, text, new Vector2(bounds.X + (bounds.Width - size.X) / 2,
            bounds.Y + (bounds.Height - size.Y) / 2), color, config);
    }

    private void DrawResizeHandles(SpriteBatch batch, Point cursor, ModConfig config)
    {
        if (!CanResize)
            return;
        WindowResizeEdges edges = ActiveResizeEdges != WindowResizeEdges.None ? ActiveResizeEdges : GetResizeEdges(cursor);
        Color color = (edges == WindowResizeEdges.None ? UiStyle.Muted : UiStyle.Primary) * config.Opacity;
        for (int offset = 3; offset <= 9; offset += 3)
        for (int i = 0; i < offset; i++)
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Right - 2 - i, Bounds.Bottom - 1 - offset + i, 1, 1), color);

        Color highlight = UiStyle.Primary * config.Opacity;
        if (edges.HasFlag(WindowResizeEdges.Left))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Left, Bounds.Top, 1, Bounds.Height), highlight);
        if (edges.HasFlag(WindowResizeEdges.Right))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Right - 1, Bounds.Top, 1, Bounds.Height), highlight);
        if (edges.HasFlag(WindowResizeEdges.Top))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, 1), highlight);
        if (edges.HasFlag(WindowResizeEdges.Bottom))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Left, Bounds.Bottom - 1, Bounds.Width, 1), highlight);
    }

    private static void DrawButton(SpriteBatch batch, Rectangle bounds, Point cursor, ModConfig config, bool close)
    {
        if (bounds.Contains(cursor))
            batch.Draw(Game1.staminaRect, bounds, UiStyle.Border * config.Opacity);

        // Whole-pixel symbols keep these small controls independent of font scaling.
        int x = bounds.Center.X - 4;
        int y = bounds.Center.Y - 4;
        Color color = UiStyle.Primary * config.Opacity;
        if (close)
        {
            for (int i = 0; i < 9; i++)
            {
                batch.Draw(Game1.staminaRect, new Rectangle(x + i, y + i, 1, 1), color);
                batch.Draw(Game1.staminaRect, new Rectangle(x + 8 - i, y + i, 1, 1), color);
            }
        }
        else
            batch.Draw(Game1.staminaRect, new Rectangle(x, y + 7, 9, 1), color);
    }
}
