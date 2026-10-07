using AgrariaOS.Configuration;
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
    private const int MailEntryIndex = 5;
    private const int InventoryEntryIndex = 1;
    private const int LivestockEntryIndex = 2;
    private const int MiningEntryIndex = 3;
    private const int ChatEntryIndex = 6;
    private const int NotificationsEntryIndex = 7;
    private const int ExitEntryIndex = 8;
    private static readonly (int Emoji, string Label)[] Entries =
    {
        (107, "Dashboard"),
        (UiStyle.InventoryIcon, "Inventory"),
        (74, "Livestock"),
        (121, "Mining Ledger"),
        (41, "Wallet"),
        (UiStyle.MailIcon, "Mail"),
        (UiStyle.ChatIcon, "Chat"),
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
    private readonly Action openNotifications;
    private readonly Func<bool> isNotificationsMinimized;
    private readonly Func<int> unreadNotifications;
    private readonly Action openChat;
    private readonly Func<bool> isChatMinimized;
    private readonly Action<Point, ContextMenuItem[]> showContextMenu;
    private readonly Func<ContextMenuItem[]> minimizedWindows;
    private readonly Action openMiningLedger;
    private readonly Func<bool> isMiningLedgerMinimized;
    private bool autoHideChecked;
    private readonly Rectangle[] drawnSlots = new Rectangle[Entries.Length];
    private Rectangle drawnBounds;

    public Pelicom(ModConfig config, Func<bool> isEnabled, Action<Exception> onDrawFailed,
        Action openMail, Func<bool> isMailMinimized, Action openInventory, Func<bool> isInventoryMinimized,
        Action openLivestock, Func<bool> isLivestockMinimized,
        Action openNotifications, Func<bool> isNotificationsMinimized, Func<int> unreadNotifications,
        Action openChat, Func<bool> isChatMinimized, Action<Point, ContextMenuItem[]> showContextMenu,
        Func<ContextMenuItem[]> minimizedWindows, Action openMiningLedger, Func<bool> isMiningLedgerMinimized)
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
        this.openNotifications = openNotifications;
        this.isNotificationsMinimized = isNotificationsMinimized;
        this.unreadNotifications = unreadNotifications;
        this.openChat = openChat;
        this.isChatMinimized = isChatMinimized;
        this.showContextMenu = showContextMenu;
        this.minimizedWindows = minimizedWindows;
        this.openMiningLedger = openMiningLedger;
        this.isMiningLedgerMinimized = isMiningLedgerMinimized;
    }

    internal bool IsVisible => isEnabled() && Game1.IsHudDrawn && !Game1.eventUp && Game1.farmEvent == null
        && (Game1.activeClickableMenu == null || Game1.activeClickableMenu is GameMenu) && Game1.currentMinigame == null;

    // Keep the rail behind the tabbed menu without intercepting its input.
    internal bool IsInteractive => IsVisible && Game1.activeClickableMenu == null;

    // Native HUD dispatch consumes clicks inside this rail before they reach world/tool actions.
    // Inventory, Livestock, Mail, Chat and Notifications open desktop windows; Exit terminates the game.
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (!IsInteractive || !drawnBounds.Contains(x, y))
            return;
        if (drawnSlots[MailEntryIndex].Contains(x, y))
            openMail();
        else if (drawnSlots[InventoryEntryIndex].Contains(x, y))
            openInventory();
        else if (drawnSlots[LivestockEntryIndex].Contains(x, y))
            openLivestock();
        else if (drawnSlots[MiningEntryIndex].Contains(x, y))
            openMiningLedger();
        else if (drawnSlots[NotificationsEntryIndex].Contains(x, y))
            openNotifications();
        else if (drawnSlots[ChatEntryIndex].Contains(x, y))
            openChat();
        else if (drawnSlots[ExitEntryIndex].Contains(x, y))
            // Terminate immediately without entering the game's save or shutdown flow.
            Environment.Exit(0);
    }
    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (isWithinBounds(x, y))
            showContextMenu(new Point(x, y), new[]
            {
                new ContextMenuItem("Auto-hide", () => autoHideChecked = !autoHideChecked, autoHideChecked)
            }.Concat(minimizedWindows()).ToArray());
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
        if (!IsVisible)
            return;

        try
        {
            DrawStrip(b);
        }
        catch (Exception ex)
        {
            drawnBounds = Rectangle.Empty;
            onDrawFailed(ex);
        }
    }

    private void DrawStrip(SpriteBatch batch)
    {
        Rectangle strip = new(0, 0, StripWidth, Game1.uiViewport.Height);
        int mouseX = Game1.getMouseX(ui_scale: true);
        int mouseY = Game1.getMouseY(ui_scale: true);
        int hovered = -1;
        int hoveredY = 0;
        int bottomGroupY = strip.Bottom - TopPadding - BottomEntryCount * SlotHeight;
        int separatorY = bottomGroupY - TopPadding;

        // Reuse the game's PointClamp UI batch and sprites; never change shared render state.
        batch.Draw(Game1.staminaRect, strip, UiStyle.Background * config.Opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(StripWidth - 1, 0, 1, strip.Height), UiStyle.Border * config.Opacity);
        if (separatorY >= 0)
            batch.Draw(Game1.staminaRect, new Rectangle(4, separatorY, StripWidth - 8, 1), UiStyle.Border * config.Opacity);
        for (int i = 0; i < Entries.Length; i++)
        {
            bool isBottomEntry = i >= Entries.Length - BottomEntryCount;
            int slotY = isBottomEntry ? bottomGroupY + (i - (Entries.Length - BottomEntryCount)) * SlotHeight
                : TopPadding + i * SlotHeight;
            Rectangle slot = new(0, slotY, StripWidth - 1, SlotHeight);
            // On unusually short viewports, keep the bottom group clear of the top entries.
            if (slot.Top < 0 || slot.Bottom > strip.Bottom || (!isBottomEntry && slot.Bottom > separatorY - TopPadding))
                continue;
            drawnSlots[i] = slot;

            if (IsInteractive && slot.Contains(mouseX, mouseY))
            {
                hovered = i;
                hoveredY = slot.Y;
                batch.Draw(Game1.staminaRect, slot, UiStyle.Border * config.Opacity);
            }

            Point position = new((StripWidth - IconSize * IconScale) / 2, slot.Y + (SlotHeight - IconSize * IconScale) / 2);
            UiStyle.DrawIcon(batch, Entries[i].Emoji, position, IconScale, config.Opacity);
            if ((i == MailEntryIndex && isMailMinimized()) || (i == InventoryEntryIndex && isInventoryMinimized())
                || (i == LivestockEntryIndex && isLivestockMinimized()) || (i == MiningEntryIndex && isMiningLedgerMinimized())
                || (i == NotificationsEntryIndex && isNotificationsMinimized())
                || (i == ChatEntryIndex && isChatMinimized()))
                batch.Draw(Game1.staminaRect, new Rectangle(1, slot.Y + 8, 1, SlotHeight - 16), UiStyle.Muted * config.Opacity);
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
            int y = Math.Clamp(hoveredY + (SlotHeight - (int)MathF.Ceiling(size.Y)) / 2,
                0, Math.Max(0, strip.Height - (int)MathF.Ceiling(size.Y) - 4));
            Rectangle tooltip = new(StripWidth + 4, y, (int)MathF.Ceiling(size.X) + 12, (int)MathF.Ceiling(size.Y) + 4);
            batch.Draw(Game1.staminaRect, tooltip, UiStyle.Background * config.Opacity);
            UiText.Draw(batch, label, new Vector2(tooltip.X + 6, tooltip.Y + 2), Color.White, config);
        }

        drawnBounds = strip;
    }
}
