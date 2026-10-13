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
    internal bool ReceiveKeyboardButton(SButton button, IInputHelper input)
    {
        if (CanReceiveInput && !contextMenu.IsOpen && Game1.game1.HasKeyboardFocus()
            && button == SButton.A && (IsHeld(input, SButton.LeftControl) || IsHeld(input, SButton.RightControl))
            && focusedWindow is { IsValid: true, IsMinimized: false } selectWindow
            && drawnWindows.Contains(selectWindow) && IsFrontmost(selectWindow))
        {
            if (selectWindow is InventoryWindow inventory)
            {
                EndDrag();
                inventory.SelectAll();
                return true;
            }
            if (selectWindow is TradeWindow trade)
            {
                EndDrag();
                trade.SelectAll();
                return true;
            }
        }
        if (CanReceiveInput && !contextMenu.IsOpen && Game1.game1.HasKeyboardFocus()
            && (button == SButton.X || button == SButton.V)
            && (IsHeld(input, SButton.LeftControl) || IsHeld(input, SButton.RightControl))
            && focusedWindow is { IsValid: true, IsMinimized: false } clipWindow
            && drawnWindows.Contains(clipWindow) && IsFrontmost(clipWindow))
        {
            // Trade is ignored; only backpack/chest inventories support cut/paste.
            if (clipWindow is InventoryWindow clipInventory)
            {
                if (button == SButton.X)
                    return TryCutFocusedInventory(clipInventory);
                return TryPasteIntoFocusedInventory(clipInventory);
            }
        }
        if (CanReceiveInput && button == SButton.Escape && (itemDragSource != null || HasPendingTransfer || HasCut))
        {
            ClearCut();
            CancelPendingTransfer();
            EndDrag();
            return true;
        }
        if (CanReceiveInput && contextMenu.IsOpen && button == SButton.Escape)
        {
            contextMenu.Close();
            return true;
        }
        if (CanReceiveInput && !contextMenu.IsOpen && Game1.game1.HasKeyboardFocus()
            && focusedWindow is { IsValid: true, IsMinimized: false } focused
            && drawnWindows.Contains(focused) && IsFrontmost(focused)
            && focused.ReceiveShortcut(button))
            return true;
        if (!RefreshKeyboardFocus() || !Game1.game1.HasKeyboardFocus() || !button.TryGetKeyboard(out Keys key))
            return false;
        if (key is Keys.Escape or Keys.Tab)
            ClearKeyboardFocus();
        else if (keyboardWindow!.KeyboardSubscriber is TextEntry entry)
        {
            bool control = IsHeld(input, SButton.LeftControl) || IsHeld(input, SButton.RightControl);
            bool shift = IsHeld(input, SButton.LeftShift) || IsHeld(input, SButton.RightShift);
            if (!control || !entry.ReceiveShortcut(key))
                entry.Navigate(key, extendSelection: shift);
        }
        else if (key == Keys.V && (input.IsDown(SButton.LeftControl) || input.IsSuppressed(SButton.LeftControl)
            || input.IsDown(SButton.RightControl) || input.IsSuppressed(SButton.RightControl)))
        {
            // Poll reads SMAPI's suppressed keys, so paste must happen before suppression.
            string pasted = "";
            if (DesktopClipboard.GetText(ref pasted) && !string.IsNullOrEmpty(pasted))
                keyboardWindow!.KeyboardSubscriber!.RecieveTextInput(pasted);
        }
        else
            keyboardWindow!.ReceiveKey(key);
        return true;
    }

    internal void SuppressKeyboardInput(IInputHelper input)
    {
        if (!RefreshKeyboardFocus() || !Game1.game1.HasKeyboardFocus())
            return;
        foreach (SButton button in KeyboardButtons)
        {
            if (input.IsDown(button))
                input.Suppress(button);
        }
    }

    internal void ClearKeyboardFocus()
    {
        IKeyboardSubscriber? subscriber = keyboardWindow?.KeyboardSubscriber;
        if (subscriber != null)
        {
            if (keyboardDispatcher?.Subscriber == subscriber)
                keyboardDispatcher.Subscriber = null;
            keyboardWindow!.ReleaseKeyboardFocus();
        }
        keyboardWindow = null;
        keyboardDispatcher = null;
    }

    private bool RefreshKeyboardFocus()
    {
        if (keyboardWindow == null)
            return false;
        IKeyboardSubscriber? subscriber = keyboardWindow.KeyboardSubscriber;
        if (subscriber == null || !keyboardWindow.WantsKeyboardFocus || !isVisible() || Game1.IsChatting || keyboardWindow.IsMinimized || keyboardWindow.Bounds.IsEmpty
            || keyboardDispatcher != Game1.keyboardDispatcher
            || !IsFrontmost(keyboardWindow)
            || (Game1.keyboardDispatcher.Subscriber != null && Game1.keyboardDispatcher.Subscriber != subscriber))
        {
            ClearKeyboardFocus();
            return false;
        }
        // UpdateOther clears subscribers outside vanilla menus. Restore our owner after that
        // tick and before native text events arrive; never take focus from another text field.
        Game1.keyboardDispatcher.Subscriber = subscriber;
        return true;
    }

    private bool CanReceiveInput => isVisible() && !Game1.IsChatting
        && drawnViewport == new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);

    internal bool ShowContextMenu(Point cursor, ContextMenuItem[] items, string? title = null, Func<bool>? isValid = null,
        ContextMenuInfo? info = null)
    {
        CancelPendingTransfer();
        EndDrag();
        ClearKeyboardFocus();
        return contextMenu.Open(cursor, ContextMenuItems.WithInfo(items, lookupAnything.Available ? info : null, OpenLookup),
            config, title, isValid);
    }

    internal void CloseContextMenu() => contextMenu.Close();

}
