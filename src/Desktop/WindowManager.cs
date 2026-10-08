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
    // Grouped windows share one frame; a group's z-order is its frontmost member.
    private readonly List<OsWindow> windows = new();
    private readonly List<WindowTabGroup> tabGroups = new();
    private bool suppressOpenSave;
    private static readonly HashSet<string> PersistentWindowIds = new(StringComparer.Ordinal)
    {
        OverviewWindow.WindowId,
        MailWindow.WindowId,
        InventoryWindow.WindowId,
        LivestockWindow.WindowId,
        SocialWindow.WindowId,
        MiningLedgerWindow.WindowId,
        BundlesWindow.WindowId,
        NotificationsWindow.WindowId,
        ChatWindow.WindowId,
        NotesWindow.WindowId,
    };
    private readonly List<OsWindow> drawnWindows = new();
    private readonly ContextMenu contextMenu = new();
    private Point drawnViewport;
    private OsWindow? draggedWindow;
    private WindowTabGroup? draggedGroup;
    private OsWindow? resizedWindow;
    private WindowTabGroup? resizedGroup;
    private OsWindow? contentDragWindow;
    private Point dragOffset;
    private Point resizeStartCursor;
    private Rectangle resizeStartBounds;
    private OsWindow? keyboardWindow;
    private OsWindow? focusedWindow;
    private OsWindow? itemDragSource;
    private ISalable[] draggedItems = Array.Empty<ISalable>();
    private ISalable? pressedItem;
    private Point itemDragStart;
    private bool itemDragActive;
    private bool collapseSelectionOnRelease;
    private readonly Queue<ISalable> pendingBuys = new();
    private (int Version, InventoryWindow Source, InventoryWindow Target)? pendingTransfer;
    private bool HasPendingTransfer => pendingTransfer.HasValue;
    private int transferVersion;
    private readonly HashSet<Item> cutItems = new(ReferenceEqualityComparer.Instance);
    private InventoryWindow? cutSource;
    private Chest? cutSourceChest;
    private bool HasCut => cutSource != null && cutItems.Count > 0;
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

    internal void OpenOverview() => Open(OverviewWindow.WindowId, () => new OverviewWindow(config, saveConfig, lookupAnything, getMovement, FocusWorld));
    internal void OpenMail() => Open(MailWindow.WindowId, () => new MailWindow());
    internal void OpenInventory()
    {
        Open(InventoryWindow.WindowId, () => new InventoryWindow(config, lookupAnything: lookupAnything, getTradeShop: GetTradeShop));
        if (windows.FirstOrDefault(window => window.Id == InventoryWindow.WindowId) is InventoryWindow inventory)
            AssignCutPredicate(inventory);
    }
    internal void OpenLivestock() => Open(LivestockWindow.WindowId, () => new LivestockWindow());
    internal void OpenSocial() => Open(SocialWindow.WindowId, () => new SocialWindow(lookupAnything, OpenLookup));
    internal void OpenMiningLedger(MiningLedger ledger) => Open(MiningLedgerWindow.WindowId, () => new MiningLedgerWindow(ledger));
    internal void OpenBundles() => Open(BundlesWindow.WindowId, () => new BundlesWindow(lookupAnything));
    internal void OpenChestInventory(Chest chest)
    {
        Open(InventoryWindow.ChestWindowId, () => new InventoryWindow(config, chest, lookupAnything, GetTradeShop));
        if (windows.FirstOrDefault(window => window.Id == InventoryWindow.ChestWindowId) is InventoryWindow chestWindow)
        {
            AssignCutPredicate(chestWindow);
            chestWindow.ShowChest(chest);
            // Switching chests cancels a chest-sourced cut; a backpack cut survives.
            if (cutSource != null && ReferenceEquals(cutSource, chestWindow)
                && !ReferenceEquals(cutSourceChest, chestWindow.Chest))
                ClearCut();
        }
    }
    internal void OpenTrade(ShopMenu shop)
    {
        Open(TradeWindow.WindowId, () => new TradeWindow(config, shop, lookupAnything, EnqueueBuys));
        if (windows.FirstOrDefault(window => window.Id == TradeWindow.WindowId) is TradeWindow tradeWindow)
            tradeWindow.ShowShop(shop);
    }
    internal TradeWindow? GetTradeWindow()
        => windows.FirstOrDefault(window => window.Id == TradeWindow.WindowId) as TradeWindow;
    internal ShopMenu? GetTradeShop()
    {
        TradeWindow? trade = GetTradeWindow();
        return trade is { IsValid: true } ? trade.Shop : null;
    }
    internal void CloseTrade()
    {
        if (GetTradeWindow() is { } trade)
            CloseWindow(trade);
        pendingBuys.Clear();
    }
    internal void OpenNotifications(NotificationHistory history) => Open(NotificationsWindow.WindowId, () => new NotificationsWindow(history));
    internal void OpenNotes() => Open(NotesWindow.WindowId,
        () => new NotesWindow(config, saveConfig, (cursor, items) => ShowContextMenu(cursor, items)));
    internal void OpenChat(ChatSession session, bool showLocal = false)
    {
        Open(ChatWindow.WindowId, () => new ChatWindow(session, config, saveConfig));
        if (showLocal && windows.FirstOrDefault(window => window.Id == ChatWindow.WindowId) is ChatWindow chatWindow)
            chatWindow.SelectTab("Local");
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
            if (!config.WindowColumnWidths.TryGetValue(farmerId, out Dictionary<string, Dictionary<string, int>>? columnWidths))
                config.WindowColumnWidths[farmerId] = columnWidths = new();
            window.ConfigureColumnWidths(columnWidths.GetValueOrDefault(id) ?? new Dictionary<string, int>(), widths =>
            {
                columnWidths[id] = widths;
                saveConfig();
            });
        }
        WindowTabGroup? group = FindGroup(window);
        if (group != null)
        {
            // Reopening a tabbed window restores the whole frame and selects the tab.
            foreach (OsWindow member in group.Tabs)
                member.IsMinimized = false;
            group.SetActive(window);
            BringGroupToFront(group);
        }
        else
        {
            window.IsMinimized = false;
            windows.Remove(window);
            windows.Add(window);
        }
        focusedWindow = window;
        RememberOpenWindows();
        RememberTabGroups();
    }

    internal bool IsMinimized(string id) => windows.Any(window => window.Id == id && window.IsMinimized);

    internal void RestoreSavedWindows(NotificationHistory notifications, ChatSession chat, MiningLedger ledger)
    {
        if (!Context.IsWorldReady)
            return;
        long farmerId;
        try { farmerId = Game1.player.UniqueMultiplayerID; }
        catch { return; }
        if (!config.OpenWindows.TryGetValue(farmerId, out List<WindowOpenState>? saved) || saved == null || saved.Count == 0)
            return;
        suppressOpenSave = true;
        try
        {
            foreach (WindowOpenState state in saved.ToArray())
            {
                if (state == null || string.IsNullOrWhiteSpace(state.Id) || !PersistentWindowIds.Contains(state.Id))
                    continue;
                try
                {
                    switch (state.Id)
                    {
                        case OverviewWindow.WindowId: OpenOverview(); break;
                        case MailWindow.WindowId: OpenMail(); break;
                        case InventoryWindow.WindowId: OpenInventory(); break;
                        case LivestockWindow.WindowId: OpenLivestock(); break;
                        case SocialWindow.WindowId: OpenSocial(); break;
                        case MiningLedgerWindow.WindowId: OpenMiningLedger(ledger); break;
                        case BundlesWindow.WindowId: OpenBundles(); break;
                        case NotificationsWindow.WindowId: OpenNotifications(notifications); break;
                        case ChatWindow.WindowId: OpenChat(chat); break;
                        case NotesWindow.WindowId: OpenNotes(); break;
                        default: continue;
                    }
                    if (state.IsMinimized && windows.FirstOrDefault(window => window.Id == state.Id) is { } opened)
                        opened.IsMinimized = true;
                }
                catch { }
            }
            RestoreTabGroups(farmerId);
            FocusWorld();
        }
        finally
        {
            suppressOpenSave = false;
        }
        RememberOpenWindows();
        RememberTabGroups();
    }

    private void RememberOpenWindows()
    {
        if (suppressOpenSave || !Context.IsWorldReady)
            return;
        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            List<WindowOpenState> current = windows
                .Where(window => window.IsValid && PersistentWindowIds.Contains(window.Id))
                .Select(window => new WindowOpenState { Id = window.Id, IsMinimized = window.IsMinimized })
                .ToList();
            if (config.OpenWindows.TryGetValue(farmerId, out List<WindowOpenState>? saved) && saved != null
                && saved.Count == current.Count && saved.Zip(current).All(pair =>
                    pair.First.Id == pair.Second.Id && pair.First.IsMinimized == pair.Second.IsMinimized))
                return;
            config.OpenWindows[farmerId] = current;
            saveConfig();
        }
        catch { }
    }

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

    private void RememberTabGroups()
    {
        if (suppressOpenSave || !Context.IsWorldReady)
            return;
        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            var current = new List<TabGroupState>();
            foreach (WindowTabGroup group in tabGroups)
            {
                if (group.Tabs.Count < 2)
                    continue;
                if (group.Tabs.Any(member => !member.IsValid || !windows.Contains(member)))
                    continue;
                current.Add(new TabGroupState
                {
                    WindowIds = group.Tabs.Select(member => member.Id).ToList(),
                    ActiveId = group.Active.Id
                });
            }
            if (config.WindowTabGroups.TryGetValue(farmerId, out List<TabGroupState>? saved) && saved != null
                && saved.Count == current.Count && saved.Zip(current).All(pair =>
                    string.Equals(pair.First.ActiveId, pair.Second.ActiveId, StringComparison.Ordinal)
                    && pair.First.WindowIds.Count == pair.Second.WindowIds.Count
                    && pair.First.WindowIds.Zip(pair.Second.WindowIds).All(ids => string.Equals(ids.First, ids.Second, StringComparison.Ordinal))))
                return;
            config.WindowTabGroups[farmerId] = current;
            saveConfig();
        }
        catch { }
    }

    private void RestoreTabGroups(long farmerId)
    {
        if (!config.WindowTabGroups.TryGetValue(farmerId, out List<TabGroupState>? saved) || saved == null)
            return;
        tabGroups.Clear();
        foreach (TabGroupState state in saved.ToArray())
        {
            try
            {
                if (state?.WindowIds == null || state.WindowIds.Count < 2)
                    continue;
                var members = new List<OsWindow>();
                foreach (string id in state.WindowIds)
                {
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    OsWindow? window = windows.FirstOrDefault(candidate => candidate.Id == id && !members.Contains(candidate)
                        && FindGroup(candidate) == null && candidate.IsValid);
                    if (window != null)
                        members.Add(window);
                }
                if (members.Count < 2)
                    continue;
                OsWindow active = members.FirstOrDefault(member => string.Equals(member.Id, state.ActiveId, StringComparison.Ordinal)) ?? members[0];
                WindowPlacement? placement = members.Select(member => member.GetPlacement()).FirstOrDefault(placement => placement != null)
                    ?? active.GetPlacement();
                var group = new WindowTabGroup(members, active, placement ?? new WindowPlacement { X = 0, Y = 0, Width = 420, Height = 260 });
                // Members already carry the saved shared placement; the next draw syncs bounds.
                tabGroups.Add(group);
                group.SetActive(active);
                bool minimized = members.All(member => member.IsMinimized);
                if (!minimized && members.Any(member => member.IsMinimized))
                {
                    // A partially minimized restore (e.g. from an older config) heals to visible.
                    foreach (OsWindow member in members)
                        member.IsMinimized = false;
                }
            }
            catch { }
        }
    }

    private void DissolveGroupIfSingle(WindowTabGroup group)
    {
        if (group.Tabs.Count < 2)
            tabGroups.Remove(group);
    }

    private void RememberGroupPlacement(WindowTabGroup group)
    {
        foreach (OsWindow member in group.Tabs)
            RememberPlacement(member);
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

    internal void OpenLookup(object subject)
    {
        if (windows.FirstOrDefault(window => window.Id == LookupWindow.WindowId && !window.IsValid) is { } stale)
            CloseWindow(stale);
        Open(LookupWindow.WindowId, () => new LookupWindow(lookupAnything));
        if (windows.FirstOrDefault(window => window.Id == LookupWindow.WindowId) is LookupWindow lookup)
            lookup.ShowPage(subject);
    }

    internal void CloseAll()
    {
        InvalidateLayout();
        foreach (OsWindow window in windows)
            window.Close();
        windows.Clear();
        tabGroups.Clear();
        ClearCut();
    }

    internal bool IsContextMenuOpen => contextMenu.IsOpen;

    internal bool IsOverWindow(Point cursor) => GetWindowAt(cursor) != null;

    internal bool IsTextFieldHovered(Point cursor)
    {
        try { return GetWindowAt(cursor) is { } window && window.IsTextFieldAt(cursor); }
        catch { return false; }
    }

    // Focus, not visibility, owns the pause. Background windows can stay open while playing.
    internal bool HasDesktopFocus => isVisible() && !Game1.IsChatting
        && (contextMenu.IsOpen || HasPendingTransfer || draggedWindow != null || draggedGroup != null
            || resizedWindow != null || resizedGroup != null
            || contentDragWindow != null || itemDragSource != null
            || (focusedWindow is { IsValid: true, IsMinimized: false } focused && windows.Contains(focused)));

    internal void FocusWorld()
    {
        if (HasPendingTransfer || itemDragSource != null || draggedWindow != null || draggedGroup != null
            || resizedWindow != null || resizedGroup != null || contentDragWindow != null)
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
                RememberPlacement(window);
                window.IsMinimized = true;
                focusedWindow = GetFrontmostActive();
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
        // The press identifies the frame; tab selection decides the active member.
        OsWindow? pressedTab = group.GetTabAt(cursor) ?? (group.TitleBounds.Contains(cursor) ? group.Active : null);
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
            RememberGroupPlacement(group);
            foreach (OsWindow member in group.Tabs)
                member.IsMinimized = true;
            focusedWindow = GetFrontmostActive();
            EndDrag();
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
            if (active.KeyboardSubscriber?.Selected == true)
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

    private static bool IsHeld(IInputHelper input, SButton button) => input.IsDown(button) || input.IsSuppressed(button);

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

    // Titles onto each other: the release cursor must land in another frame's title strip.
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

    private void CancelPendingTransfer()
    {
        transferVersion++;
        if (pendingTransfer is { } pending)
            pending.Source.TransferStatus = pending.Target.TransferStatus = "Transfer canceled.";
        // An invalid/removed chest may never tick its mutex again. Let the UI proceed;
        // any late acquisition still validates its version and releases without moving items.
        pendingTransfer = null;
    }

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

    private void MergeStacks(InventoryWindow source, Item target, Item[] requested)
    {
        Farmer farmer = Game1.player;
        if (source.Chest == null)
        {
            if (!source.IsValid || !Context.IsPlayerFree)
                return;
            var items = farmer.Items;
            if (InventoryTransferBatch.IndexOf(items, target) < 0)
            {
                source.TransferStatus = "That stack is no longer available.";
                return;
            }
            try
            {
                var result = InventoryStackActions.Merge(items, target, requested, farmer.removeItemFromInventory);
                if (result.MovedUnits <= 0)
                {
                    source.TransferStatus = target.Stack >= target.maximumStackSize()
                        ? "That stack is full." : "Those stacks cannot be combined.";
                    return;
                }
                bool remaining = requested.Any(item => !ReferenceEquals(item, target)
                    && InventoryTransferBatch.IndexOf(items, item) >= 0);
                source.TransferStatus = result.FullyMerged > 0
                    ? $"Merged {result.FullyMerged} stack{(result.FullyMerged == 1 ? "" : "s")}."
                        + (remaining ? " Target stack is full." : "")
                    : $"Merged {result.MovedUnits} items.";
                source.CollapseSelection(target);
                Game1.playSound("Ship");
            }
            catch (Exception ex)
            {
                source.TransferStatus = "Merge failed.";
                onTransferFailed(ex);
            }
            return;
        }

        Chest chest = source.Chest;
        var mutex = chest.GetMutex();
        if (mutex.IsLocked())
        {
            source.TransferStatus = "Chest busy. Try again.";
            return;
        }
        GameLocation location = Game1.currentLocation;
        uint day = Game1.stats.DaysPlayed;
        Point viewport = drawnViewport;
        int version = ++transferVersion;
        pendingTransfer = (version, source, source);
        void CompletePending()
        {
            if (pendingTransfer?.Version == version)
                pendingTransfer = null;
        }
        source.TransferStatus = "Merging...";
        bool IsValid() => version == transferVersion && CanReceiveInput && Context.IsPlayerFree
            && Game1.player == farmer && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && viewport == drawnViewport && source.IsValid && !source.IsMinimized && windows.Contains(source)
            && ReferenceEquals(source.Chest, chest)
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
                            source.TransferStatus = "Transfer canceled.";
                        return;
                    }
                    var chestItems = chest.GetItemsForPlayer(farmer.UniqueMultiplayerID);
                    if (InventoryTransferBatch.IndexOf(chestItems, target) < 0)
                    {
                        source.TransferStatus = "That stack is no longer available.";
                        return;
                    }
                    void Remove(Item item) => chestItems[InventoryTransferBatch.IndexOf(chestItems, item)] = null!;
                    var result = InventoryStackActions.Merge(chestItems, target, requested, Remove);
                    chest.clearNulls();
                    if (result.MovedUnits <= 0)
                    {
                        source.TransferStatus = target.Stack >= target.maximumStackSize()
                            ? "That stack is full." : "Those stacks cannot be combined.";
                        return;
                    }
                    bool remaining = requested.Any(item => !ReferenceEquals(item, target)
                        && InventoryTransferBatch.IndexOf(chestItems, item) >= 0);
                    source.TransferStatus = result.FullyMerged > 0
                        ? $"Merged {result.FullyMerged} stack{(result.FullyMerged == 1 ? "" : "s")}."
                            + (remaining ? " Target stack is full." : "")
                        : $"Merged {result.MovedUnits} items.";
                    source.CollapseSelection(target);
                    Game1.playSound("Ship");
                }
                catch (Exception ex)
                {
                    source.TransferStatus = "Merge failed.";
                    onTransferFailed(ex);
                }
                finally
                {
                    mutex.ReleaseLock();
                    CompletePending();
                }
            }, () =>
            {
                CompletePending();
                if (version == transferVersion)
                    source.TransferStatus = "Chest busy. Try again.";
            });
        }
        catch (Exception ex)
        {
            CompletePending();
            source.TransferStatus = "Merge failed.";
            onTransferFailed(ex);
        }
    }

    private void SellDraggedItems(InventoryWindow source, TradeWindow target, Item[] requested)
    {
        ShopMenu? shop = target.Shop;
        if (shop == null || !source.IsValid || !target.IsValid || !Context.IsPlayerFree)
            return;
        Farmer farmer = Game1.player;
        var live = requested.Where(item => InventoryTransferBatch.IndexOf(farmer.Items, item) >= 0).ToArray();
        if (live.Length == 0)
        {
            source.TransferStatus = target.TransferStatus = "That stack is no longer available.";
            return;
        }
        foreach (Item item in live)
        {
            if (!TradeActions.CanSell(shop, item))
            {
                source.TransferStatus = target.TransferStatus = $"The shop won't buy {item.DisplayName}.";
                return;
            }
        }
        string? lastStatus = null;
        foreach (Item item in live)
            lastStatus = TradeActions.Sell(shop, item).Status;
        source.TransferStatus = target.TransferStatus = live.Length == 1 ? lastStatus : $"Sold {live.Length} stacks.";
    }

    internal void EnqueueBuys(ISalable[] requested)
    {
        TradeWindow? trade = GetTradeWindow();
        ShopMenu? shop = trade?.Shop;
        if (trade == null || shop == null || !trade.IsValid || requested.Length == 0)
            return;
        if (!Context.IsPlayerFree && Game1.activeClickableMenu is not NumberEntryMenu)
            return;
        foreach (ISalable salable in requested)
        {
            if (!shop.forSale.Any(current => ReferenceEquals(current, salable)))
                continue;
            bool stackable;
            try { stackable = salable.maximumStackSize() > 1; }
            catch { stackable = false; }
            if (!stackable)
            {
                var result = TradeActions.Buy(shop, salable, 1);
                trade.TransferStatus = result.Error ?? result.Status;
                if (result.ShouldClose)
                {
                    pendingBuys.Clear();
                    CloseTrade();
                    return;
                }
            }
            else if (!pendingBuys.Any(queued => ReferenceEquals(queued, salable)))
                pendingBuys.Enqueue(salable);
        }
        ProcessBuyQueue();
    }

    private void ProcessBuyQueue()
    {
        if (Game1.activeClickableMenu != null || pendingBuys.Count == 0)
            return;
        if (!Context.IsWorldReady)
        {
            pendingBuys.Clear();
            return;
        }
        TradeWindow? trade = GetTradeWindow();
        ShopMenu? shop = trade?.Shop;
        if (trade == null || shop == null || !trade.IsValid || !windows.Contains(trade))
        {
            pendingBuys.Clear();
            return;
        }
        ISalable salable = pendingBuys.Dequeue();
        if (!shop.forSale.Any(current => ReferenceEquals(current, salable)))
        {
            trade.TransferStatus = "That item is no longer for sale.";
            ProcessBuyQueue();
            return;
        }
        Farmer farmer = Game1.player;
        int max = TradeActions.GetMaxBuyable(shop, farmer, salable);
        if (max <= 0)
        {
            trade.TransferStatus = TradeActions.GetCurrencyAmount(farmer, shop.currency) < (shop.itemPriceAndStock.TryGetValue(salable, out var stock) ? stock.Price : 0)
                ? "Not enough funds." : "Out of stock.";
            ProcessBuyQueue();
            return;
        }
        string unitPrice = TradeActions.PriceText(shop, salable);
        string suffix = TradeActions.CurrencySuffix(shop.currency);
        string title = $"Buy {salable.DisplayName}";
        string PricePrompt()
        {
            int liveBalance = TradeActions.GetCurrencyAmount(Game1.player, shop.currency);
            int liveMax = TradeActions.GetMaxBuyable(shop, Game1.player, salable);
            return $"{unitPrice} each (You have {liveBalance} {suffix}) - Qty 1-{liveMax}".Trim();
        }
        TradeWindow capturedTrade = trade;
        ShopMenu capturedShop = shop;
        ISalable capturedItem = salable;
        bool IsCurrent() => Context.IsWorldReady && capturedTrade.IsValid && !capturedTrade.IsMinimized
            && windows.Contains(capturedTrade) && ReferenceEquals(capturedTrade.Shop, capturedShop)
            && capturedShop.forSale.Any(current => ReferenceEquals(current, capturedItem));
        Game1.activeClickableMenu = new NumberEntryMenu(config, title, PricePrompt, amount =>
        {
            if (!IsCurrent())
                return "Shop closed.";
            int currentMax = TradeActions.GetMaxBuyable(capturedShop, Game1.player, capturedItem);
            if (amount < 1 || amount > currentMax)
                return $"Enter a number from 1 to {Math.Max(1, currentMax)}.";
            var result = TradeActions.Buy(capturedShop, capturedItem, amount);
            if (result.Error != null)
                return result.Error;
            capturedTrade.TransferStatus = result.Status;
            if (result.ShouldClose)
            {
                pendingBuys.Clear();
                CloseTrade();
            }
            return null;
        }, IsCurrent, confirmLabel: "Buy");
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
        foreach (WindowTabGroup group in tabGroups)
            group.ClearDrawnTabs();
        EndDrag();
    }

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
