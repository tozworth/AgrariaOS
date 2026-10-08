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
    internal WindowTabGroup? FindGroup(OsWindow window)
        => tabGroups.FirstOrDefault(group => group.Contains(window));

    private void BringGroupToFront(WindowTabGroup group)
    {
        foreach (OsWindow member in group.Tabs.ToArray())
        {
            windows.Remove(member);
            windows.Add(member);
        }
    }

    private bool IsFrontmost(OsWindow window)
    {
        if (!window.IsValid || window.IsMinimized || !windows.Contains(window))
            return false;
        WindowTabGroup? group = FindGroup(window);
        if (group != null && !ReferenceEquals(group.Active, window))
            return false;
        int frameIndex(OsWindow candidate)
        {
            WindowTabGroup? candidateGroup = FindGroup(candidate);
            if (candidateGroup != null)
                return candidateGroup.Tabs.Where(member => windows.Contains(member)).Select(member => windows.IndexOf(member)).DefaultIfEmpty(-1).Max();
            return windows.IndexOf(candidate);
        }
        int targetIndex = group != null
            ? group.Tabs.Where(member => windows.Contains(member)).Select(member => windows.IndexOf(member)).DefaultIfEmpty(-1).Max()
            : windows.IndexOf(window);
        if (targetIndex < 0)
            return false;
        foreach (OsWindow candidate in windows)
        {
            if (!candidate.IsValid || candidate.IsMinimized)
                continue;
            WindowTabGroup? candidateGroup = FindGroup(candidate);
            if (candidateGroup != null && !ReferenceEquals(candidateGroup.Active, candidate))
                continue;
            if (frameIndex(candidate) > targetIndex)
                return false;
        }
        return true;
    }

    private void CloseWindow(OsWindow window)
    {
        WindowTabGroup? group = FindGroup(window);
        if (group != null)
        {
            group.RemoveTab(window);
            DissolveGroupIfSingle(group);
        }
        RememberPlacement(window);
        windows.Remove(window);
        try { window.Close(); }
        catch { }
        if (window is TradeWindow)
            pendingBuys.Clear();
        if (ReferenceEquals(focusedWindow, window))
            focusedWindow = null;
        if (ReferenceEquals(keyboardWindow, window))
            ClearKeyboardFocus();
    }

    private void CloseGroup(WindowTabGroup group)
    {
        foreach (OsWindow member in group.Tabs.ToArray())
        {
            RememberPlacement(member);
            windows.Remove(member);
            try { member.Close(); }
            catch { }
        }
        tabGroups.Remove(group);
        focusedWindow = null;
        ClearKeyboardFocus();
    }

    private void DetachTab(WindowTabGroup group, OsWindow tab, Point? cursor)
    {
        if (!group.Contains(tab) || group.Tabs.Count < 2)
            return;
        group.RemoveTab(tab);
        DissolveGroupIfSingle(group);
        // Keep the detached window where the group was, nudged so the frame is visible.
        WindowPlacement? placement = group.GetPlacement();
        Point origin = placement != null ? new Point(placement.X, placement.Y) : tab.Bounds.Location;
        Point dimensions = placement != null ? new Point(placement.Width, placement.Height) : tab.Bounds.Size;
        if (dimensions.X <= 0 || dimensions.Y <= 0)
            dimensions = tab.DefaultSize;
        Point offset = new(origin.X + 24, origin.Y + 24);
        if (cursor.HasValue)
            offset = cursor.Value - new Point(Math.Min(120, dimensions.X / 2), 12);
        tab.RestorePlacement(new WindowPlacement { X = offset.X, Y = offset.Y, Width = dimensions.X, Height = dimensions.Y });
        windows.Remove(tab);
        windows.Add(tab);
        tab.IsMinimized = false;
        focusedWindow = tab;
        RememberPlacement(tab);
        RememberGroupPlacement(group);
        RememberOpenWindows();
        RememberTabGroups();
    }

    private void FinishWindowDrag(Point cursor)
    {
        try
        {
            if (draggedWindow != null)
            {
                if (FindDropTarget(cursor, draggedWindow, null) is { } target)
                    DockWindow(draggedWindow, target);
            }
            else if (draggedGroup != null)
            {
                if (FindDropTarget(cursor, null, draggedGroup) is { } target)
                    DockGroup(draggedGroup, target);
            }
        }
        catch { }
    }

    private object? FindDropTarget(Point cursor, OsWindow? draggedSingle, WindowTabGroup? draggedGroup)
    {
        var candidates = new List<(int Index, object Frame)>();
        foreach (OsWindow window in windows)
        {
            if (!window.IsValid || window.IsMinimized || !windows.Contains(window))
                continue;
            if (draggedSingle != null && ReferenceEquals(window, draggedSingle))
                continue;
            if (draggedGroup != null && draggedGroup.Contains(window))
                continue;
            WindowTabGroup? group = FindGroup(window);
            if (group != null)
            {
                // Only consider the group once, at its frontmost member.
                if (!ReferenceEquals(window, group.Tabs.Where(member => windows.Contains(member))
                        .OrderByDescending(member => windows.IndexOf(member)).FirstOrDefault()))
                    continue;
                if (group.Bounds.IsEmpty || !group.TitleBounds.Contains(cursor))
                    continue;
                int index = group.Tabs.Where(member => windows.Contains(member))
                    .Select(member => windows.IndexOf(member)).DefaultIfEmpty(-1).Max();
                candidates.Add((index, group));
            }
            else
            {
                if (window.Bounds.IsEmpty || !window.TitleBounds.Contains(cursor))
                    continue;
                candidates.Add((windows.IndexOf(window), window));
            }
        }
        if (candidates.Count == 0)
            return null;
        candidates.Sort((a, b) => b.Index.CompareTo(a.Index));
        return candidates[0].Frame;
    }

    private void DockWindow(OsWindow dragged, object target)
    {
        if (!dragged.IsValid || !windows.Contains(dragged) || dragged.IsMinimized)
            return;
        ClearKeyboardFocus();
        if (target is WindowTabGroup targetGroup)
        {
            if (targetGroup.Contains(dragged) || !targetGroup.Tabs.All(member => member.IsValid))
                return;
            WindowPlacement? placement = targetGroup.GetPlacement() ?? dragged.GetPlacement();
            targetGroup.AddTab(dragged);
            targetGroup.SetActive(dragged);
            if (placement != null)
                targetGroup.InitFromPlacement(placement);
            BringGroupToFront(targetGroup);
            focusedWindow = dragged;
            RememberGroupPlacement(targetGroup);
        }
        else if (target is OsWindow targetWindow)
        {
            if (ReferenceEquals(targetWindow, dragged) || !targetWindow.IsValid || targetWindow.IsMinimized)
                return;
            if (FindGroup(targetWindow) != null || FindGroup(dragged) != null)
                return;
            WindowPlacement? placement = targetWindow.GetPlacement() ?? dragged.GetPlacement()
                ?? new WindowPlacement { X = targetWindow.Bounds.X, Y = targetWindow.Bounds.Y, Width = targetWindow.Bounds.Width, Height = targetWindow.Bounds.Height };
            var group = new WindowTabGroup(new[] { targetWindow, dragged }, dragged, placement);
            foreach (OsWindow member in group.Tabs)
                member.RestorePlacement(placement);
            tabGroups.Add(group);
            BringGroupToFront(group);
            focusedWindow = dragged;
            RememberGroupPlacement(group);
        }
        else
            return;
        RememberOpenWindows();
        RememberTabGroups();
    }

    private void DockGroup(WindowTabGroup dragged, object target)
    {
        if (dragged.Tabs.Count == 0 || dragged.Tabs.Any(member => !member.IsValid || !windows.Contains(member)))
            return;
        ClearKeyboardFocus();
        if (target is WindowTabGroup targetGroup)
        {
            if (ReferenceEquals(targetGroup, dragged))
                return;
            WindowPlacement? placement = targetGroup.GetPlacement() ?? dragged.GetPlacement();
            foreach (OsWindow member in dragged.Tabs.ToArray())
            {
                if (!targetGroup.Contains(member))
                    targetGroup.AddTab(member);
            }
            targetGroup.SetActive(dragged.Active);
            tabGroups.Remove(dragged);
            if (placement != null)
                targetGroup.InitFromPlacement(placement);
            BringGroupToFront(targetGroup);
            focusedWindow = targetGroup.Active;
            RememberGroupPlacement(targetGroup);
        }
        else if (target is OsWindow targetWindow)
        {
            if (dragged.Contains(targetWindow))
                return;
            WindowTabGroup? existing = FindGroup(targetWindow);
            if (existing != null)
            {
                DockGroup(dragged, existing);
                return;
            }
            WindowPlacement? placement = targetWindow.GetPlacement() ?? dragged.GetPlacement();
            var members = new List<OsWindow> { targetWindow };
            members.AddRange(dragged.Tabs.Where(member => !ReferenceEquals(member, targetWindow)));
            var group = new WindowTabGroup(members, dragged.Active, placement
                ?? new WindowPlacement { X = targetWindow.Bounds.X, Y = targetWindow.Bounds.Y, Width = targetWindow.Bounds.Width, Height = targetWindow.Bounds.Height });
            foreach (OsWindow member in group.Tabs)
                member.RestorePlacement(group.GetPlacement() ?? placement
                    ?? new WindowPlacement { X = targetWindow.Bounds.X, Y = targetWindow.Bounds.Y, Width = targetWindow.Bounds.Width, Height = targetWindow.Bounds.Height });
            tabGroups.Remove(dragged);
            tabGroups.Add(group);
            BringGroupToFront(group);
            focusedWindow = group.Active;
            RememberGroupPlacement(group);
        }
        else
            return;
        RememberOpenWindows();
        RememberTabGroups();
    }

}
