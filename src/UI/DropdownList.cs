using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace AgrariaOS.UI;

// A bounded dropdown popup with wheel and keyboard navigation. The owning window
// draws it last and keeps its anchor within that window's content pane.
internal sealed class DropdownList
{
    private IReadOnlyList<string> choices = Array.Empty<string>();
    private Action<int>? changed;
    private int first;
    private int highlighted;
    private int visible;
    private int rowHeight;
    private Rectangle bounds;
    internal bool IsOpen => changed != null;

    internal void Open(IReadOnlyList<string> choices, int selected, Action<int> changed)
    {
        Close();
        if (choices.Count == 0) return;
        this.choices = choices;
        this.changed = changed;
        highlighted = Math.Clamp(selected, 0, choices.Count - 1);
        first = highlighted;
    }

    internal void Close()
    {
        changed = null;
        choices = Array.Empty<string>();
        bounds = Rectangle.Empty;
    }

    internal bool Layout(Rectangle anchor, Rectangle pane, int rowHeight)
    {
        bounds = Rectangle.Empty;
        if (!IsOpen || anchor.IsEmpty || rowHeight <= 0 || pane.Height < rowHeight + 2) return false;
        this.rowHeight = rowHeight;
        visible = Math.Min(choices.Count, Math.Min(8, (pane.Height - 2) / rowHeight));
        first = Math.Clamp(first, 0, Math.Max(0, choices.Count - visible));
        int height = visible * rowHeight + 2;
        int y = anchor.Bottom + height <= pane.Bottom ? anchor.Bottom : anchor.Top - height;
        bounds = new Rectangle(anchor.X, Math.Clamp(y, pane.Y, pane.Bottom - height), anchor.Width, height);
        return true;
    }

    internal void Draw(SpriteBatch batch, Rectangle anchor, Rectangle pane, int rowHeight,
        ModConfig config, Point cursor)
    {
        if (!Layout(anchor, pane, rowHeight)) return;
        batch.Draw(Game1.staminaRect, bounds, UiStyle.Background); // Opaque over the rows below.
        UiStyle.DrawBorder(batch, bounds, config.Opacity);
        for (int i = 0; i < visible; i++)
        {
            int index = first + i;
            Rectangle row = new(bounds.X + 1, bounds.Y + 1 + i * rowHeight, bounds.Width - 2, rowHeight);
            if (row.Contains(cursor) || index == highlighted)
                batch.Draw(Game1.staminaRect, row, new Color(54, 101, 129) * config.Opacity);
            UiText.DrawCell(batch, choices[index], new Rectangle(row.X, row.Y, Math.Max(0, row.Width - 10), row.Height),
                UiStyle.Primary, config);
        }
        if (choices.Count > visible)
        {
            int heightThumb = Math.Max(8, (bounds.Height - 2) * visible / choices.Count);
            int yThumb = bounds.Y + 1 + (bounds.Height - 2 - heightThumb) * first / Math.Max(1, choices.Count - visible);
            batch.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 5, yThumb, 3, heightThumb), UiStyle.Muted * config.Opacity);
        }
    }

    // An open list consumes the next click even outside itself, dismissing safely.
    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        if (!IsOpen) return false;
        Action<int>? action = changed;
        int index = bounds.Contains(cursor) ? first + (cursor.Y - bounds.Y - 1) / Math.Max(1, rowHeight) : -1;
        Close();
        if (leftClick && index >= 0 && index < first + visible)
            action?.Invoke(index);
        return true;
    }

    internal void ReceiveScroll(int delta)
    {
        if (delta != 0)
            first = Math.Clamp(first - Math.Sign(delta) * Math.Max(1, Math.Abs(delta) / 120),
                0, Math.Max(0, choices.Count - visible));
    }

    internal bool ReceiveKey(Keys key)
    {
        if (!IsOpen) return false;
        if (key == Keys.Escape) Close();
        else if (key is Keys.Enter or Keys.Space)
        {
            Action<int>? action = changed;
            int index = highlighted;
            Close();
            action?.Invoke(index);
        }
        else
        {
            if (key == Keys.Up) highlighted--;
            if (key == Keys.Down) highlighted++;
            if (key == Keys.Home) highlighted = 0;
            if (key == Keys.End) highlighted = choices.Count - 1;
            highlighted = Math.Clamp(highlighted, 0, choices.Count - 1);
            if (highlighted < first) first = highlighted;
            if (highlighted >= first + visible) first = highlighted - Math.Max(1, visible) + 1;
        }
        return true;
    }
}
