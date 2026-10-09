using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop;

partial class Pelicom : IClickableMenu
{
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
            if (i == MailEntryIndex && AgrariaOS.Mail.MailArchive.Active?.UnreadCount > 0)
                UiStyle.DrawIcon(batch, UiStyle.UnreadMailIcon, new Point(slot.Right - 10, slot.Y + 1), 1, config.Opacity);
            if ((i == OverviewEntryIndex && isOverviewMinimized()) || (i == MailEntryIndex && isMailMinimized()) || (i == InventoryEntryIndex && isInventoryMinimized())
                || (i == LivestockEntryIndex && isLivestockMinimized()) || (i == SocialEntryIndex && isSocialMinimized()) || (i == MiningEntryIndex && isMiningLedgerMinimized())
                || (i == BundlesEntryIndex && isBundlesMinimized())
                || (i == WalletEntryIndex && isWalletMinimized())
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
            if (hovered == MailEntryIndex && AgrariaOS.Mail.MailArchive.Active?.UnreadCount > 0)
                label += $" ({AgrariaOS.Mail.MailArchive.Active.UnreadCount} unread)";
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
