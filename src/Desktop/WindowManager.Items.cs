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
    private void BeginItemDrag(OsWindow window, Point cursor, IInputHelper input)
    {
        bool control = IsHeld(input, SButton.LeftControl) || IsHeld(input, SButton.RightControl);
        bool shift = IsHeld(input, SButton.LeftShift) || IsHeld(input, SButton.RightShift);
        if (window is InventoryWindow inventory && !HasPendingTransfer)
        {
            pressedItem = inventory.SelectRow(cursor, control, shift);
            if (pressedItem != null)
            {
                itemDragSource = inventory;
                draggedItems = inventory.SelectedItems;
                itemDragStart = cursor;
                collapseSelectionOnRelease = !control && !shift;
            }
        }
        else if (window is TradeWindow trade && !HasPendingTransfer)
        {
            pressedItem = trade.SelectRow(cursor, control, shift);
            if (pressedItem != null)
            {
                itemDragSource = trade;
                draggedItems = trade.SelectedItems;
                itemDragStart = cursor;
                collapseSelectionOnRelease = !control && !shift;
            }
        }
    }

    private void FinishItemDrag(Point cursor)
    {
        if (itemDragSource == null)
            return;
        itemDragActive |= Math.Abs(cursor.X - itemDragStart.X) >= 5 || Math.Abs(cursor.Y - itemDragStart.Y) >= 5;
        if (itemDragActive)
        {
            if (GetInventoryDropTarget(cursor) is { } chestTarget
                && itemDragSource is InventoryWindow chestSource)
                TransferItems(chestSource, chestTarget, draggedItems.OfType<Item>().ToArray());
            else if (GetTradeSellTarget(cursor) is { } sellTarget
                && itemDragSource is InventoryWindow sellSource)
                SellDraggedItems(sellSource, sellTarget, draggedItems.OfType<Item>().ToArray());
            else if (GetTradeBuyTarget(cursor) is not null
                && itemDragSource is TradeWindow)
                EnqueueBuys(draggedItems.ToArray());
            else if (GetMergeTargetItem(cursor) is { } mergeTarget
                && itemDragSource is InventoryWindow mergeSource)
                MergeStacks(mergeSource, mergeTarget, draggedItems.OfType<Item>().ToArray());
        }
        else if (collapseSelectionOnRelease && pressedItem != null)
        {
            if (itemDragSource is InventoryWindow inventory && pressedItem is Item item)
                inventory.CollapseSelection(item);
            else if (itemDragSource is TradeWindow trade)
                trade.CollapseSelection(pressedItem);
        }
    }

    private InventoryWindow? GetInventoryDropTarget(Point cursor)
    {
        if (itemDragSource is not InventoryWindow source || !source.IsValid || HasPendingTransfer
            || !Context.IsPlayerFree || GetWindowAt(cursor) is not InventoryWindow target
            || ReferenceEquals(target, itemDragSource) || !target.ContainsDropPoint(cursor)
            || (source.Chest == null) == (target.Chest == null))
            return null;
        Chest chest = (source.Chest ?? target.Chest)!;
        return Utility.tileWithinRadiusOfPlayer((int)chest.TileLocation.X, (int)chest.TileLocation.Y, 1, Game1.player)
            ? target : null;
    }

    private TradeWindow? GetTradeSellTarget(Point cursor)
    {
        if (itemDragSource is not InventoryWindow source || !source.IsValid || HasPendingTransfer
            || !Context.IsPlayerFree || draggedItems.Length == 0)
            return null;
        if (source.Chest != null)
            return null;
        if (GetWindowAt(cursor) is not TradeWindow target || !target.IsValid
            || !target.ContainsDropPoint(cursor) || target.Shop == null)
            return null;
        return target;
    }

    private InventoryWindow? GetTradeBuyTarget(Point cursor)
    {
        if (itemDragSource is not TradeWindow source || !source.IsValid || HasPendingTransfer
            || !Context.IsPlayerFree || draggedItems.Length == 0)
            return null;
        if (source.Shop == null)
            return null;
        if (GetWindowAt(cursor) is not InventoryWindow target || !target.IsValid
            || target.Chest != null || !target.ContainsDropPoint(cursor))
            return null;
        return target;
    }

    private Item? GetMergeTargetItem(Point cursor)
    {
        if (itemDragSource is not InventoryWindow source || !source.IsValid || HasPendingTransfer
            || !Context.IsPlayerFree || draggedItems.Length == 0)
            return null;
        if (GetWindowAt(cursor) is not InventoryWindow hovered || !ReferenceEquals(hovered, itemDragSource))
            return null;
        Item? target = hovered.GetRowAt(cursor);
        if (target == null || draggedItems.Any(dragged => ReferenceEquals(dragged, target)))
            return null;
        // Ignore stale rows whose stack was removed since the last render.
        var liveItems = (source.Chest?.GetItemsForPlayer() ?? Game1.player.Items)
            .Where(item => item != null).ToArray();
        if (!liveItems.Any(current => ReferenceEquals(current, target)))
            return null;
        foreach (ISalable draggedSalable in draggedItems)
        {
            if (draggedSalable is not Item dragged)
                continue;
            if (ReferenceEquals(dragged, target) || dragged.Stack <= 0)
                continue;
            if (!liveItems.Any(current => ReferenceEquals(current, dragged)))
                continue;
            if (target.canStackWith(dragged) && target.Stack < target.maximumStackSize())
                return target;
        }
        return null;
    }












}
