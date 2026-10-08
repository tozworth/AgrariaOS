using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Desktop;

// A tabbed frame that hosts two or more windows in a shared bounds. Tabs show
// each window's icon and title (never the subtitle). The group owns position,
// size and chrome; members mirror the group's bounds so placement persistence,
// minimize and close keep working per window.
internal sealed class WindowTabGroup
{
    internal List<OsWindow> Tabs { get; } = new();
    internal OsWindow Active { get; private set; }
    internal Rectangle Bounds { get; private set; }
    internal int TitleHeight { get; private set; }
    internal Rectangle TitleBounds => new(Bounds.X, Bounds.Y, Bounds.Width, TitleHeight);
    internal Rectangle CloseBounds => new(Bounds.Right - TitleHeight - 1, Bounds.Y + 1, TitleHeight, TitleHeight - 1);
    internal Rectangle MinimizeBounds => new(CloseBounds.X - TitleHeight, CloseBounds.Y, TitleHeight, CloseBounds.Height);
    internal WindowResizeEdges ActiveResizeEdges { get; set; }
    private Point? position;
    private Point? size;
    private Point minimumSize;
    private Rectangle workArea;
    private readonly Dictionary<OsWindow, Rectangle> drawnTabs = new();

    internal WindowTabGroup(IEnumerable<OsWindow> tabs, OsWindow active, Point? position, Point? size)
    {
        Tabs.AddRange(tabs);
        Active = Tabs.Contains(active) ? active : Tabs[0];
        this.position = position;
        this.size = size;
    }

    internal WindowTabGroup(IEnumerable<OsWindow> tabs, OsWindow active, WindowPlacement placement)
        : this(tabs, active, new Point(placement.X, placement.Y), new Point(placement.Width, placement.Height))
    {
    }

    internal bool Contains(OsWindow window) => Tabs.Contains(window);

    internal void SetActive(OsWindow window)
    {
        if (Tabs.Contains(window))
            Active = window;
    }

    internal void AddTab(OsWindow window)
    {
        if (!Tabs.Contains(window))
            Tabs.Add(window);
    }

    internal void RemoveTab(OsWindow window)
    {
        Tabs.Remove(window);
        if (Tabs.Count > 0 && ReferenceEquals(Active, window))
            Active = Tabs[0];
    }

    internal OsWindow? GetTabAt(Point cursor)
    {
        foreach (var pair in drawnTabs)
        {
            if (pair.Value.Contains(cursor))
                return pair.Key;
        }
        return null;
    }

    internal void ClearDrawnTabs() => drawnTabs.Clear();

    internal bool Layout(Rectangle workArea, int titleHeight, ModConfig config)
    {
        this.workArea = workArea;
        TitleHeight = titleHeight;
        if (Tabs.Count == 0
            || workArea.Width < Math.Max(160, titleHeight * 4) || workArea.Height < titleHeight + 40)
        {
            Bounds = Rectangle.Empty;
            return false;
        }

        Point groupMinimum = new(titleHeight * 4, titleHeight + 40);
        foreach (OsWindow tab in Tabs)
        {
            try
            {
                Point tabMinimum = tab.GetMinimumSizeForGroup(config, titleHeight);
                groupMinimum = new Point(Math.Max(groupMinimum.X, tabMinimum.X), Math.Max(groupMinimum.Y, tabMinimum.Y));
            }
            catch { }
        }
        minimumSize = new Point(Math.Min(groupMinimum.X, workArea.Width), Math.Min(groupMinimum.Y, workArea.Height));
        Point desiredSize;
        try { desiredSize = size ?? Active.DefaultSize; }
        catch { desiredSize = new Point(420, 260); }
        int width = Math.Clamp(desiredSize.X, minimumSize.X, workArea.Width);
        int height = Math.Clamp(desiredSize.Y, minimumSize.Y, workArea.Height);
        Point origin = position ?? new Point(workArea.X + (workArea.Width - width) / 2, workArea.Y + (workArea.Height - height) / 2);
        Bounds = new Rectangle(Math.Clamp(origin.X, workArea.Left, workArea.Right - width),
            Math.Clamp(origin.Y, workArea.Top, workArea.Bottom - height), width, height);
        SyncMembers();
        return true;
    }

    private void SyncMembers()
    {
        foreach (OsWindow tab in Tabs)
        {
            try { tab.SetGroupedBounds(Bounds, TitleHeight, workArea, position, size); }
            catch { }
        }
    }

    internal WindowPlacement? GetPlacement()
    {
        if (Bounds.IsEmpty)
            return null;
        Point origin = position ?? Bounds.Location;
        Point dimensions = size ?? Bounds.Size;
        return new WindowPlacement { X = origin.X, Y = origin.Y, Width = dimensions.X, Height = dimensions.Y };
    }

    internal void InitFromPlacement(WindowPlacement placement)
    {
        position = new Point(placement.X, placement.Y);
        size = new Point(placement.Width, placement.Height);
        SyncMembers();
    }

    internal void MoveTo(Point origin)
    {
        position = new Point(Math.Clamp(origin.X, workArea.Left, workArea.Right - Bounds.Width),
            Math.Clamp(origin.Y, workArea.Top, workArea.Bottom - Bounds.Height));
        Bounds = new Rectangle(position.Value, Bounds.Size);
        SyncMembers();
    }

    internal WindowResizeEdges GetResizeEdges(Point cursor)
    {
        if (!Bounds.Contains(cursor) || CloseBounds.Contains(cursor) || MinimizeBounds.Contains(cursor))
            return WindowResizeEdges.None;
        const int border = 4;
        const int grip = 12;
        if (cursor.X >= Bounds.Right - grip && cursor.Y >= Bounds.Bottom - grip)
            return WindowResizeEdges.Right | WindowResizeEdges.Bottom;

        WindowResizeEdges edges = WindowResizeEdges.None;
        if (cursor.X < Bounds.Left + border) edges |= WindowResizeEdges.Left;
        if (cursor.X >= Bounds.Right - border) edges |= WindowResizeEdges.Right;
        if (cursor.Y < Bounds.Top + border) edges |= WindowResizeEdges.Top;
        if (cursor.Y >= Bounds.Bottom - border) edges |= WindowResizeEdges.Bottom;
        // The tab strip drags the whole frame; never resize from the title row.
        if (TitleBounds.Contains(cursor))
            edges &= ~(WindowResizeEdges.Top | WindowResizeEdges.Left | WindowResizeEdges.Right);
        return edges;
    }

    internal void ResizeFrom(Rectangle startBounds, Point delta, WindowResizeEdges edges)
    {
        int left = startBounds.Left;
        int right = startBounds.Right;
        int top = startBounds.Top;
        int bottom = startBounds.Bottom;
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
        SyncMembers();
    }

    internal void Draw(SpriteBatch batch, ModConfig config, Point cursor)
    {
        drawnTabs.Clear();
        if (Bounds.IsEmpty || Tabs.Count == 0)
            return;

        bool contentBackground;
        try { contentBackground = Active.ShowsContentBackground; }
        catch { contentBackground = true; }
        Rectangle background = contentBackground ? Bounds : new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, TitleHeight + 1);
        batch.Draw(Game1.staminaRect, background, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, Bounds, config.Opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(Bounds.X + 1, Bounds.Y + TitleHeight, Bounds.Width - 2, 1),
            UiStyle.Border * config.Opacity);

        // Tab strip: equal widths, icon plus title text only.
        Rectangle strip = new(Bounds.X + 1, Bounds.Y + 1, Math.Max(0, MinimizeBounds.X - Bounds.X - 1), TitleHeight - 1);
        int count = Tabs.Count;
        int baseWidth = count == 0 ? 0 : strip.Width / count;
        int remainder = count == 0 ? 0 : strip.Width % count;
        int x = strip.X;
        for (int i = 0; i < count; i++)
        {
            OsWindow tab = Tabs[i];
            int tabWidth = baseWidth + (i == count - 1 ? remainder : 0);
            Rectangle tabBounds = new(x, strip.Y, Math.Max(0, tabWidth), strip.Height);
            x += tabWidth;
            if (tabBounds.Width <= 0 || tabBounds.Height <= 0)
                continue;
            drawnTabs[tab] = tabBounds;

            bool isActive = ReferenceEquals(tab, Active);
            bool hovered = tabBounds.Contains(cursor);
            if (isActive)
                batch.Draw(Game1.staminaRect, tabBounds, UiStyle.Border * config.Opacity);
            else if (hovered)
                batch.Draw(Game1.staminaRect, tabBounds, UiStyle.Border * (config.Opacity * 0.45f));
            if (i + 1 < count)
                batch.Draw(Game1.staminaRect, new Rectangle(tabBounds.Right - 1, tabBounds.Y + 3, 1, Math.Max(0, tabBounds.Height - 6)),
                    UiStyle.Border * (config.Opacity * 0.7f));

            const int iconSize = 18;
            int iconY = tabBounds.Y + (tabBounds.Height - iconSize) / 2;
            try { tab.DrawTitleIcon(batch, new Point(tabBounds.X + 6, iconY), config); }
            catch { }
            Rectangle textBounds = new(tabBounds.X + 30, tabBounds.Y, Math.Max(0, tabBounds.Right - 6 - (tabBounds.X + 30)), tabBounds.Height);
            if (textBounds.Width > 0)
            {
                string title;
                try { title = tab.Title; }
                catch { title = ""; }
                title = UiText.Fit(title ?? "", Math.Max(0, textBounds.Width - 1), config.FontScale);
                Vector2 size = Game1.smallFont.MeasureString(title) * config.FontScale;
                if (!string.IsNullOrEmpty(title) && size.Y + 1 <= textBounds.Height)
                {
                    Color color = isActive ? UiStyle.Primary : UiStyle.Muted;
                    UiText.Draw(batch, title, new Vector2(textBounds.X,
                        textBounds.Y + (textBounds.Height - size.Y) / 2), color, config);
                }
            }
        }

        DrawButton(batch, MinimizeBounds, cursor, config, close: false);
        DrawButton(batch, CloseBounds, cursor, config, close: true);

        Rectangle contentBounds = new(Bounds.X + 1, Bounds.Y + TitleHeight + 1,
            Bounds.Width - 2, Bounds.Height - TitleHeight - 2);
        if (contentBounds.Width > 0 && contentBounds.Height > 0)
        {
            try { Active.DrawGroupedContent(batch, contentBounds, config); }
            catch (Exception ex)
            {
                // Surface content failures without losing the frame; the manager
                // reports draw errors and falls back to vanilla on the next frame.
                throw new InvalidOperationException($"Tab '{Active.Id}' failed to draw.", ex);
            }
        }
        DrawResizeHandles(batch, cursor, config);
    }

    private void DrawResizeHandles(SpriteBatch batch, Point cursor, ModConfig config)
    {
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
