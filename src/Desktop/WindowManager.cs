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
using StardewValley.Objects;

namespace AgrariaOS.Desktop;

internal sealed class WindowManager
{
    private readonly ModConfig config;
    private readonly Func<bool> isVisible;
    private readonly Action<Exception> onDrawFailed;
    private readonly Action saveConfig;
    private readonly Action<Exception> onTransferFailed;
    private readonly LookupAnythingIntegration lookupAnything;
    private readonly Func<ClickToMove> getMovement;
    private Dictionary<string, WindowPlacement>? layouts;
    // Last entry is the front window, in both drawing and hit testing.
    private readonly List<OsWindow> windows = new();
    private readonly List<OsWindow> drawnWindows = new();
    private readonly ContextMenu contextMenu = new();
    private Point drawnViewport;
    private OsWindow? draggedWindow;
    private OsWindow? resizedWindow;
    private OsWindow? contentDragWindow;
    private Point dragOffset;
    private Point resizeStartCursor;
    private Rectangle resizeStartBounds;
    private OsWindow? keyboardWindow;
    private OsWindow? focusedWindow;
    private InventoryWindow? itemDragSource;
    private Item[] draggedItems = Array.Empty<Item>();
    private Item? pressedItem;
    private Point itemDragStart;
    private bool itemDragActive;
    private bool collapseSelectionOnRelease;
    private (int Version, InventoryWindow Source, InventoryWindow Target)? pendingTransfer;
    private bool HasPendingTransfer => pendingTransfer.HasValue;
    private int transferVersion;
    private KeyboardDispatcher? keyboardDispatcher;
    private static readonly SButton[] KeyboardButtons = Enum.GetValues<SButton>()
        .Where(button => button.TryGetKeyboard(out _)).ToArray();

    internal WindowManager(ModConfig config, Func<bool> isVisible, Action<Exception> onDrawFailed,
        Action saveConfig, Action<Exception> onTransferFailed, LookupAnythingIntegration lookupAnything,
        Func<ClickToMove> getMovement)
    {
        this.config = config;
        this.isVisible = isVisible;
        this.onDrawFailed = onDrawFailed;
        this.saveConfig = saveConfig;
        this.onTransferFailed = onTransferFailed;
        this.lookupAnything = lookupAnything;
        this.getMovement = getMovement;
    }

    internal void OpenOverview() => Open(OverviewWindow.WindowId, () => new OverviewWindow(lookupAnything, getMovement, FocusWorld));
    internal void OpenMail() => Open(MailWindow.WindowId, () => new MailWindow());
    internal void OpenInventory() => Open(InventoryWindow.WindowId, () => new InventoryWindow(config, lookupAnything: lookupAnything));
    internal void OpenLivestock() => Open(LivestockWindow.WindowId, () => new LivestockWindow());
    internal void OpenMiningLedger(MiningLedger ledger) => Open(MiningLedgerWindow.WindowId, () => new MiningLedgerWindow(ledger));
    internal void OpenChestInventory(Chest chest)
    {
        Open(InventoryWindow.ChestWindowId, () => new InventoryWindow(config, chest, lookupAnything));
        ((InventoryWindow)windows.Last()).ShowChest(chest);
    }
    internal void OpenNotifications(NotificationHistory history) => Open(NotificationsWindow.WindowId, () => new NotificationsWindow(history));
    internal void OpenChat(ChatSession session, bool showLocal = false)
    {
        Open(ChatWindow.WindowId, () => new ChatWindow(session, config, saveConfig));
        if (showLocal)
            ((ChatWindow)windows.Last()).SelectTab("Local");
    }

    internal void Open(string id, Func<OsWindow> create)
    {
        CancelPendingTransfer();
        EndDrag();
        contextMenu.Close();
        ClearKeyboardFocus();
        OsWindow? window = windows.FirstOrDefault(window => window.Id == id);
        if (window == null)
        {
            // Resolve ownership on first open, after the save's farmer is available.
            long farmerId = Game1.player.UniqueMultiplayerID;
            if (layouts == null)
            {
                if (!config.WindowLayouts.TryGetValue(farmerId, out layouts))
                    config.WindowLayouts[farmerId] = layouts = new();
            }
            window = create();
            if (layouts.TryGetValue(id, out WindowPlacement? placement))
                window.RestorePlacement(placement);
            if (!config.WindowHiddenColumns.TryGetValue(farmerId, out Dictionary<string, string[]>? columnLayouts))
                config.WindowHiddenColumns[farmerId] = columnLayouts = new();
            window.ConfigureColumnVisibility(columnLayouts.GetValueOrDefault(id) ?? Array.Empty<string>(), hiddenColumns =>
            {
                columnLayouts[id] = hiddenColumns;
                saveConfig();
            });
        }
        window.IsMinimized = false;
        windows.Remove(window);
        windows.Add(window);
        focusedWindow = window;
    }

    internal bool IsMinimized(string id) => windows.Any(window => window.Id == id && window.IsMinimized);

    internal void OpenLookup(object subject)
    {
        if (windows.FirstOrDefault(window => window.Id == LookupWindow.WindowId && !window.IsValid) is { } stale)
        {
            windows.Remove(stale);
            stale.Close();
        }
        Open(LookupWindow.WindowId, () => new LookupWindow(lookupAnything));
        ((LookupWindow)windows.Last()).ShowPage(subject);
    }

    internal ContextMenuItem[] GetMinimizedWindowItems() => windows.Where(window => window.IsValid && window.IsMinimized)
        .Select(window => new ContextMenuItem($"Restore {window.Title}", () =>
        {
            if (window.IsValid)
                Open(window.Id, () => window);
        })).ToArray();

    internal void CloseAll()
    {
        InvalidateLayout();
        foreach (OsWindow window in windows)
            window.Close();
        windows.Clear();
    }

    internal bool IsContextMenuOpen => contextMenu.IsOpen;

    // Focus, not visibility, owns the pause. Background windows can stay open while playing.
    internal bool HasDesktopFocus => isVisible() && !Game1.IsChatting
        && (contextMenu.IsOpen || HasPendingTransfer || draggedWindow != null || resizedWindow != null
            || contentDragWindow != null || itemDragSource != null
            || (focusedWindow is { IsValid: true, IsMinimized: false } focused && windows.Contains(focused)));

    internal void FocusWorld()
    {
        if (HasPendingTransfer || itemDragSource != null || draggedWindow != null || resizedWindow != null
            || contentDragWindow != null)
            return;
        focusedWindow = null;
        ClearKeyboardFocus();
    }

    internal void CloseContextMenu() => contextMenu.Close();

    internal bool ShowContextMenu(Point cursor, ContextMenuItem[] items, string? title = null, Func<bool>? isValid = null,
        ContextMenuInfo? info = null)
    {
        CancelPendingTransfer();
        EndDrag();
        ClearKeyboardFocus();
        return contextMenu.Open(cursor, ContextMenuItems.WithInfo(items, lookupAnything.Available ? info : null, OpenLookup),
            config, title, isValid);
    }

    internal bool ReceiveClick(Point cursor, bool leftClick, IInputHelper input)
    {
        if (!leftClick)
            EndDrag();
        if (CanReceiveInput && contextMenu.ReceiveClick(cursor, leftClick))
            return true;
        OsWindow? window = GetWindowAt(cursor);
        if (window == null)
            return CanReceiveInput && (HasPendingTransfer || itemDragSource != null || draggedWindow != null
                || resizedWindow != null || contentDragWindow != null);
        ClearKeyboardFocus();
        focusedWindow = window;

        if (leftClick)
        {
            EndDrag();
            windows.Remove(window);
            windows.Add(window);
            if (window.CloseBounds.Contains(cursor))
            {
                RememberPlacement(window);
                windows.Remove(window);
                window.Close();
                focusedWindow = windows.LastOrDefault(window => !window.IsMinimized);
                EndDrag();
            }
            else if (window.MinimizeBounds.Contains(cursor))
            {
                RememberPlacement(window);
                window.IsMinimized = true;
                focusedWindow = windows.LastOrDefault(window => !window.IsMinimized);
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
                if (window.KeyboardSubscriber?.Selected == true)
                {
                    keyboardWindow = window;
                    keyboardDispatcher = Game1.keyboardDispatcher;
                }
                contentDragWindow = window;
                if (window is InventoryWindow inventory && !HasPendingTransfer)
                {
                    bool control = IsHeld(input, SButton.LeftControl) || IsHeld(input, SButton.RightControl);
                    bool shift = IsHeld(input, SButton.LeftShift) || IsHeld(input, SButton.RightShift);
                    pressedItem = inventory.SelectRow(cursor, control, shift);
                    if (pressedItem != null)
                    {
                        itemDragSource = inventory;
                        draggedItems = inventory.SelectedItems;
                        itemDragStart = cursor;
                        collapseSelectionOnRelease = !control && !shift;
                    }
                }
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
        // Suppress the whole press (including held repeats) before native HUD/world dispatch.
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

    private bool CanReceiveInput => isVisible() && !Game1.IsChatting
        && drawnViewport == new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);

    private OsWindow? GetWindowAt(Point cursor)
    {
        if (!CanReceiveInput)
            return null;

        return drawnWindows.LastOrDefault(window => window.IsValid && !window.IsMinimized
            && windows.Contains(window) && window.Bounds.Contains(cursor));
    }

    internal WindowResizeEdges GetResizeCursorEdges(Point cursor)
    {
        if (!CanReceiveInput || contextMenu.IsOpen)
            return WindowResizeEdges.None;
        // Keep the gesture's direction when the pointer leaves the edge or hits a size limit.
        if (resizedWindow is { IsValid: true, IsMinimized: false } resizing && windows.Contains(resizing))
            return resizing.ActiveResizeEdges;
        if (draggedWindow != null || contentDragWindow != null || itemDragSource != null)
            return WindowResizeEdges.None;
        // Match click dispatch: covered windows must never supply a resize cursor.
        return GetWindowAt(cursor)?.GetResizeEdges(cursor) ?? WindowResizeEdges.None;
    }

    internal void Update(IInputHelper input)
    {
        foreach (OsWindow window in windows.ToArray())
            window.Update();
        foreach (OsWindow window in windows.Where(window => !window.IsValid).ToArray())
        {
            CancelPendingTransfer();
            EndDrag();
            RememberPlacement(window);
            windows.Remove(window);
            window.Close();
            if (ReferenceEquals(focusedWindow, window))
                focusedWindow = windows.LastOrDefault(window => !window.IsMinimized);
        }
        contextMenu.Update();
        if (!CanReceiveInput)
            contextMenu.Close();
        RefreshKeyboardFocus();
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
            EndDrag();
            return;
        }
        if (itemDragSource != null)
            itemDragActive |= Math.Abs(cursor.X - itemDragStart.X) >= 5 || Math.Abs(cursor.Y - itemDragStart.Y) >= 5;
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
        itemDragSource = null;
        draggedItems = Array.Empty<Item>();
        pressedItem = null;
        itemDragActive = false;
        collapseSelectionOnRelease = false;
    }

    private static bool IsHeld(IInputHelper input, SButton button) => input.IsDown(button) || input.IsSuppressed(button);

    private InventoryWindow? GetInventoryDropTarget(Point cursor)
    {
        if (itemDragSource == null || !itemDragSource.IsValid || HasPendingTransfer
            || !Context.IsPlayerFree || GetWindowAt(cursor) is not InventoryWindow target
            || ReferenceEquals(target, itemDragSource) || !target.ContainsDropPoint(cursor)
            || (itemDragSource.Chest == null) == (target.Chest == null))
            return null;
        Chest chest = (itemDragSource.Chest ?? target.Chest)!;
        return Utility.tileWithinRadiusOfPlayer((int)chest.TileLocation.X, (int)chest.TileLocation.Y, 1, Game1.player)
            ? target : null;
    }

    private void FinishItemDrag(Point cursor)
    {
        if (itemDragSource == null)
            return;
        itemDragActive |= Math.Abs(cursor.X - itemDragStart.X) >= 5 || Math.Abs(cursor.Y - itemDragStart.Y) >= 5;
        if (itemDragActive)
        {
            if (GetInventoryDropTarget(cursor) is { } target)
                TransferItems(itemDragSource, target, draggedItems);
        }
        else if (collapseSelectionOnRelease && pressedItem != null)
            itemDragSource.CollapseSelection(pressedItem);
    }

    private void CancelPendingTransfer()
    {
        transferVersion++;
        if (pendingTransfer is { } pending)
            pending.Source.TransferStatus = pending.Target.TransferStatus = "Transfer canceled.";
        // An invalid/removed chest may never tick its mutex again. Let the UI proceed;
        // any late acquisition still validates its version and releases without moving items.
        pendingTransfer = null;
    }

    private void TransferItems(InventoryWindow source, InventoryWindow target, Item[] requested)
    {
        Chest chest = (source.Chest ?? target.Chest)!;
        var mutex = chest.GetMutex();
        void SetStatus(string message) => source.TransferStatus = target.TransferStatus = message;
        // Never borrow/release a lock already owned by a vanilla menu or another operation.
        if (mutex.IsLocked())
        {
            SetStatus("Chest busy. Try again.");
            return;
        }
        Farmer farmer = Game1.player;
        GameLocation location = Game1.currentLocation;
        uint day = Game1.stats.DaysPlayed;
        Chest? sourceChest = source.Chest;
        Chest? targetChest = target.Chest;
        Point viewport = drawnViewport;
        int version = ++transferVersion;
        pendingTransfer = (version, source, target);
        void CompletePending()
        {
            if (pendingTransfer?.Version == version)
                pendingTransfer = null;
        }
        SetStatus("Transferring...");
        bool IsValid() => version == transferVersion && CanReceiveInput && Context.IsPlayerFree
            && Game1.player == farmer && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && viewport == drawnViewport && source.IsValid && target.IsValid
            && !source.IsMinimized && !target.IsMinimized && windows.Contains(source) && windows.Contains(target)
            && ReferenceEquals(source.Chest, sourceChest) && ReferenceEquals(target.Chest, targetChest)
            && Utility.tileWithinRadiusOfPlayer((int)chest.TileLocation.X, (int)chest.TileLocation.Y, 1, farmer);
        try
        {
            mutex.RequestLock(() =>
            {
                try
                {
                    if (!IsValid())
                    {
                        if (version == transferVersion)
                            SetStatus("Transfer canceled.");
                        return;
                    }
                    var sourceItems = sourceChest?.GetItemsForPlayer(farmer.UniqueMultiplayerID) ?? farmer.Items;
                    var targetItems = targetChest?.GetItemsForPlayer(farmer.UniqueMultiplayerID) ?? farmer.Items;
                    bool Accepts(Item item) => targetChest?.SpecialChestType switch
                    {
                        Chest.SpecialChestTypes.MiniShippingBin => Utility.highlightShippableObjects(item),
                        Chest.SpecialChestTypes.Enricher => StardewValley.Object.HighlightFertilizers(item),
                        _ => true
                    };
                    Item? Insert(Item item) => targetChest != null ? targetChest.addItem(item) : farmer.addItemToInventory(item);
                    void Remove(Item item)
                    {
                        if (sourceChest == null)
                            farmer.removeItemFromInventory(item);
                        else
                            sourceItems[InventoryTransferBatch.IndexOf(sourceItems, item)] = null!;
                    }
                    void Restored(Item item)
                    {
                        if (sourceChest == null && ReferenceEquals(farmer.CurrentItem, item))
                            item.actionWhenBeingHeld(farmer);
                    }
                    int moved = InventoryTransferBatch.Move(sourceItems, targetItems, requested, Accepts, Insert, Remove, Restored);
                    sourceChest?.clearNulls();
                    bool remaining = requested.Any(item => InventoryTransferBatch.IndexOf(sourceItems, item) >= 0);
                    SetStatus(remaining ? "Some stacks stayed: full or ineligible." : $"Moved {moved} stack{(moved == 1 ? "" : "s")}.");
                    if (moved > 0)
                        Game1.playSound("Ship");
                }
                catch (Exception ex)
                {
                    SetStatus("Transfer failed.");
                    onTransferFailed(ex);
                }
                finally
                {
                    // Release only the lock acquired for this request, including canceled requests.
                    mutex.ReleaseLock();
                    CompletePending();
                }
            }, () =>
            {
                CompletePending();
                if (version == transferVersion)
                    SetStatus("Chest busy. Try again.");
            });
        }
        catch (Exception ex)
        {
            CompletePending();
            SetStatus("Transfer failed.");
            onTransferFailed(ex);
        }
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
        focusedWindow = null;
        CancelPendingTransfer();
        contextMenu.Close();
        ClearKeyboardFocus();
        drawnWindows.Clear();
        EndDrag();
    }

    internal void Draw(SpriteBatch batch)
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
            const int margin = 8;
            int left = Pelicom.StripWidth + margin;
            Rectangle workArea = new(left, margin, Math.Max(0, drawnViewport.X - left - margin), Math.Max(0, drawnViewport.Y - margin * 2));
            float titleTextHeight = windows.Count == 0 ? Game1.smallFont.LineSpacing
                : windows.Max(window => Math.Max(Game1.smallFont.MeasureString(window.Title).Y,
                    Game1.smallFont.MeasureString(window.TitleSubtext).Y));
            int titleHeight = Math.Max(27, (int)MathF.Ceiling(titleTextHeight * config.FontScale)) + 6;
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            foreach (OsWindow window in windows)
            {
                if (window.IsValid && !window.IsMinimized && window.Layout(workArea, titleHeight, config))
                {
                    window.Draw(batch, config, cursor);
                    drawnWindows.Add(window);
                }
            }
            if (itemDragActive && itemDragSource != null)
            {
                InventoryWindow? dropTarget = GetInventoryDropTarget(cursor);
                dropTarget?.DrawDropTarget(batch, config);
                itemDragSource.DrawItemDrag(batch, cursor, draggedItems, dropTarget != null, config);
            }
            contextMenu.Draw(batch, config, cursor);
        }
        catch (Exception ex)
        {
            InvalidateLayout();
            onDrawFailed(ex);
        }
    }

    internal void ClearKeyboardFocus()
    {
        IKeyboardSubscriber? subscriber = keyboardWindow?.KeyboardSubscriber;
        if (subscriber != null)
        {
            if (keyboardDispatcher?.Subscriber == subscriber)
                keyboardDispatcher.Subscriber = null;
            subscriber.Selected = false;
        }
        keyboardWindow = null;
        keyboardDispatcher = null;
    }

    private bool RefreshKeyboardFocus()
    {
        if (keyboardWindow == null)
            return false;
        IKeyboardSubscriber? subscriber = keyboardWindow.KeyboardSubscriber;
        if (!isVisible() || Game1.IsChatting || keyboardWindow.IsMinimized || keyboardWindow.Bounds.IsEmpty
            || keyboardDispatcher != Game1.keyboardDispatcher
            || windows.LastOrDefault(window => !window.IsMinimized) != keyboardWindow
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

    internal bool ReceiveKeyboardButton(SButton button, IInputHelper input)
    {
        if (CanReceiveInput && !contextMenu.IsOpen && Game1.game1.HasKeyboardFocus()
            && button == SButton.A && (IsHeld(input, SButton.LeftControl) || IsHeld(input, SButton.RightControl))
            && focusedWindow is InventoryWindow inventory && inventory.IsValid && !inventory.IsMinimized
            && drawnWindows.Contains(inventory) && windows.LastOrDefault(window => !window.IsMinimized) == inventory)
        {
            EndDrag();
            inventory.SelectAll();
            return true;
        }
        if (CanReceiveInput && button == SButton.Escape && (itemDragSource != null || HasPendingTransfer))
        {
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
            && drawnWindows.Contains(focused) && windows.LastOrDefault(window => !window.IsMinimized) == focused
            && focused.ReceiveShortcut(button))
            return true;
        if (!RefreshKeyboardFocus() || !Game1.game1.HasKeyboardFocus() || !button.TryGetKeyboard(out Keys key))
            return false;
        if (key is Keys.Escape or Keys.Tab)
            ClearKeyboardFocus();
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
}
