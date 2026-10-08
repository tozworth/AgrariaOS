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
    private void AssignCutPredicate(InventoryWindow window)
    {
        InventoryWindow captured = window;
        captured.IsCut = item => ReferenceEquals(captured, cutSource) && cutItems.Contains(item);
    }

    private void ClearCut()
    {
        cutItems.Clear();
        cutSource = null;
        cutSourceChest = null;
    }

    private void PruneCut()
    {
        if (cutSource == null || cutItems.Count == 0)
        {
            ClearCut();
            return;
        }
        if (!cutSource.IsValid || !windows.Contains(cutSource) || !ReferenceEquals(cutSource.Chest, cutSourceChest))
        {
            ClearCut();
            return;
        }
        IList<Item> live = cutSourceChest?.GetItemsForPlayer() ?? Game1.player.Items;
        cutItems.RemoveWhere(item => InventoryTransferBatch.IndexOf(live, item) < 0);
        if (cutItems.Count == 0)
            ClearCut();
    }

    private bool TryCutFocusedInventory(InventoryWindow focused)
    {
        if (HasPendingTransfer)
            return true;
        EndDrag();
        Item[] selected = focused.SelectedItems;
        if (selected.Length == 0)
            return true;
        ClearCut();
        cutSource = focused;
        cutSourceChest = focused.Chest;
        foreach (Item item in selected)
            cutItems.Add(item);
        focused.TransferStatus = $"Cut {selected.Length} stack{(selected.Length == 1 ? "" : "s")} — paste to move.";
        return true;
    }

    private bool TryPasteIntoFocusedInventory(InventoryWindow target)
    {
        if (HasPendingTransfer)
            return true;
        PruneCut();
        if (!HasCut || cutSource == null)
        {
            target.TransferStatus = "Nothing to paste.";
            return true;
        }
        if (ReferenceEquals(cutSource, target) && ReferenceEquals(cutSourceChest, target.Chest))
            return true;
        bool sourceIsChest = cutSourceChest != null;
        bool targetIsChest = target.Chest != null;
        if (sourceIsChest == targetIsChest)
        {
            target.TransferStatus = "Can't paste here.";
            return true;
        }
        if (!cutSource.IsValid || !target.IsValid || !Context.IsPlayerFree)
        {
            target.TransferStatus = "Can't paste here.";
            return true;
        }
        EndDrag();
        TransferItems(cutSource, target, cutItems.ToArray());
        return true;
    }

}
