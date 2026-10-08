using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

internal sealed record WindowTab(string Id, string Label);

// Owns selection and hit boxes, leaving each window responsible for its tab content.
internal sealed class TabBar
{
    private readonly WindowTab[] tabs;
    private readonly Rectangle[] hitBoxes;
    internal string SelectedId { get; private set; }
    internal static int Height(ModConfig config) => UiText.LineHeight(config.FontScale) + 8;

    internal TabBar(params WindowTab[] tabs)
    {
        if (tabs.Length == 0 || tabs.Select(tab => tab.Id).Distinct().Count() != tabs.Length)
            throw new ArgumentException("Tabs must have at least one entry and unique IDs.", nameof(tabs));
        this.tabs = tabs.ToArray();
        hitBoxes = new Rectangle[tabs.Length];
        SelectedId = tabs[0].Id;
    }

    // Returns the remaining content rectangle so tab sizing is shared too.
    internal Rectangle Draw(SpriteBatch batch, Rectangle bounds, ModConfig config, Point cursor)
    {
        Array.Clear(hitBoxes);
        int height = Math.Min(bounds.Height, Height(config));
        int x = bounds.X;
        for (int i = 0; i < tabs.Length && x < bounds.Right; i++)
        {
            int width = Math.Min(bounds.Right - x,
                (int)MathF.Ceiling(Game1.smallFont.MeasureString(tabs[i].Label).X * config.FontScale) + 24);
            Rectangle tabBounds = new(x, bounds.Y, width, height);
            hitBoxes[i] = tabBounds;
            bool selected = SelectedId == tabs[i].Id;
            if (selected || tabBounds.Contains(cursor))
                batch.Draw(Game1.staminaRect, tabBounds, UiStyle.Border * (config.Opacity * (selected ? 0.6f : 0.35f)));
            string label = UiText.Fit(tabs[i].Label, Math.Max(0, width - 12), config.FontScale);
            Vector2 size = Game1.smallFont.MeasureString(label) * config.FontScale;
            if (label.Length > 0 && size.Y + 1 <= height)
                UiText.Draw(batch, label, new Vector2(x + 6, bounds.Y + (height - size.Y) / 2),
                    selected ? UiStyle.Primary : UiStyle.Muted, config);
            if (height > 0)
                batch.Draw(Game1.staminaRect, new Rectangle(x, bounds.Y + height - 1, width, 1), (selected ? UiStyle.Primary : UiStyle.Border) * config.Opacity);
            x += width;
        }
        if (height > 0 && x < bounds.Right)
            batch.Draw(Game1.staminaRect, new Rectangle(x, bounds.Y + height - 1, bounds.Right - x, 1), UiStyle.Border * config.Opacity);
        return new Rectangle(bounds.X, bounds.Y + height, bounds.Width, bounds.Height - height);
    }

    internal bool ReceiveClick(Point cursor)
    {
        string? id = GetTabAt(cursor);
        if (id == null)
            return false;
        SelectedId = id;
        return true;
    }

    internal string? GetTabAt(Point cursor)
    {
        for (int i = 0; i < hitBoxes.Length; i++)
        {
            if (!hitBoxes[i].IsEmpty && hitBoxes[i].Contains(cursor))
                return tabs[i].Id;
        }
        return null;
    }

    internal void Select(string id)
    {
        if (!tabs.Any(tab => tab.Id == id))
            throw new ArgumentException($"Unknown tab: {id}", nameof(id));
        SelectedId = id;
    }
}
