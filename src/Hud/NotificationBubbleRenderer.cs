using AgrariaOS.Configuration;
using AgrariaOS.Desktop;
using AgrariaOS.Notifications;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Hud;

internal sealed class NotificationBubbleRenderer
{
    internal void Draw(SpriteBatch batch, HUDMessage message, ref int heightUsed, ModConfig config, Rectangle bell)
    {
        const int padding = 8;
        const int iconWidth = 24;
        Rectangle workArea = Pelicom.GetWorkArea(config, padding);
        int width = Math.Min(460, workArea.Width);
        int lineHeight = UiText.LineHeight(config.FontScale);
        PelicomEdge edge = Pelicom.GetPosition(config);
        bool horizontal = edge is PelicomEdge.Top or PelicomEdge.Bottom;
        bool stackDown = !bell.IsEmpty && (edge == PelicomEdge.Top
            || (!horizontal && bell.Center.Y < workArea.Center.Y));
        int left = bell.IsEmpty ? workArea.Left
            : horizontal ? Math.Clamp(bell.Center.X - width / 2, workArea.Left, workArea.Right - width)
            : edge == PelicomEdge.Right ? workArea.Right - width : workArea.Left;
        int minimumHeight = Math.Max(18, lineHeight) + padding * 2;
        if (width <= padding * 2 + iconWidth || workArea.Height < minimumHeight)
            return;
        int anchor = bell.IsEmpty ? workArea.Bottom
            : horizontal ? (stackDown ? workArea.Top : workArea.Bottom)
            : Math.Clamp(stackDown ? bell.Top : bell.Bottom,
                workArea.Top + (stackDown ? 0 : minimumHeight),
                workArea.Bottom - (stackDown ? minimumHeight : 0));
        int available = (stackDown ? workArea.Bottom - anchor : anchor - workArea.Top) - heightUsed;
        if (available < minimumHeight)
            return;

        int textWidth = width - padding * 2 - iconWidth;
        List<string> lines = UiText.Wrap(NotificationHistory.GetText(message), textWidth - 1, config.FontScale);
        int maxLines = Math.Min(4, (available - padding * 2) / lineHeight);
        if (lines.Count > maxLines)
        {
            lines.RemoveRange(maxLines, lines.Count - maxLines);
            lines[^1] = UiText.Fit(lines[^1] + "...", textWidth - 1, config.FontScale);
        }
        int height = Math.Max(18, Math.Max(1, lines.Count) * lineHeight) + padding * 2;
        Rectangle bounds = new(left, stackDown ? anchor + heightUsed : anchor - heightUsed - height, width, height);
        heightUsed += height + padding;
        float opacity = Math.Clamp(message.transparency, 0f, 1f);
        Color color = NotificationHistory.GetColor(message.whatType);
        batch.Draw(Game1.staminaRect, bounds, UiStyle.Background * (config.Opacity * opacity));
        UiStyle.DrawBorder(batch, bounds, config.Opacity * opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), color * (config.Opacity * opacity));
        UiStyle.DrawIcon(batch, UiStyle.NotificationIcon, new Point(left + padding, bounds.Y + padding), 2, config.Opacity * opacity);
        for (int i = 0; i < lines.Count; i++)
            UiText.Draw(batch, lines[i], new Vector2(left + padding + iconWidth, bounds.Y + padding + i * lineHeight), color, config, opacity);
    }
}
