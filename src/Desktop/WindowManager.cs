using AgrariaOS.Configuration;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Notifications;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Desktop;

internal sealed class WindowManager
{
    private readonly ModConfig config;
    private readonly Func<bool> isVisible;
    private readonly Action<Exception> onDrawFailed;
    private readonly Action saveConfig;
    private Dictionary<string, WindowPlacement>? layouts;
    // Last entry is the front window, in both drawing and hit testing.
    private readonly List<OsWindow> windows = new();
    private readonly List<OsWindow> drawnWindows = new();
    private Point drawnViewport;
    private OsWindow? draggedWindow;
    private OsWindow? resizedWindow;
    private OsWindow? contentDragWindow;
    private Point dragOffset;
    private Point resizeStartCursor;
    private Rectangle resizeStartBounds;

    internal WindowManager(ModConfig config, Func<bool> isVisible, Action<Exception> onDrawFailed, Action saveConfig)
    {
        this.config = config;
        this.isVisible = isVisible;
        this.onDrawFailed = onDrawFailed;
        this.saveConfig = saveConfig;
    }

    internal void OpenMail() => Open(MailWindow.WindowId, () => new MailWindow());
    internal void OpenInventory() => Open(InventoryWindow.WindowId, () => new InventoryWindow());
    internal void OpenNotifications(NotificationHistory history) => Open(NotificationsWindow.WindowId, () => new NotificationsWindow(history));

    internal void Open(string id, Func<OsWindow> create)
    {
        OsWindow? window = windows.FirstOrDefault(window => window.Id == id);
        if (window == null)
        {
            // Resolve ownership on first open, after the save's farmer is available.
            if (layouts == null)
            {
                long farmerId = Game1.player.UniqueMultiplayerID;
                if (!config.WindowLayouts.TryGetValue(farmerId, out layouts))
                    config.WindowLayouts[farmerId] = layouts = new();
            }
            window = create();
            if (layouts.TryGetValue(id, out WindowPlacement? placement))
                window.RestorePlacement(placement);
        }
        window.IsMinimized = false;
        windows.Remove(window);
        windows.Add(window);
    }

    internal bool IsMinimized(string id) => windows.Any(window => window.Id == id && window.IsMinimized);

    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        OsWindow? window = GetWindowAt(cursor);
        if (window == null)
            return false;

        if (leftClick)
        {
            EndDrag();
            windows.Remove(window);
            windows.Add(window);
            if (window.CloseBounds.Contains(cursor))
            {
                RememberPlacement(window);
                windows.Remove(window);
                EndDrag();
            }
            else if (window.MinimizeBounds.Contains(cursor))
            {
                RememberPlacement(window);
                window.IsMinimized = true;
                EndDrag();
            }
            else if (window.GetResizeEdges(cursor) is var edges && edges != WindowResizeEdges.None)
            {
                resizedWindow = window;
                window.ActiveResizeEdges = edges;
                resizeStartCursor = cursor;
                resizeStartBounds = window.Bounds;
            }
            else if (window.TitleBounds.Contains(cursor))
            {
                draggedWindow = window;
                dragOffset = cursor - window.Bounds.Location;
            }
            else
            {
                window.ReceiveContentClick(cursor, leftClick: true);
                contentDragWindow = window;
            }
        }
        else
            window.ReceiveContentClick(cursor, leftClick: false);
        // Suppress the whole press (including held repeats) before native HUD/world dispatch.
        return true;
    }

    internal bool ReceiveScroll(Point cursor, int delta)
    {
        OsWindow? window = GetWindowAt(cursor);
        if (window == null)
            return false;

        window.ReceiveScroll(delta);
        return true;
    }

    private OsWindow? GetWindowAt(Point cursor)
    {
        if (!isVisible() || Game1.IsChatting || drawnViewport != new Point(Game1.uiViewport.Width, Game1.uiViewport.Height))
            return null;

        return drawnWindows.LastOrDefault(window => !window.IsMinimized && windows.Contains(window) && window.Bounds.Contains(cursor));
    }

    internal void Update(IInputHelper input)
    {
        // Suppression reports the button released to the game on subsequent ticks.
        // SMAPI retains the suppression flag until the physical button is released.
        bool leftButtonHeld = input.IsDown(SButton.MouseLeft) || input.IsSuppressed(SButton.MouseLeft);
        if (!leftButtonHeld || !isVisible() || Game1.IsChatting
            || drawnViewport != new Point(Game1.uiViewport.Width, Game1.uiViewport.Height))
        {
            EndDrag();
            return;
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (draggedWindow != null)
            draggedWindow.MoveTo(cursor - dragOffset);
        resizedWindow?.ResizeFrom(resizeStartBounds, cursor - resizeStartCursor, resizedWindow.ActiveResizeEdges);
        contentDragWindow?.DragContent(cursor);
    }

    internal void EndDrag()
    {
        if (draggedWindow != null)
            RememberPlacement(draggedWindow);
        if (resizedWindow != null)
            RememberPlacement(resizedWindow);
        draggedWindow = null;
        if (resizedWindow != null)
            resizedWindow.ActiveResizeEdges = WindowResizeEdges.None;
        resizedWindow = null;
        contentDragWindow?.EndContentDrag();
        contentDragWindow = null;
    }

    private void RememberPlacement(OsWindow window)
    {
        WindowPlacement? placement = window.GetPlacement();
        if (layouts == null || placement == null
            || (layouts.TryGetValue(window.Id, out WindowPlacement? previous) && previous == placement))
            return;

        layouts[window.Id] = placement;
        saveConfig();
    }

    internal void InvalidateLayout()
    {
        drawnWindows.Clear();
        EndDrag();
    }

    internal void Draw(SpriteBatch batch)
    {
        drawnWindows.Clear();
        if (!isVisible())
        {
            EndDrag();
            return;
        }

        try
        {
            drawnViewport = new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);
            const int margin = 8;
            int left = Pelicom.StripWidth + margin;
            Rectangle workArea = new(left, margin, Math.Max(0, drawnViewport.X - left - margin), Math.Max(0, drawnViewport.Y - margin * 2));
            float titleTextHeight = windows.Count == 0 ? Game1.smallFont.LineSpacing
                : windows.Max(window => Game1.smallFont.MeasureString(window.Title).Y);
            int titleHeight = Math.Max(27, (int)MathF.Ceiling(titleTextHeight * config.FontScale)) + 6;
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            foreach (OsWindow window in windows)
            {
                if (!window.IsMinimized && window.Layout(workArea, titleHeight, config))
                {
                    window.Draw(batch, config, cursor);
                    drawnWindows.Add(window);
                }
            }
        }
        catch (Exception ex)
        {
            InvalidateLayout();
            onDrawFailed(ex);
        }
    }
}
