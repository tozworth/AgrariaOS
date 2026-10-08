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

internal sealed partial class WindowManager
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















    private static bool IsHeld(IInputHelper input, SButton button) => input.IsDown(button) || input.IsSuppressed(button);


    // Titles onto each other: the release cursor must land in another frame's title strip.

























}
