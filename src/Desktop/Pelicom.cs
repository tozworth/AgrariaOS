using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop;

internal sealed partial class Pelicom : IClickableMenu
{
    internal const int StripWidth = 35;
    private const int SlotHeight = 35;
    private const int TopPadding = 8;
    private const int BottomEntryCount = 2;
    private const int IconSize = 9;
    private const int IconScale = 3;
    private const int OverviewEntryIndex = 0;
    private const int BundlesEntryIndex = 5;
    private const int MailEntryIndex = 7;
    private const int InventoryEntryIndex = 1;
    private const int LivestockEntryIndex = 2;
    private const int SocialEntryIndex = 3;
    private const int MiningEntryIndex = 4;
    private const int ChatEntryIndex = 8;
    private const int NotesEntryIndex = 9;
    private const int NotificationsEntryIndex = 10;
    private const int ExitEntryIndex = 11;
    private static readonly (int Emoji, string Label)[] Entries =
    {
        (107, "Overview"),
        (UiStyle.InventoryIcon, "Inventory"),
        (74, "Livestock"),
        (UiStyle.SocialIcon, "Social"),
        (121, "Mining Ledger"),
        (84, "Bundles"),
        (41, "Wallet"),
        (UiStyle.MailIcon, "Mail"),
        (UiStyle.ChatIcon, "Chat"),
        (139, "Notes"),
        (UiStyle.NotificationIcon, "Notifications"),
        (131, "Exit")
    };

    private readonly ModConfig config;
    private readonly Func<bool> isEnabled;
    private readonly Action<Exception> onDrawFailed;
    private readonly Action openMail;
    private readonly Func<bool> isMailMinimized;
    private readonly Action openInventory;
    private readonly Func<bool> isInventoryMinimized;
    private readonly Action openLivestock;
    private readonly Func<bool> isLivestockMinimized;
    private readonly Action openSocial;
    private readonly Func<bool> isSocialMinimized;
    private readonly Action openNotifications;
    private readonly Func<bool> isNotificationsMinimized;
    private readonly Func<int> unreadNotifications;
    private readonly Action openChat;
    private readonly Func<bool> isChatMinimized;
    private readonly Action openNotes;
    private readonly Func<bool> isNotesMinimized;
    private readonly Action<Point, ContextMenuItem[]> showContextMenu;
    private readonly Action openMiningLedger;
    private readonly Func<bool> isMiningLedgerMinimized;
    private readonly Action openBundles;
    private readonly Func<bool> isBundlesMinimized;
    private readonly Func<bool> isBundlesAvailable;
    private readonly Action openOverview;
    private readonly Func<bool> isOverviewMinimized;
    private bool autoHideChecked;
    private readonly Action saveConfig;
    private readonly Rectangle[] drawnSlots = new Rectangle[Entries.Length];
    private Rectangle drawnBounds;
    // The strip draws inside Game1.drawHUD via onScreenMenus, so its hover tip would sit
    // below desktop windows drawn later in RenderedHud. Remember the tip and replay it
    // above windows through DrawTooltip.
    private Rectangle pendingTooltip;
    private string pendingTooltipLabel = "";

    public Pelicom(ModConfig config, Func<bool> isEnabled, Action<Exception> onDrawFailed,
        Action openMail, Func<bool> isMailMinimized, Action openInventory, Func<bool> isInventoryMinimized,
        Action openLivestock, Func<bool> isLivestockMinimized,
        Action openSocial, Func<bool> isSocialMinimized,
        Action openNotifications, Func<bool> isNotificationsMinimized, Func<int> unreadNotifications,
        Action openChat, Func<bool> isChatMinimized,
        Action openNotes, Func<bool> isNotesMinimized, Action<Point, ContextMenuItem[]> showContextMenu,
        Action openMiningLedger, Func<bool> isMiningLedgerMinimized,
        Action openBundles, Func<bool> isBundlesMinimized, Func<bool> isBundlesAvailable,
        Action openOverview, Func<bool> isOverviewMinimized, Action saveConfig)
    {
        this.config = config;
        this.isEnabled = isEnabled;
        this.onDrawFailed = onDrawFailed;
        this.openMail = openMail;
        this.isMailMinimized = isMailMinimized;
        this.openInventory = openInventory;
        this.isInventoryMinimized = isInventoryMinimized;
        this.openLivestock = openLivestock;
        this.isLivestockMinimized = isLivestockMinimized;
        this.openSocial = openSocial;
        this.isSocialMinimized = isSocialMinimized;
        this.openNotifications = openNotifications;
        this.isNotificationsMinimized = isNotificationsMinimized;
        this.unreadNotifications = unreadNotifications;
        this.openChat = openChat;
        this.isChatMinimized = isChatMinimized;
        this.openNotes = openNotes;
        this.isNotesMinimized = isNotesMinimized;
        this.showContextMenu = showContextMenu;
        this.openMiningLedger = openMiningLedger;
        this.isMiningLedgerMinimized = isMiningLedgerMinimized;
        this.openBundles = openBundles;
        this.isBundlesMinimized = isBundlesMinimized;
        this.isBundlesAvailable = isBundlesAvailable;
        this.openOverview = openOverview;
        this.isOverviewMinimized = isOverviewMinimized;
        this.saveConfig = saveConfig;
    }

    internal bool IsVisible => isEnabled() && Game1.IsHudDrawn && !Game1.eventUp && Game1.farmEvent == null
        && (Game1.activeClickableMenu == null || Game1.activeClickableMenu is GameMenu) && Game1.currentMinigame == null;

    // Keep the rail behind the tabbed menu without intercepting its input.
    internal bool IsInteractive => IsVisible && Game1.activeClickableMenu == null;

    // Native HUD dispatch consumes clicks inside this rail before they reach world/tool actions.
    // Overview, Inventory, Livestock, Social, Mining Ledger, Bundles, Mail, Chat, Notes and Notifications open desktop windows; Exit terminates the game.






    // Replays the hover tip computed by the last draw above desktop windows.
    // WindowManager invokes this after its frames but before the context menu.


}
