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
internal sealed partial class WindowTabGroup
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










}
