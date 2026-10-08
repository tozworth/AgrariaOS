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
    internal void Draw(SpriteBatch batch, Action<SpriteBatch>? overlay = null)
    {
        drawnWindows.Clear();
        if (!isVisible())
        {
            focusedWindow = null;
            CancelPendingTransfer();
            contextMenu.Close();
            ClearKeyboardFocus();
            EndDrag();
            return;
        }

        try
        {
            if (!CanReceiveInput)
                contextMenu.Close();
            drawnViewport = new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);
            Rectangle workArea = Pelicom.GetWorkArea(config);
            float titleTextHeight = windows.Count == 0 ? Game1.smallFont.LineSpacing
                : windows.Max(window => Math.Max(Game1.smallFont.MeasureString(window.Title).Y,
                    Game1.smallFont.MeasureString(window.TitleSubtext).Y));
            int titleHeight = Math.Max(27, (int)MathF.Ceiling(titleTextHeight * config.FontScale)) + 6;
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            // Frames back-to-front: ungrouped windows by index, groups by frontmost member.
            var frames = new List<(int Index, object Frame)>();
            foreach (OsWindow window in windows)
            {
                if (!window.IsValid || window.IsMinimized)
                    continue;
                WindowTabGroup? group = FindGroup(window);
                if (group != null)
                {
                    // Emit the group once at its frontmost member.
                    OsWindow? last = group.Tabs.Where(member => windows.Contains(member) && member.IsValid && !member.IsMinimized)
                        .OrderByDescending(member => windows.IndexOf(member)).FirstOrDefault();
                    if (!ReferenceEquals(window, last))
                        continue;
                    if (group.Tabs.Count < 2)
                        continue;
                    int index = group.Tabs.Where(member => windows.Contains(member))
                        .Select(member => windows.IndexOf(member)).DefaultIfEmpty(-1).Max();
                    frames.Add((index, group));
                }
                else
                    frames.Add((windows.IndexOf(window), window));
            }
            frames.Sort((a, b) => a.Index.CompareTo(b.Index));
            var drawnGroups = new HashSet<WindowTabGroup>();
            foreach (var frame in frames)
            {
                if (frame.Frame is WindowTabGroup group)
                {
                    if (!drawnGroups.Add(group) || group.Tabs.Count < 2)
                        continue;
                    if (group.Tabs.Any(member => !member.IsValid || member.IsMinimized || !windows.Contains(member)))
                        continue;
                    if (group.Layout(workArea, titleHeight, config))
                    {
                        group.Draw(batch, config, cursor);
                        if (group.Active.IsValid && !group.Active.IsMinimized && windows.Contains(group.Active))
                            drawnWindows.Add(group.Active);
                    }
                }
                else if (frame.Frame is OsWindow window)
                {
                    if (window.IsValid && !window.IsMinimized && window.Layout(workArea, titleHeight, config))
                    {
                        window.Draw(batch, config, cursor);
                        drawnWindows.Add(window);
                    }
                }
            }
            // Prune groups that lost members without a click (e.g. failed restores).
            foreach (WindowTabGroup group in tabGroups.ToArray())
            {
                group.Tabs.RemoveAll(member => !windows.Contains(member));
                if (group.Tabs.Count < 2)
                    tabGroups.Remove(group);
            }
            if (itemDragActive && itemDragSource != null)
            {
                InventoryWindow? chestTarget = GetInventoryDropTarget(cursor);
                TradeWindow? sellTarget = chestTarget == null ? GetTradeSellTarget(cursor) : null;
                InventoryWindow? buyTarget = chestTarget == null && sellTarget == null ? GetTradeBuyTarget(cursor) : null;
                Item? mergeTarget = chestTarget == null && sellTarget == null && buyTarget == null ? GetMergeTargetItem(cursor) : null;
                Rectangle? mergeRow = null;
                if (mergeTarget != null && itemDragSource is InventoryWindow mergeSource)
                    mergeRow = mergeSource.GetRowBoundsAt(cursor);
                chestTarget?.DrawDropTarget(batch, config);
                sellTarget?.DrawDropTarget(batch, config);
                buyTarget?.DrawDropTarget(batch, config);
                if (mergeRow.HasValue && itemDragSource is InventoryWindow mergeHighlight)
                    mergeHighlight.DrawMergeTarget(batch, mergeRow.Value, config);
                bool valid = chestTarget != null || sellTarget != null || buyTarget != null || mergeTarget != null;
                if (itemDragSource is InventoryWindow dragInventory)
                    dragInventory.DrawItemDrag(batch, cursor, draggedItems.OfType<Item>().ToArray(), valid, config);
                else if (itemDragSource is TradeWindow dragTrade)
                    dragTrade.DrawItemDrag(batch, cursor, draggedItems, valid, config);
            }
            // Overlays (e.g. the Pelicom hover tip, drawn earlier below these frames)
            // replay above windows but below the context menu.
            overlay?.Invoke(batch);
            contextMenu.Draw(batch, config, cursor);
        }
        catch (Exception ex)
        {
            InvalidateLayout();
            onDrawFailed(ex);
        }
    }

    internal void InvalidateLayout()
    {
        focusedWindow = null;
        CancelPendingTransfer();
        contextMenu.Close();
        ClearKeyboardFocus();
        drawnWindows.Clear();
        foreach (WindowTabGroup group in tabGroups)
            group.ClearDrawnTabs();
        EndDrag();
    }

}
