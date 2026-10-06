using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

internal sealed class WindowManager
{
    private readonly ModConfig config;
    private readonly Func<bool> isVisible;
    private readonly Action<Exception> onDrawFailed;
    // Last entry is the front window, in both drawing and hit testing.
    private readonly List<OsWindow> windows = new();
    private readonly List<OsWindow> drawnWindows = new();
    private Point drawnViewport;
    private OsWindow? draggedWindow;
    private OsWindow? contentDragWindow;
    private Point dragOffset;

    internal WindowManager(ModConfig config, Func<bool> isVisible, Action<Exception> onDrawFailed)
    {
        this.config = config;
        this.isVisible = isVisible;
        this.onDrawFailed = onDrawFailed;
    }

    internal void OpenMail() => Open(MailWindow.WindowId, () => new MailWindow());
    internal void OpenInventory() => Open(InventoryWindow.WindowId, () => new InventoryWindow());

    internal void Open(string id, Func<OsWindow> create)
    {
        OsWindow window = windows.FirstOrDefault(window => window.Id == id) ?? create();
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
                windows.Remove(window);
                EndDrag();
            }
            else if (window.MinimizeBounds.Contains(cursor))
            {
                window.IsMinimized = true;
                EndDrag();
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
        if (!leftButtonHeld || !isVisible() || Game1.IsChatting)
        {
            EndDrag();
            return;
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (draggedWindow != null)
            draggedWindow.MoveTo(cursor - dragOffset);
        contentDragWindow?.DragContent(cursor);
    }

    internal void EndDrag()
    {
        draggedWindow = null;
        contentDragWindow?.EndContentDrag();
        contentDragWindow = null;
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
            int titleHeight = Math.Max(27, (int)MathF.Ceiling(Game1.smallFont.LineSpacing * config.FontScale)) + 6;
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            foreach (OsWindow window in windows)
            {
                if (!window.IsMinimized && window.Layout(workArea, titleHeight))
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
