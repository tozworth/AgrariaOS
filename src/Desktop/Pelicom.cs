using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop;

internal sealed class Pelicom : IClickableMenu
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
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (!IsInteractive || !drawnBounds.Contains(x, y))
            return;
        if (drawnSlots[OverviewEntryIndex].Contains(x, y))
            openOverview();
        else if (drawnSlots[MailEntryIndex].Contains(x, y))
            openMail();
        else if (drawnSlots[InventoryEntryIndex].Contains(x, y))
            openInventory();
        else if (drawnSlots[LivestockEntryIndex].Contains(x, y))
            openLivestock();
        else if (drawnSlots[SocialEntryIndex].Contains(x, y))
            openSocial();
        else if (drawnSlots[MiningEntryIndex].Contains(x, y))
            openMiningLedger();
        else if (drawnSlots[BundlesEntryIndex].Contains(x, y))
            openBundles();
        else if (drawnSlots[NotificationsEntryIndex].Contains(x, y))
            openNotifications();
        else if (drawnSlots[ChatEntryIndex].Contains(x, y))
            openChat();
        else if (drawnSlots[NotesEntryIndex].Contains(x, y))
            openNotes();
        else if (drawnSlots[ExitEntryIndex].Contains(x, y))
            // Terminate immediately without entering the game's save or shutdown flow.
            Environment.Exit(0);
    }
    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (isWithinBounds(x, y))
            showContextMenu(new Point(x, y), new[]
            {
                new ContextMenuItem("Auto-hide", () => autoHideChecked = !autoHideChecked, autoHideChecked),
                new ContextMenuItem("Position", Children: Enum.GetValues<PelicomEdge>().Select(edge =>
                    new ContextMenuItem(edge.ToString(), () => SetPosition(edge), GetPosition(config) == edge)).ToArray())
            });
    }

    private void SetPosition(PelicomEdge edge)
    {
        config.PelicomPositions[Game1.player.UniqueMultiplayerID] = edge;
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
        saveConfig();
    }

    internal static PelicomEdge GetPosition(ModConfig config)
        => config.PelicomPositions.GetValueOrDefault(Game1.player.UniqueMultiplayerID, PelicomEdge.Left);

    internal static Rectangle GetWorkArea(ModConfig config, int margin = 8)
    {
        PelicomEdge edge = GetPosition(config);
        int left = margin + (edge == PelicomEdge.Left ? StripWidth : 0);
        int top = margin + (edge == PelicomEdge.Top ? StripWidth : 0);
        int right = Game1.uiViewport.Width - margin - (edge == PelicomEdge.Right ? StripWidth : 0);
        int bottom = Game1.uiViewport.Height - margin - (edge == PelicomEdge.Bottom ? StripWidth : 0);
        return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
    public override bool isWithinBounds(int x, int y) => IsInteractive && drawnBounds.Contains(x, y);

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        // Drawing owns UI-space geometry; discard stale bounds until the next frame.
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
    }

    public override void draw(SpriteBatch b)
    {
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
        pendingTooltip = Rectangle.Empty;
        pendingTooltipLabel = "";
        if (!IsVisible)
            return;

        try
        {
            DrawStrip(b);
        }
        catch (Exception ex)
        {
            drawnBounds = Rectangle.Empty;
            pendingTooltip = Rectangle.Empty;
            pendingTooltipLabel = "";
            onDrawFailed(ex);
        }
    }

    // Replays the hover tip computed by the last draw above desktop windows.
    // WindowManager invokes this after its frames but before the context menu.
    internal void DrawTooltip(SpriteBatch batch)
    {
        if (pendingTooltip.IsEmpty || string.IsNullOrEmpty(pendingTooltipLabel))
            return;
        if (!IsInteractive)
            return;
        try
        {
            DrawTooltipRect(batch, pendingTooltip, pendingTooltipLabel);
        }
        catch (Exception ex)
        {
            pendingTooltip = Rectangle.Empty;
            pendingTooltipLabel = "";
            onDrawFailed(ex);
        }
    }

    private void DrawTooltipRect(SpriteBatch batch, Rectangle tooltip, string label)
    {
        batch.Draw(Game1.staminaRect, tooltip, UiStyle.Background * config.Opacity);
        UiText.Draw(batch, label, new Vector2(tooltip.X + 6, tooltip.Y + 2), Color.White, config);
    }

    private void DrawStrip(SpriteBatch batch)
    {
        PelicomEdge edge = GetPosition(config);
        bool horizontal = edge is PelicomEdge.Top or PelicomEdge.Bottom;
        Rectangle strip = horizontal
            ? new Rectangle(0, edge == PelicomEdge.Top ? 0 : Math.Max(0, Game1.uiViewport.Height - StripWidth), Game1.uiViewport.Width, StripWidth)
            : new Rectangle(edge == PelicomEdge.Left ? 0 : Math.Max(0, Game1.uiViewport.Width - StripWidth), 0, StripWidth, Game1.uiViewport.Height);
        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);
        int hovered = -1;
        int length = horizontal ? strip.Width : strip.Height;
        int endGroup = length - TopPadding - BottomEntryCount * SlotHeight;
        int separator = endGroup - TopPadding;
        string clock = horizontal ? StatusReadoutRenderer.FormatTime(Game1.timeOfDay, config.Use24HourClock) : "";
        Vector2 clockSize = Game1.smallFont.MeasureString(clock);
        int clockWidth = horizontal ? (int)MathF.Ceiling(clockSize.X) + TopPadding * 2 : 0;
        int startLimit = separator - TopPadding - clockWidth;

        // Reuse the game's PointClamp UI batch and sprites; never change shared render state.
        batch.Draw(Game1.staminaRect, strip, UiStyle.Background * config.Opacity);
        Rectangle border = edge switch
        {
            PelicomEdge.Left => new Rectangle(strip.Right - 1, strip.Y, 1, strip.Height),
            PelicomEdge.Right => new Rectangle(strip.X, strip.Y, 1, strip.Height),
            PelicomEdge.Top => new Rectangle(strip.X, strip.Bottom - 1, strip.Width, 1),
            _ => new Rectangle(strip.X, strip.Y, strip.Width, 1)
        };
        batch.Draw(Game1.staminaRect, border, UiStyle.Border * config.Opacity);
        if (separator >= 0)
            batch.Draw(Game1.staminaRect, horizontal
                ? new Rectangle(separator, strip.Y + 4, 1, StripWidth - 8)
                : new Rectangle(strip.X + 4, separator, StripWidth - 8, 1), UiStyle.Border * config.Opacity);
        if (horizontal && startLimit >= TopPadding && clockSize.Y + 1 <= strip.Height)
            UiText.Draw(batch, clock, new Vector2(separator - TopPadding - clockSize.X,
                strip.Y + (strip.Height - clockSize.Y) / 2), UiStyle.Primary, config, scale: 1f);
        bool showBundles;
        try { showBundles = isBundlesAvailable(); }
        catch { showBundles = false; }
        int topOffset = TopPadding;
        for (int i = 0; i < Entries.Length; i++)
        {
            if (i == BundlesEntryIndex && !showBundles)
                continue;
            bool isBottomEntry = i >= Entries.Length - BottomEntryCount;
            int offset = isBottomEntry ? endGroup + (i - (Entries.Length - BottomEntryCount)) * SlotHeight
                : topOffset;
            if (!isBottomEntry)
                topOffset += SlotHeight;
            Rectangle slot = horizontal ? new Rectangle(offset, strip.Y, SlotHeight, StripWidth - 1)
                : new Rectangle(strip.X, offset, StripWidth - 1, SlotHeight);
            // On unusually short viewports, keep the bottom group clear of the top entries.
            if (offset < 0 || offset + SlotHeight > length || (!isBottomEntry && offset + SlotHeight > startLimit))
                continue;
            drawnSlots[i] = slot;

            if (IsInteractive && slot.Contains(mouseX, mouseY))
            {
                hovered = i;
                batch.Draw(Game1.staminaRect, slot, UiStyle.Border * config.Opacity);
            }

            Point position = new(slot.X + (slot.Width - IconSize * IconScale) / 2,
                slot.Y + (slot.Height - IconSize * IconScale) / 2);
            UiStyle.DrawIcon(batch, Entries[i].Emoji, position, IconScale, config.Opacity);
            if ((i == OverviewEntryIndex && isOverviewMinimized()) || (i == MailEntryIndex && isMailMinimized()) || (i == InventoryEntryIndex && isInventoryMinimized())
                || (i == LivestockEntryIndex && isLivestockMinimized()) || (i == SocialEntryIndex && isSocialMinimized()) || (i == MiningEntryIndex && isMiningLedgerMinimized())
                || (i == BundlesEntryIndex && isBundlesMinimized())
                || (i == NotificationsEntryIndex && isNotificationsMinimized())
                || (i == ChatEntryIndex && isChatMinimized())
                || (i == NotesEntryIndex && isNotesMinimized()))
                batch.Draw(Game1.staminaRect, horizontal ? new Rectangle(slot.X + 8,
                    edge == PelicomEdge.Top ? slot.Bottom - 2 : slot.Y + 1, SlotHeight - 16, 1)
                    : new Rectangle(edge == PelicomEdge.Left ? slot.X + 1 : slot.Right - 2, slot.Y + 8, 1, SlotHeight - 16),
                    UiStyle.Muted * config.Opacity);
            if (i == NotificationsEntryIndex && unreadNotifications() > 0)
            {
                int count = Math.Min(99, unreadNotifications());
                int width = Utility.getWidthOfTinyDigitString(count, 1f) + 4;
                Rectangle badge = new(slot.Right - width - 1, slot.Y + 1, width, 11);
                batch.Draw(Game1.staminaRect, badge, new Color(168, 55, 55) * config.Opacity);
                Utility.drawTinyDigits(count, batch, new Vector2(badge.X + 2, badge.Y + 2), 1f, 0.92f, Color.White * config.Opacity);
            }
        }

        if (hovered >= 0)
        {
            string label = Entries[hovered].Label;
            if (hovered == NotificationsEntryIndex && unreadNotifications() > 0)
                label += $" ({unreadNotifications()} unread)";
            Vector2 size = Game1.smallFont.MeasureString(label) * config.FontScale;
            Rectangle hoveredSlot = drawnSlots[hovered];
            int width = Math.Min(Game1.uiViewport.Width, (int)MathF.Ceiling(size.X) + 12);
            int height = Math.Min(Game1.uiViewport.Height, (int)MathF.Ceiling(size.Y) + 4);
            int x = edge == PelicomEdge.Right ? strip.Left - width - 4
                : horizontal ? hoveredSlot.X : strip.Right + 4;
            int y = edge == PelicomEdge.Top ? strip.Bottom + 4
                : edge == PelicomEdge.Bottom ? strip.Top - height - 4
                : hoveredSlot.Y + (SlotHeight - height) / 2;
            Rectangle tooltip = new(Math.Clamp(x, 0, Math.Max(0, Game1.uiViewport.Width - width)),
                Math.Clamp(y, 0, Math.Max(0, Game1.uiViewport.Height - height)), width, height);
            pendingTooltip = tooltip;
            pendingTooltipLabel = label;
            DrawTooltipRect(batch, tooltip, label);
        }

        drawnBounds = strip;
    }
}
