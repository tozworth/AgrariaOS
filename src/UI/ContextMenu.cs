using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

internal sealed record ContextMenuItem(string Text, Action? OnClick = null, bool? IsChecked = null, bool IsEnabled = true);

// One popup per desktop. Entries may be plain actions or checkable actions.
internal sealed class ContextMenu
{
    private const int Padding = 6;
    private const int CheckWidth = 18;
    private ContextMenuItem[] items = Array.Empty<ContextMenuItem>();
    private Rectangle bounds;
    private int rowHeight;
    private int checkWidth;
    private string? title;
    private int headerHeight;
    private int visibleRows;
    private int firstRow;
    private Func<bool>? isValid;

    internal bool IsOpen => items.Length > 0;

    internal bool Open(Point cursor, ContextMenuItem[] entries, ModConfig config,
        string? title = null, Func<bool>? isValid = null)
    {
        Close();
        if (entries.Length == 0)
            return false;

        rowHeight = UiText.LineHeight(config.FontScale) + Padding * 2;
        checkWidth = entries.Any(item => item.IsChecked.HasValue) ? CheckWidth : 0;
        this.title = title;
        this.isValid = isValid;
        headerHeight = title == null ? 0 : rowHeight;
        int width = Math.Min(Game1.uiViewport.Width, Math.Max(100,
            (int)MathF.Ceiling(Math.Max(entries.Max(item => Game1.smallFont.MeasureString(item.Text).X),
                Game1.smallFont.MeasureString(title ?? "").X) * config.FontScale)
            + checkWidth + Padding * 2 + 3));
        visibleRows = Math.Min(entries.Length, (Game1.uiViewport.Height - headerHeight - 2) / rowHeight);
        // Long contact lists scroll instead of disappearing or clipping outside the viewport.
        if (width <= 0 || visibleRows <= 0)
        {
            Close();
            return false;
        }
        int height = headerHeight + rowHeight * visibleRows + 2;

        bounds = new Rectangle(Math.Clamp(cursor.X, 0, Game1.uiViewport.Width - width),
            Math.Clamp(cursor.Y, 0, Game1.uiViewport.Height - height), width, height);
        items = entries;
        firstRow = 0;
        return true;
    }

    internal void Close()
    {
        items = Array.Empty<ContextMenuItem>();
        bounds = Rectangle.Empty;
        isValid = null;
        title = null;
        firstRow = 0;
    }

    internal void Update()
    {
        if (IsOpen && isValid?.Invoke() == false)
            Close();
    }

    internal bool ReceiveScroll(int delta)
    {
        Update();
        if (!IsOpen)
            return false;
        firstRow = Math.Clamp(firstRow - Math.Sign(delta), 0, items.Length - visibleRows);
        return true;
    }

    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        Update();
        if (!IsOpen)
            return false;

        Action? action = null;
        if (leftClick)
        {
            for (int i = 0; i < visibleRows; i++)
            {
                if (RowBounds(i).Contains(cursor))
                {
                    ContextMenuItem item = items[firstRow + i];
                    // Disabled rows consume the press and leave the popup open.
                    if (!item.IsEnabled)
                        return true;
                    action = item.OnClick;
                    break;
                }
            }
        }
        // Dismissal consumes the press too, so it can't activate the UI/world below.
        Close();
        action?.Invoke();
        return true;
    }

    internal void Draw(SpriteBatch batch, ModConfig config, Point cursor)
    {
        if (!IsOpen)
            return;

        batch.Draw(Game1.staminaRect, bounds, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, bounds, config.Opacity);
        if (title != null)
        {
            UiText.Draw(batch, UiText.Fit(title, bounds.Width - Padding * 2 - 2, config.FontScale),
                new Vector2(bounds.X + Padding + 1, bounds.Y + Padding + 1), UiStyle.Muted, config);
            batch.Draw(Game1.staminaRect, new Rectangle(bounds.X + 1, bounds.Y + headerHeight,
                bounds.Width - 2, 1), UiStyle.Border * config.Opacity);
        }
        for (int i = 0; i < visibleRows; i++)
        {
            Rectangle row = RowBounds(i);
            ContextMenuItem item = items[firstRow + i];
            Color color = item.IsEnabled ? UiStyle.Primary : UiStyle.Muted;
            if (item.IsEnabled && row.Contains(cursor))
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * config.Opacity);

            if (item.IsChecked.HasValue)
            {
                Rectangle check = new(row.X + Padding, row.Center.Y - 5, 10, 10);
                UiStyle.DrawBorder(batch, check, config.Opacity * (item.IsEnabled ? 1f : 0.5f));
                if (item.IsChecked.Value)
                    batch.Draw(Game1.staminaRect, new Rectangle(check.X + 3, check.Y + 3, 4, 4), color * config.Opacity);
            }

            string text = UiText.Fit(item.Text, row.Width - checkWidth - Padding * 2 - 1, config.FontScale);
            Vector2 size = Game1.smallFont.MeasureString(text) * config.FontScale;
            UiText.Draw(batch, text, new Vector2(row.X + Padding + checkWidth, row.Y + (row.Height - size.Y) / 2),
                color, config);
        }
        // A narrow scroll position marker leaves the full row available for its action.
        if (visibleRows < items.Length)
        {
            int trackHeight = visibleRows * rowHeight;
            int thumbHeight = Math.Max(4, trackHeight * visibleRows / items.Length);
            int thumbY = bounds.Y + 1 + headerHeight
                + (trackHeight - thumbHeight) * firstRow / (items.Length - visibleRows);
            batch.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 3, thumbY, 2, thumbHeight),
                UiStyle.Muted * config.Opacity);
        }
    }

    private Rectangle RowBounds(int index) => new(bounds.X + 1, bounds.Y + 1 + headerHeight + index * rowHeight,
        bounds.Width - 2, rowHeight);
}
