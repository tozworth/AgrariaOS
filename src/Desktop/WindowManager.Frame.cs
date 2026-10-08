using AgrariaOS.Configuration;
using AgrariaOS.Chat;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Notifications;
using AgrariaOS.Interactions;
using AgrariaOS.Integration;
using AgrariaOS.Mining;
using AgrariaOS.Movement;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace AgrariaOS.Desktop;

partial class WindowManager
{
    internal void Update(IInputHelper input)
    {
        foreach (OsWindow window in windows.ToArray())
            window.Update();
        bool removedInvalid = false;
        foreach (OsWindow window in windows.Where(window => !window.IsValid).ToArray())
        {
            CancelPendingTransfer();
            if (window is TradeWindow)
                pendingBuys.Clear();
            EndDrag();
            WindowTabGroup? invalidGroup = FindGroup(window);
            if (invalidGroup != null)
            {
                invalidGroup.RemoveTab(window);
                DissolveGroupIfSingle(invalidGroup);
            }
            RememberPlacement(window);
            windows.Remove(window);
            window.Close();
            if (ReferenceEquals(focusedWindow, window))
                focusedWindow = GetFrontmostActive();
            if (ReferenceEquals(keyboardWindow, window))
                ClearKeyboardFocus();
            removedInvalid = true;
        }
        // Drop single-member groups left by invalid members.
        foreach (WindowTabGroup group in tabGroups.ToArray())
        {
            group.Tabs.RemoveAll(member => !windows.Contains(member) || !member.IsValid);
            if (group.Tabs.Count == 0)
                tabGroups.Remove(group);
            else
            {
                if (!group.Tabs.Contains(group.Active))
                    group.SetActive(group.Tabs[0]);
                DissolveGroupIfSingle(group);
            }
        }
        if (removedInvalid)
        {
            RememberOpenWindows();
            RememberTabGroups();
        }
        PruneCut();
        contextMenu.Update();
        if (!CanReceiveInput)
            contextMenu.Close();
        RefreshKeyboardFocus();
        ProcessBuyQueue();
        // Suppression reports the button released to the game on subsequent ticks.
        // SMAPI retains the suppression flag until the physical button is released.
        bool leftButtonHeld = IsHeld(input, SButton.MouseLeft);
        if (!CanReceiveInput)
        {
            focusedWindow = null;
            CancelPendingTransfer();
            EndDrag();
            return;
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (!leftButtonHeld)
        {
            FinishItemDrag(cursor);
            FinishWindowDrag(cursor);
            EndDrag();
            return;
        }
        if (itemDragSource != null)
            itemDragActive |= Math.Abs(cursor.X - itemDragStart.X) >= 5 || Math.Abs(cursor.Y - itemDragStart.Y) >= 5;
        if (draggedWindow != null)
            draggedWindow.MoveTo(cursor - dragOffset);
        if (draggedGroup != null)
            draggedGroup.MoveTo(cursor - dragOffset);
        resizedWindow?.ResizeFrom(resizeStartBounds, cursor - resizeStartCursor, resizedWindow.ActiveResizeEdges);
        resizedGroup?.ResizeFrom(resizeStartBounds, cursor - resizeStartCursor, resizedGroup.ActiveResizeEdges);
        contentDragWindow?.DragContent(cursor);
    }

    internal void EndDrag()
    {
        if (draggedWindow != null)
            RememberPlacement(draggedWindow);
        if (draggedGroup != null)
            RememberGroupPlacement(draggedGroup);
        if (resizedWindow != null)
            RememberPlacement(resizedWindow);
        if (resizedGroup != null)
            RememberGroupPlacement(resizedGroup);
        draggedWindow = null;
        draggedGroup = null;
        if (resizedWindow != null)
            resizedWindow.ActiveResizeEdges = WindowResizeEdges.None;
        resizedWindow = null;
        if (resizedGroup != null)
            resizedGroup.ActiveResizeEdges = WindowResizeEdges.None;
        resizedGroup = null;
        contentDragWindow?.EndContentDrag();
        contentDragWindow = null;
        itemDragSource = null;
        draggedItems = Array.Empty<ISalable>();
        pressedItem = null;
        itemDragActive = false;
        collapseSelectionOnRelease = false;
    }

    internal WindowResizeEdges GetResizeCursorEdges(Point cursor)
    {
        if (!CanReceiveInput || contextMenu.IsOpen)
            return WindowResizeEdges.None;
        // Keep the gesture's direction when the pointer leaves the edge or hits a size limit.
        if (resizedWindow is { IsValid: true, IsMinimized: false } resizing && windows.Contains(resizing))
            return resizing.ActiveResizeEdges;
        if (resizedGroup is { } resizingGroup && resizingGroup.Tabs.All(member => member.IsValid && !member.IsMinimized))
            return resizingGroup.ActiveResizeEdges;
        if (draggedWindow != null || draggedGroup != null || contentDragWindow != null || itemDragSource != null)
            return WindowResizeEdges.None;
        // Match click dispatch: covered windows must never supply a resize cursor.
        OsWindow? hovered = GetWindowAt(cursor);
        if (hovered == null)
            return WindowResizeEdges.None;
        WindowTabGroup? hoveredGroup = FindGroup(hovered);
        return hoveredGroup != null ? hoveredGroup.GetResizeEdges(cursor) : hovered.GetResizeEdges(cursor);
    }

}
