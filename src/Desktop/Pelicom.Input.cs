using AgrariaOS.Configuration;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop;

partial class Pelicom : IClickableMenu
{
    private const int DragThreshold = 6;

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (!IsInteractive || !drawnBounds.Contains(x, y))
            return;
        for (int i = 0; i < Entries.Length; i++)
        {
            if (!drawnSlots[i].IsEmpty && drawnSlots[i].Contains(x, y))
            {
                ActivateEntry(i);
                return;
            }
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (isWithinBounds(x, y))
        {
            CancelPress();
            showContextMenu(new Point(x, y), new[]
            {
                new ContextMenuItem("Auto-hide", () => autoHideChecked = !autoHideChecked, autoHideChecked),
                new ContextMenuItem("Reset icon order", () => ResetOrder()),
                new ContextMenuItem("Position", Children: Enum.GetValues<PelicomEdge>().Select(edge =>
                    new ContextMenuItem(edge.ToString(), () => SetPosition(edge), GetPosition(config) == edge)).ToArray())
            });
        }
    }

    private void ActivateEntry(int entry)
    {
        if (!IsInteractive)
            return;
        string? windowId = entry switch
        {
            OverviewEntryIndex => OverviewWindow.WindowId,
            MailEntryIndex => MailWindow.WindowId,
            JournalEntryIndex => JournalWindow.WindowId,
            InventoryEntryIndex => InventoryWindow.WindowId,
            LivestockEntryIndex => LivestockWindow.WindowId,
            SocialEntryIndex => SocialWindow.WindowId,
            MiningEntryIndex => MiningLedgerWindow.WindowId,
            BundlesEntryIndex => BundlesWindow.WindowId,
            WalletEntryIndex => WalletWindow.WindowId,
            NotificationsEntryIndex => NotificationsWindow.WindowId,
            ChatEntryIndex => ChatWindow.WindowId,
            NotesEntryIndex => NotesWindow.WindowId,
            OptionsEntryIndex => OptionsWindow.WindowId,
            _ => null
        };
        if (windowId != null && minimizeOpenWindow(windowId))
            return;
        if (entry == OverviewEntryIndex)
            openOverview();
        else if (entry == MailEntryIndex)
            openMail();
        else if (entry == JournalEntryIndex)
            openJournal();
        else if (entry == InventoryEntryIndex)
            openInventory();
        else if (entry == LivestockEntryIndex)
            openLivestock();
        else if (entry == SocialEntryIndex)
            openSocial();
        else if (entry == MiningEntryIndex)
            openMiningLedger();
        else if (entry == BundlesEntryIndex)
            openBundles();
        else if (entry == WalletEntryIndex)
            openWallet();
        else if (entry == NotificationsEntryIndex)
            openNotifications();
        else if (entry == ChatEntryIndex)
            openChat();
        else if (entry == NotesEntryIndex)
            openNotes();
        else if (entry == OptionsEntryIndex)
            openOptions();
        else if (entry == ExitEntryIndex)
            // Terminate immediately without entering the game's save or shutdown flow.
            Environment.Exit(0);
    }

    internal bool IsDragging => dragging;

    internal bool ReceivePress(Point cursor)
    {
        if (!IsInteractive || !drawnBounds.Contains(cursor.X, cursor.Y))
            return false;
        pressedOnStrip = true;
        pressCursor = cursor;
        pressedEntry = null;
        for (int i = 0; i < Entries.Length; i++)
        {
            if (!drawnSlots[i].IsEmpty && drawnSlots[i].Contains(cursor.X, cursor.Y))
            {
                pressedEntry = i;
                break;
            }
        }
        dragging = false;
        dropPosition = null;
        return true;
    }

    internal void Update(IInputHelper input)
    {
        if (!pressedOnStrip)
            return;
        if (!IsInteractive)
        {
            CancelPress();
            return;
        }
        bool held;
        try { held = input.IsDown(SButton.MouseLeft) || input.IsSuppressed(SButton.MouseLeft); }
        catch { held = false; }
        if (!held)
        {
            int? entry = pressedEntry;
            bool wasDragging = dragging;
            int? drop = dropPosition;
            bool intoEndGroup = dropInEndGroup;
            pressedOnStrip = false;
            pressedEntry = null;
            dragging = false;
            dropPosition = null;
            if (wasDragging)
            {
                if (entry.HasValue && drop.HasValue)
                    CommitReorder(entry.Value, drop.Value, intoEndGroup);
            }
            else if (entry.HasValue)
                ActivateEntry(entry.Value);
            return;
        }
        if (!pressedEntry.HasValue)
            return;
        if (!dragging)
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            if (Math.Abs(cursor.X - pressCursor.X) < DragThreshold && Math.Abs(cursor.Y - pressCursor.Y) < DragThreshold)
                return;
            dragging = true;
        }
        UpdateDropPosition();
    }

    private void CancelPress()
    {
        pressedOnStrip = false;
        pressedEntry = null;
        dragging = false;
        dropPosition = null;
    }

    private void UpdateDropPosition()
    {
        if (!pressedEntry.HasValue)
        {
            dropPosition = null;
            return;
        }
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        List<int> order = GetDisplayOrder(out int endCount);
        int split = order.Count - endCount;
        if (order.IndexOf(pressedEntry.Value) < split)
            split--;
        order.Remove(pressedEntry.Value);
        bool horizontal;
        try { horizontal = GetPosition(config) is PelicomEdge.Top or PelicomEdge.Bottom; }
        catch { horizontal = false; }
        int cursorAxis = horizontal ? cursor.X : cursor.Y;
        dropInEndGroup = cursorAxis >= drawnEndStart;
        int groupStart = dropInEndGroup ? split : 0;
        int groupEnd = dropInEndGroup ? order.Count : split;
        for (int pos = groupStart; pos < groupEnd; pos++)
        {
            Rectangle slot = drawnSlots[order[pos]];
            if (slot.IsEmpty)
                continue;
            int center = horizontal ? slot.Center.X : slot.Center.Y;
            if (cursorAxis < center)
            {
                dropPosition = pos;
                return;
            }
        }
        dropPosition = groupEnd;
    }
}
