using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Desktop;

partial class WindowTabGroup
{
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

}
