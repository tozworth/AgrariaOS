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
            CancelPress();
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
        List<int> order = GetDisplayOrder(out int bottomCount);
        int visibleCount = order.Count;
        int topCount = visibleCount - bottomCount;
        int length = horizontal ? strip.Width : strip.Height;
        int endGroup = length - TopPadding - bottomCount * SlotHeight;
        int separator = bottomCount > 0 ? endGroup - TopPadding : length;
        // Keep an end drop target even after its last icon is moved out.
        drawnEndStart = bottomCount > 0 ? separator : length - TopPadding - SlotHeight;
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
        if (bottomCount > 0 && separator >= 0)
            batch.Draw(Game1.staminaRect, horizontal
                ? new Rectangle(separator, strip.Y + 4, 1, StripWidth - 8)
                : new Rectangle(strip.X + 4, separator, StripWidth - 8, 1), UiStyle.Border * config.Opacity);
        Rectangle clockBounds = Rectangle.Empty;
        if (horizontal && startLimit >= TopPadding && clockSize.Y + 1 <= strip.Height)
        {
            UiText.Draw(batch, clock, new Vector2(separator - TopPadding - clockSize.X,
                strip.Y + (strip.Height - clockSize.Y) / 2), UiStyle.Primary, config, scale: 1f);
            clockBounds = new Rectangle(separator - clockWidth, strip.Y, clockWidth, strip.Height - 1);
        }
        bool isDraggingRail = dragging && pressedEntry.HasValue;
        int topOffset = TopPadding;
        for (int pos = 0; pos < order.Count; pos++)
        {
            int i = order[pos];
            bool isBottomEntry = pos >= topCount;
            int offset = isBottomEntry ? endGroup + (pos - topCount) * SlotHeight
                : topOffset;
            if (!isBottomEntry)
                topOffset += SlotHeight;
            Rectangle slot = horizontal ? new Rectangle(offset, strip.Y, SlotHeight, StripWidth - 1)
                : new Rectangle(strip.X, offset, StripWidth - 1, SlotHeight);
            // On unusually short viewports, keep the bottom group clear of the top entries.
            if (offset < 0 || offset + SlotHeight > length || (!isBottomEntry && offset + SlotHeight > startLimit))
                continue;
            drawnSlots[i] = slot;

            bool isDragged = isDraggingRail && pressedEntry == i;
            if (isDragged)
            {
                // Leave a gap at the source while the ghost follows the cursor.
                batch.Draw(Game1.staminaRect, slot, UiStyle.Background * config.Opacity);
                continue;
            }

            if (IsInteractive && !isDraggingRail && slot.Contains(mouseX, mouseY))
            {
                hovered = i;
                batch.Draw(Game1.staminaRect, slot, UiStyle.Border * config.Opacity);
            }

            Point position = new(slot.X + (slot.Width - IconSize * IconScale) / 2,
                slot.Y + (slot.Height - IconSize * IconScale) / 2);
            UiStyle.DrawIcon(batch, Entries[i].Emoji, position, IconScale, config.Opacity);
            if (i == MailEntryIndex && AgrariaOS.Mail.MailArchive.Active?.UnreadCount > 0)
                UiStyle.DrawIcon(batch, UiStyle.UnreadMailIcon, new Point(slot.Right - 10, slot.Y + 1), 1, config.Opacity);
            bool hasJournalActivity = false;
            try { hasJournalActivity = i == JournalEntryIndex && hasNewQuestActivity(); }
            catch { }
            if (hasJournalActivity)
                UiStyle.DrawIcon(batch, UiStyle.UnreadMailIcon, new Point(slot.Right - 10, slot.Y + 1), 1, config.Opacity);
            if ((i == OverviewEntryIndex && isOverviewMinimized()) || (i == MailEntryIndex && isMailMinimized())
                || (i == JournalEntryIndex && isJournalMinimized())
                || (i == InventoryEntryIndex && isInventoryMinimized())
                || (i == LivestockEntryIndex && isLivestockMinimized()) || (i == SocialEntryIndex && isSocialMinimized()) || (i == MiningEntryIndex && isMiningLedgerMinimized())
                || (i == BundlesEntryIndex && isBundlesMinimized())
                || (i == WalletEntryIndex && isWalletMinimized())
                || (i == NotificationsEntryIndex && isNotificationsMinimized())
                || (i == ChatEntryIndex && isChatMinimized())
                || (i == NotesEntryIndex && isNotesMinimized())
                || (i == OptionsEntryIndex && isOptionsMinimized()))
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

        if (isDraggingRail)
            DrawDragFeedback(batch, strip, horizontal, mouseX, mouseY);

        string? tooltipLabel = null;
        Rectangle tooltipTarget = Rectangle.Empty;
        if (hovered >= 0 && !isDraggingRail)
        {
            string label = Entries[hovered].Label;
            if (hovered == MailEntryIndex && AgrariaOS.Mail.MailArchive.Active?.UnreadCount > 0)
                label += $" ({AgrariaOS.Mail.MailArchive.Active.UnreadCount} unread)";
            if (hovered == JournalEntryIndex)
            {
                try
                {
                    if (hasNewQuestActivity())
                        label += " (!)";
                }
                catch { }
            }
            if (hovered == NotificationsEntryIndex && unreadNotifications() > 0)
                label += $" ({unreadNotifications()} unread)";
            tooltipLabel = label;
            tooltipTarget = drawnSlots[hovered];
        }
        else if (IsInteractive && !isDraggingRail && clockBounds.Contains(mouseX, mouseY))
        {
            // The date remains available here even when hidden in the status readout.
            tooltipLabel = StatusReadoutRenderer.FormatDate(Game1.dayOfMonth, Game1.currentSeason, Game1.year);
            tooltipTarget = clockBounds;
        }
        if (tooltipLabel != null)
        {
            Vector2 size = Game1.smallFont.MeasureString(tooltipLabel) * config.FontScale;
            int width = Math.Min(Game1.uiViewport.Width, (int)MathF.Ceiling(size.X) + 12);
            int height = Math.Min(Game1.uiViewport.Height, (int)MathF.Ceiling(size.Y) + 4);
            int x = edge == PelicomEdge.Right ? strip.Left - width - 4
                : horizontal ? tooltipTarget.X : strip.Right + 4;
            int y = edge == PelicomEdge.Top ? strip.Bottom + 4
                : edge == PelicomEdge.Bottom ? strip.Top - height - 4
                : tooltipTarget.Y + (SlotHeight - height) / 2;
            Rectangle tooltip = new(Math.Clamp(x, 0, Math.Max(0, Game1.uiViewport.Width - width)),
                Math.Clamp(y, 0, Math.Max(0, Game1.uiViewport.Height - height)), width, height);
            pendingTooltip = tooltip;
            pendingTooltipLabel = tooltipLabel;
            DrawTooltipRect(batch, tooltip, tooltipLabel);
        }

        drawnBounds = strip;
    }

    private void DrawDragFeedback(SpriteBatch batch, Rectangle strip, bool horizontal, int mouseX, int mouseY)
    {
        if (!pressedEntry.HasValue)
            return;
        int dragged = pressedEntry.Value;
        // Insertion indicator within the order excluding the dragged entry.
        List<int> remaining = GetDisplayOrder(out int endCount);
        int split = remaining.Count - endCount;
        if (remaining.IndexOf(dragged) < split)
            split--;
        remaining.Remove(dragged);
        int insertAt = Math.Clamp(dropPosition ?? remaining.Count, 0, remaining.Count);
        int groupStart = dropInEndGroup ? split : 0;
        int groupEnd = dropInEndGroup ? remaining.Count : split;
        Rectangle? prevSlot = null;
        Rectangle? nextSlot = null;
        for (int pos = insertAt - 1; pos >= groupStart; pos--)
        {
            Rectangle slot = drawnSlots[remaining[pos]];
            if (slot.IsEmpty)
                continue;
            prevSlot = slot;
            break;
        }
        for (int pos = insertAt; pos < groupEnd; pos++)
        {
            Rectangle slot = drawnSlots[remaining[pos]];
            if (slot.IsEmpty)
                continue;
            nextSlot = slot;
            break;
        }
        if (prevSlot.HasValue || nextSlot.HasValue)
        {
            Rectangle indicator;
            if (horizontal)
            {
                int x;
                if (prevSlot.HasValue && nextSlot.HasValue)
                    x = (prevSlot.Value.Right + nextSlot.Value.Left) / 2;
                else if (prevSlot.HasValue)
                    x = prevSlot.Value.Right;
                else
                    x = nextSlot!.Value.Left;
                indicator = new Rectangle(x - 1, strip.Y + 4, 2, StripWidth - 8);
            }
            else
            {
                int y;
                if (prevSlot.HasValue && nextSlot.HasValue)
                    y = (prevSlot.Value.Bottom + nextSlot.Value.Top) / 2;
                else if (prevSlot.HasValue)
                    y = prevSlot.Value.Bottom;
                else
                    y = nextSlot!.Value.Top;
                indicator = new Rectangle(strip.X + 4, y - 1, StripWidth - 8, 2);
            }
            batch.Draw(Game1.staminaRect, indicator, UiStyle.Primary * config.Opacity);
        }
        else
        {
            int axis = dropInEndGroup
                ? (horizontal ? strip.Width : strip.Height) - TopPadding - SlotHeight
                : TopPadding;
            Rectangle indicator = horizontal
                ? new Rectangle(axis - 1, strip.Y + 4, 2, StripWidth - 8)
                : new Rectangle(strip.X + 4, axis - 1, StripWidth - 8, 2);
            batch.Draw(Game1.staminaRect, indicator, UiStyle.Primary * config.Opacity);
        }
        // Ghost icon follows the cursor.
        int half = IconSize * IconScale / 2;
        UiStyle.DrawIcon(batch, Entries[dragged].Emoji, new Point(mouseX - half, mouseY - half), IconScale, 1f);
    }
}
