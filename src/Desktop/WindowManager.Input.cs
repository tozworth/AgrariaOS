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
    internal bool ReceiveClick(Point cursor, bool leftClick, IInputHelper input, Func<bool>? overlayClick = null)
    {
        if (!leftClick)
            EndDrag();
        if (CanReceiveInput && contextMenu.ReceiveClick(cursor, leftClick))
            return true;
        if (CanReceiveInput && overlayClick?.Invoke() == true)
            return true;
        OsWindow? window = GetWindowAt(cursor);
        if (window == null)
            return CanReceiveInput && (HasPendingTransfer || itemDragSource != null || draggedWindow != null
                || draggedGroup != null || resizedWindow != null || resizedGroup != null || contentDragWindow != null);
        ClearKeyboardFocus();
        WindowTabGroup? group = FindGroup(window);
        if (group != null)
        {
            if (leftClick)
                return ReceiveGroupLeftClick(group, window, cursor, input);
            else
                return ReceiveGroupRightClick(group, window, cursor);
        }

        focusedWindow = window;

        if (leftClick)
        {
            EndDrag();
            windows.Remove(window);
            windows.Add(window);
            if (window.CloseBounds.Contains(cursor))
            {
                CloseWindow(window);
                focusedWindow = GetFrontmostActive();
                EndDrag();
            }
            else if (window.MinimizeBounds.Contains(cursor))
            {
                MinimizeWindow(window);
            }
            else if (window.GetTitlebarActionAt(cursor) is { } titlebarAction)
            {
                titlebarAction.Invoke();
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
                if (window.WantsKeyboardFocus)
                {
                    keyboardWindow = window;
                    keyboardDispatcher = Game1.keyboardDispatcher;
                }
                contentDragWindow = window;
                if (!HasPendingTransfer)
                    BeginItemDrag(window, cursor, input);
            }
        }
        else
        {
            ContextMenuItem[]? items = window.GetContextMenuItems(cursor);
            if (items != null)
            {
                ContextMenuInfo? info = window.GetContextMenuInfo(cursor);
                ShowContextMenu(cursor, items, isValid: () => window.IsValid && !window.IsMinimized
                    && windows.Contains(window) && (info?.IsValid() ?? true), info: info);
            }
            else
                window.ReceiveContentClick(cursor, leftClick: false);
        }
        RememberOpenWindows();
        // Suppress the whole press (including held repeats) before native HUD/world dispatch.
        return true;
    }

    private OsWindow? GetFrontmostActive()
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            OsWindow candidate = windows[i];
            if (!candidate.IsValid || candidate.IsMinimized)
                continue;
            WindowTabGroup? candidateGroup = FindGroup(candidate);
            if (candidateGroup != null)
            {
                if (ReferenceEquals(candidateGroup.Active, candidate))
                    return candidate;
                continue;
            }
            return candidate;
        }
        return null;
    }

    private bool ReceiveGroupLeftClick(WindowTabGroup group, OsWindow window, Point cursor, IInputHelper input)
    {
        EndDrag();
        // Resolve actions before switching tabs: selecting a tab must never fire
        // an action that appears in the newly active window's chrome.
        WindowTitlebarAction? titlebarAction = group.Active.GetTitlebarActionAt(cursor);
        // The press identifies the frame; tab selection decides the active member.
        OsWindow? pressedTab = titlebarAction != null ? null
            : group.GetTabAt(cursor) ?? (group.TitleBounds.Contains(cursor) ? group.Active : null);
        if (pressedTab != null && !ReferenceEquals(pressedTab, group.Active))
        {
            group.SetActive(pressedTab);
            window = pressedTab;
            RememberTabGroups();
        }
        BringGroupToFront(group);
        focusedWindow = group.Active;

        if (group.CloseBounds.Contains(cursor))
        {
            CloseGroup(group);
            focusedWindow = GetFrontmostActive();
            EndDrag();
        }
        else if (group.MinimizeBounds.Contains(cursor))
        {
            MinimizeWindow(group.Active);
        }
        else if (titlebarAction != null)
        {
            titlebarAction.Invoke();
        }
        else if (group.GetResizeEdges(cursor) is var edges && edges != WindowResizeEdges.None)
        {
            resizedGroup = group;
            group.ActiveResizeEdges = edges;
            resizeStartCursor = cursor;
            resizeStartBounds = group.Bounds;
        }
        else if (group.TitleBounds.Contains(cursor))
        {
            bool detachModifier = IsHeld(input, SButton.LeftShift) || IsHeld(input, SButton.RightShift);
            OsWindow tab = pressedTab ?? group.Active;
            if (detachModifier && group.Tabs.Count > 1)
            {
                DetachTab(group, tab, cursor);
                // Continue dragging the detached window from the same cursor offset.
                draggedWindow = tab;
                dragOffset = cursor - tab.Bounds.Location;
                focusedWindow = tab;
            }
            else
            {
                draggedGroup = group;
                dragOffset = cursor - group.Bounds.Location;
            }
        }
        else
        {
            OsWindow active = group.Active;
            active.ReceiveContentClick(cursor, leftClick: true);
            if (active.WantsKeyboardFocus)
            {
                keyboardWindow = active;
                keyboardDispatcher = Game1.keyboardDispatcher;
            }
            contentDragWindow = active;
            if (!HasPendingTransfer)
                BeginItemDrag(active, cursor, input);
            focusedWindow = active;
        }
        RememberOpenWindows();
        RememberTabGroups();
        return true;
    }

    private bool ReceiveGroupRightClick(WindowTabGroup group, OsWindow window, Point cursor)
    {
        if (group.TitleBounds.Contains(cursor))
        {
            OsWindow tab = group.GetTabAt(cursor) ?? group.Active;
            OsWindow capturedTab = tab;
            WindowTabGroup capturedGroup = group;
            string title;
            try { title = tab.Title; }
            catch { title = "Tab"; }
            var items = new List<ContextMenuItem>();
            if (group.Tabs.Count > 1)
            {
                items.Add(new ContextMenuItem("Detach tab", () => DetachTab(capturedGroup, capturedTab, null)));
                items.Add(new ContextMenuItem("Close tab", () =>
                {
                    CloseWindow(capturedTab);
                    RememberOpenWindows();
                    RememberTabGroups();
                }));
            }
            items.Add(new ContextMenuItem("Close window", () =>
            {
                if (tabGroups.Contains(capturedGroup))
                    CloseGroup(capturedGroup);
                RememberOpenWindows();
                RememberTabGroups();
            }));
            ShowContextMenu(cursor, items.ToArray(), title: title,
                isValid: () => capturedTab.IsValid && windows.Contains(capturedTab) && tabGroups.Contains(capturedGroup)
                    && capturedGroup.Contains(capturedTab));
            RememberOpenWindows();
            return true;
        }

        OsWindow active = group.Active;
        focusedWindow = active;
        ContextMenuItem[]? contentItems = active.GetContextMenuItems(cursor);
        if (contentItems != null)
        {
            ContextMenuInfo? info = active.GetContextMenuInfo(cursor);
            ShowContextMenu(cursor, contentItems, isValid: () => active.IsValid && !active.IsMinimized
                && windows.Contains(active) && (info?.IsValid() ?? true), info: info);
        }
        else
            active.ReceiveContentClick(cursor, leftClick: false);
        RememberOpenWindows();
        return true;
    }

    internal bool ReceiveScroll(Point cursor, int delta)
    {
        if (CanReceiveInput && contextMenu.ReceiveScroll(delta))
            return true;
        OsWindow? window = GetWindowAt(cursor);
        if (window == null)
            return false;

        focusedWindow = window;
        window.ReceiveScroll(delta);
        return true;
    }






    private OsWindow? GetWindowAt(Point cursor)
    {
        if (!CanReceiveInput)
            return null;

        return drawnWindows.LastOrDefault(window => window.IsValid && !window.IsMinimized
            && windows.Contains(window) && window.Bounds.Contains(cursor));
    }






}
