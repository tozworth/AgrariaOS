using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.Desktop;

partial class WindowTabGroup
{
    internal void Draw(SpriteBatch batch, ModConfig config, Point cursor, bool isDockPreview = false)
    {
        drawnTabs.Clear();
        if (Bounds.IsEmpty || Tabs.Count == 0)
            return;

        bool contentBackground;
        try { contentBackground = Active.ShowsContentBackground; }
        catch { contentBackground = true; }
        Rectangle background = contentBackground ? Bounds : new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, TitleHeight + 1);
        batch.Draw(Game1.staminaRect, background, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, Bounds, config.Opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(Bounds.X + 1, Bounds.Y + TitleHeight, Bounds.Width - 2, 1),
            UiStyle.Border * config.Opacity);

        // Tab strip: equal widths, icon plus title text only.
        Rectangle strip = new(Bounds.X + 1, Bounds.Y + 1, Math.Max(0, Active.TitlebarActionsLeft - Bounds.X - 1), TitleHeight - 1);
        int count = Tabs.Count;
        int baseWidth = count == 0 ? 0 : strip.Width / count;
        int remainder = count == 0 ? 0 : strip.Width % count;
        int x = strip.X;
        for (int i = 0; i < count; i++)
        {
            OsWindow tab = Tabs[i];
            int tabWidth = baseWidth + (i == count - 1 ? remainder : 0);
            Rectangle tabBounds = new(x, strip.Y, Math.Max(0, tabWidth), strip.Height);
            x += tabWidth;
            if (tabBounds.Width <= 0 || tabBounds.Height <= 0)
                continue;
            drawnTabs[tab] = tabBounds;

            bool isActive = ReferenceEquals(tab, Active);
            bool hovered = tabBounds.Contains(cursor);
            if (isDockPreview)
                batch.Draw(Game1.staminaRect, tabBounds, UiStyle.DockHighlight * config.Opacity);
            else if (isActive)
                batch.Draw(Game1.staminaRect, tabBounds, UiStyle.Border * config.Opacity);
            else if (hovered)
                batch.Draw(Game1.staminaRect, tabBounds, UiStyle.Border * (config.Opacity * 0.45f));
            if (i + 1 < count)
                batch.Draw(Game1.staminaRect, new Rectangle(tabBounds.Right - 1, tabBounds.Y + 3, 1, Math.Max(0, tabBounds.Height - 6)),
                    UiStyle.Border * (config.Opacity * 0.7f));

            const int iconSize = 18;
            int iconY = tabBounds.Y + (tabBounds.Height - iconSize) / 2;
            try { tab.DrawTitleIcon(batch, new Point(tabBounds.X + 6, iconY), config); }
            catch { }
            Rectangle textBounds = new(tabBounds.X + 30, tabBounds.Y, Math.Max(0, tabBounds.Right - 6 - (tabBounds.X + 30)), tabBounds.Height);
            if (textBounds.Width > 0)
            {
                string title;
                try { title = tab.Title; }
                catch { title = ""; }
                title = UiText.Fit(title ?? "", Math.Max(0, textBounds.Width - 1), config.FontScale);
                Vector2 size = Game1.smallFont.MeasureString(title) * config.FontScale;
                if (!string.IsNullOrEmpty(title) && size.Y + 1 <= textBounds.Height)
                {
                    Color color = isActive || isDockPreview ? UiStyle.Primary : UiStyle.Muted;
                    UiText.Draw(batch, title, new Vector2(textBounds.X,
                        textBounds.Y + (textBounds.Height - size.Y) / 2), color, config);
                }
            }
        }

        Active.DrawTitlebarActions(batch, config, cursor);
        DrawButton(batch, MinimizeBounds, cursor, config, close: false);
        DrawButton(batch, CloseBounds, cursor, config, close: true);

        Rectangle contentBounds = new(Bounds.X + 1, Bounds.Y + TitleHeight + 1,
            Bounds.Width - 2, Bounds.Height - TitleHeight - 2);
        if (contentBounds.Width > 0 && contentBounds.Height > 0)
        {
            try { Active.DrawGroupedContent(batch, contentBounds, config); }
            catch (Exception ex)
            {
                // Surface content failures without losing the frame; the manager
                // reports draw errors and falls back to vanilla on the next frame.
                throw new InvalidOperationException($"Tab '{Active.Id}' failed to draw.", ex);
            }
        }
        DrawResizeHandles(batch, cursor, config);
    }

    private void DrawResizeHandles(SpriteBatch batch, Point cursor, ModConfig config)
    {
        WindowResizeEdges edges = ActiveResizeEdges != WindowResizeEdges.None ? ActiveResizeEdges : GetResizeEdges(cursor);
        Color color = (edges == WindowResizeEdges.None ? UiStyle.Muted : UiStyle.Primary) * config.Opacity;
        for (int offset = 3; offset <= 9; offset += 3)
        for (int i = 0; i < offset; i++)
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Right - 2 - i, Bounds.Bottom - 1 - offset + i, 1, 1), color);

        Color highlight = UiStyle.Primary * config.Opacity;
        if (edges.HasFlag(WindowResizeEdges.Left))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Left, Bounds.Top, 1, Bounds.Height), highlight);
        if (edges.HasFlag(WindowResizeEdges.Right))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Right - 1, Bounds.Top, 1, Bounds.Height), highlight);
        if (edges.HasFlag(WindowResizeEdges.Top))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, 1), highlight);
        if (edges.HasFlag(WindowResizeEdges.Bottom))
            batch.Draw(Game1.staminaRect, new Rectangle(Bounds.Left, Bounds.Bottom - 1, Bounds.Width, 1), highlight);
    }

    private static void DrawButton(SpriteBatch batch, Rectangle bounds, Point cursor, ModConfig config, bool close)
    {
        if (bounds.Contains(cursor))
            batch.Draw(Game1.staminaRect, bounds, UiStyle.Border * config.Opacity);

        int x = bounds.Center.X - 4;
        int y = bounds.Center.Y - 4;
        Color color = UiStyle.Primary * config.Opacity;
        if (close)
        {
            for (int i = 0; i < 9; i++)
            {
                batch.Draw(Game1.staminaRect, new Rectangle(x + i, y + i, 1, 1), color);
                batch.Draw(Game1.staminaRect, new Rectangle(x + 8 - i, y + i, 1, 1), color);
            }
        }
        else
            batch.Draw(Game1.staminaRect, new Rectangle(x, y + 7, 9, 1), color);
    }

}
