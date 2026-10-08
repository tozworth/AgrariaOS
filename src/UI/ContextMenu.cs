using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

internal sealed record ContextMenuItem(string Text, Action? OnClick = null, bool? IsChecked = null, bool IsEnabled = true,
    ContextMenuItem[]? Children = null);

// The caller supplies a lookup for the clicked target and its lifetime guard. This is
// shared by world providers, generic interactions and desktop content menus.
internal sealed record ContextMenuInfo(Func<object?> GetSubject, Func<bool> IsValid);

internal static class ContextMenuItems
{
    internal static ContextMenuItem[] WithInfo(ContextMenuItem[] items, ContextMenuInfo? info, Action<object> openInfo)
        => info == null ? items : items.Append(new ContextMenuItem("Show Info", () =>
        {
            if (info.IsValid() && info.GetSubject() is { } subject)
                openInfo(subject);
        })).ToArray();
}

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
    private ModConfig config = null!;
    private ContextMenu? submenu;
    private int submenuRow = -1;
    private int arrowWidth;

    internal bool IsOpen => items.Length > 0;

    internal bool Open(Point cursor, ContextMenuItem[] entries, ModConfig config,
        string? title = null, Func<bool>? isValid = null)
    {
        Close();
        if (entries.Length == 0)
            return false;

        rowHeight = UiText.LineHeight(config.FontScale) + Padding * 2;
        this.config = config;
        arrowWidth = entries.Any(item => item.Children is { Length: > 0 }) ? CheckWidth : 0;
        checkWidth = entries.Any(item => item.IsChecked.HasValue) ? CheckWidth : 0;
        this.title = title;
        this.isValid = isValid;
        headerHeight = title == null ? 0 : rowHeight;
        int width = Math.Min(Game1.uiViewport.Width, Math.Max(100,
            (int)MathF.Ceiling(Math.Max(entries.Max(item => Game1.smallFont.MeasureString(item.Text).X),
                Game1.smallFont.MeasureString(title ?? "").X) * config.FontScale)
            + checkWidth + arrowWidth + Padding * 2 + 3));
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
        CloseSubmenu();
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
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (submenu?.Contains(cursor) == true)
            return submenu.ReceiveScroll(delta);
        CloseSubmenu();
        firstRow = Math.Clamp(firstRow - Math.Sign(delta), 0, items.Length - visibleRows);
        return true;
    }

    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        bool consumed = ReceiveClickCore(cursor, leftClick, out Action? action);
        // Close the entire cascade before invoking actions which may open another popup.
        action?.Invoke();
        return consumed;
    }

    private bool ReceiveClickCore(Point cursor, bool leftClick, out Action? action)
    {
        action = null;
        Update();
        if (!IsOpen)
            return false;

        if (leftClick && submenu?.Contains(cursor) == true)
        {
            submenu.ReceiveClickCore(cursor, true, out action);
            if (!submenu.IsOpen)
                Close();
            return true;
        }

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
                    if (item.Children is { Length: > 0 })
                    {
                        OpenSubmenu(i);
                        return true;
                    }
                    action = item.OnClick;
                    break;
                }
            }
        }
        // Dismissal consumes the press too, so it can't activate the UI/world below.
        Close();
        return true;
    }

    internal void Draw(SpriteBatch batch, ModConfig config, Point cursor)
    {
        if (!IsOpen)
            return;

        if (bounds.Contains(cursor) && submenu?.Contains(cursor) != true)
        {
            int hoveredRow = (cursor.Y - bounds.Y - 1 - headerHeight) / rowHeight;
            if (cursor.Y >= bounds.Y + 1 + headerHeight && hoveredRow >= 0 && hoveredRow < visibleRows
                && items[firstRow + hoveredRow] is { IsEnabled: true, Children: { Length: > 0 } })
                OpenSubmenu(hoveredRow);
            else
                CloseSubmenu();
        }

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
            if (item.IsEnabled && (row.Contains(cursor) || i == submenuRow))
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * config.Opacity);

            if (item.IsChecked.HasValue)
            {
                Rectangle check = new(row.X + Padding, row.Center.Y - 5, 10, 10);
                UiStyle.DrawBorder(batch, check, config.Opacity * (item.IsEnabled ? 1f : 0.5f));
                if (item.IsChecked.Value)
                    batch.Draw(Game1.staminaRect, new Rectangle(check.X + 3, check.Y + 3, 4, 4), color * config.Opacity);
            }

            string text = UiText.Fit(item.Text, row.Width - checkWidth - arrowWidth - Padding * 2 - 1, config.FontScale);
            Vector2 size = Game1.smallFont.MeasureString(text) * config.FontScale;
            UiText.Draw(batch, text, new Vector2(row.X + Padding + checkWidth, row.Y + (row.Height - size.Y) / 2),
                color, config);
            if (item.Children is { Length: > 0 })
                UiText.Draw(batch, ">", new Vector2(row.Right - Padding - arrowWidth,
                    row.Y + (row.Height - size.Y) / 2), color, config);
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
        submenu?.Draw(batch, config, cursor);
    }

    private bool Contains(Point cursor) => bounds.Contains(cursor) || submenu?.Contains(cursor) == true;

    private void CloseSubmenu()
    {
        submenu?.Close();
        submenu = null;
        submenuRow = -1;
    }

    private void OpenSubmenu(int rowIndex)
    {
        if (submenuRow == rowIndex)
            return;
        CloseSubmenu();
        ContextMenuItem[]? children = items[firstRow + rowIndex].Children;
        if (children is not { Length: > 0 })
            return;
        Rectangle row = RowBounds(rowIndex);
        var child = new ContextMenu();
        if (!child.Open(new Point(bounds.Right, row.Y), children, config))
            return;
        // Flip to the left at the viewport edge; keep parent and child adjoining.
        int x = bounds.Right + child.bounds.Width <= Game1.uiViewport.Width
            ? bounds.Right : Math.Max(0, bounds.Left - child.bounds.Width);
        child.bounds = new Rectangle(x, child.bounds.Y, child.bounds.Width, child.bounds.Height);
        submenu = child;
        submenuRow = rowIndex;
    }

    private Rectangle RowBounds(int index) => new(bounds.X + 1, bounds.Y + 1 + headerHeight + index * rowHeight,
        bounds.Width - 2, rowHeight);
}
