using AgrariaOS.Configuration;
using AgrariaOS.Mining;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

internal sealed class MiningLedgerWindow : OsWindow
{
    internal const string WindowId = "MiningLedger";
    private const int Padding = GridView<MiningRow>.Padding;
    private const int IconSize = 20;
    private const int GraphMinHeight = 110;
    private static readonly int[] LabeledBuckets = { 0, 4, 8, 12, 16, 19 };

    private readonly MiningLedger ledger;
    private readonly GridView<MiningRow> grid = new(
        GridColumn<MiningRow>.Create("Name", 200, 44, row => row.Name, row => row.Name,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell, id: "name"),
        GridColumn<MiningRow>.Create("Qty", 52, 28, row => row.Qty.ToString(CultureInfo.InvariantCulture),
            row => row.Qty, rightAligned: true, id: "quantity"),
        GridColumn<MiningRow>.Create("Value", 100, 36, row => GetValueText(row.Value),
            row => row.Value, rightAligned: true, id: "value"));

    internal MiningLedgerWindow(MiningLedger ledger) => this.ledger = ledger;

    internal override string Id => WindowId;
    internal override string Title => "Mining Ledger";
    internal override int Icon => 121;
    internal override Point DefaultSize => new(600, 420);

    protected override Point GetMinimumSize(ModConfig config)
    {
        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        return new(340, TitleHeight + 2 + GraphMinHeight + Padding * 2 + rowHeight * 4 + 1);
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        if (!Context.IsWorldReady)
            return;
        MiningLedger.FarmerDay day = ledger.GetDay(Game1.player.UniqueMultiplayerID);
        bool knowsPrices = Game1.player.stats.Get("Book_PriceCatalogue") != 0;
        MiningRow[] rows = BuildRows(day, knowsPrices);
        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int gridMinHeight = Padding * 2 + rowHeight * 4 + 1;
        int graphHeight = Math.Clamp(contentBounds.Height * 48 / 100, GraphMinHeight,
            Math.Max(GraphMinHeight, contentBounds.Height - gridMinHeight));
        Rectangle graphBounds = new(contentBounds.X, contentBounds.Y, contentBounds.Width, graphHeight);
        Rectangle gridBounds = new(contentBounds.X, contentBounds.Y + graphHeight,
            contentBounds.Width, Math.Max(0, contentBounds.Height - graphHeight));
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        DrawGraph(batch, graphBounds, day, rows, cursor, config);
        grid.SetRows(rows);
        grid.Draw(batch, gridBounds, rowHeight, config, cursor, "No ore recorded today.",
            footerText: day.Total == 0 ? "No ore recorded today." : $"Total {day.Total} ore", showRange: false);
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick) => grid.ReceiveClick(cursor, leftClick);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);
    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor) => grid.GetHeaderContextMenuItems(cursor);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    private static MiningRow[] BuildRows(MiningLedger.FarmerDay day, bool knowsPrices)
    {
        var rows = new MiningRow[MiningLedger.Ores.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            MiningLedger.OreDefinition ore = MiningLedger.Ores[i];
            string name = ore.FallbackName;
            try
            {
                string display = ItemRegistry.GetDataOrErrorItem(ore.QualifiedId).DisplayName;
                if (!string.IsNullOrWhiteSpace(display))
                    name = display;
            }
            catch
            {
            }
            int qty = day.Totals[i];
            rows[i] = new MiningRow(i, ore.QualifiedId, ore.SpriteIndex, name, qty,
                GetValue(ore.QualifiedId, qty, knowsPrices));
        }
        return rows;
    }

    private static long? GetValue(string qualifiedId, int qty, bool knowsPrices)
    {
        // Same Price Catalogue unlock and stack sell-price rules as the backpack sheet.
        if (!knowsPrices || qty <= 0)
            return null;
        try
        {
            var sample = new StardewValley.Object(qualifiedId, 1);
            if (!sample.CanBeLostOnDeath() || sample.bigCraftable.Value)
                return null;
            int unitPrice = sample.sellToStorePrice(-1L);
            long value = (long)unitPrice * qty;
            return unitPrice > 0 && value > 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetValueText(long? value)
        => value is { } amount ? amount.ToString("N0", CultureInfo.InvariantCulture) + " g" : "";

    private static void DrawNameCell(SpriteBatch batch, MiningRow row, Rectangle bounds, ModConfig config)
    {
        try
        {
            var data = ItemRegistry.GetDataOrErrorItem(row.QualifiedId);
            Rectangle source = data.GetSourceRect(0, row.SpriteIndex);
            if (source.Width > 0 && source.Height > 0)
            {
                float scale = Math.Min((float)IconSize / source.Width, (float)IconSize / source.Height);
                if (scale >= 1)
                    scale = MathF.Floor(scale);
                int width = Math.Max(1, (int)(source.Width * scale));
                int height = Math.Max(1, (int)(source.Height * scale));
                Rectangle destination = new(bounds.X + Padding + (IconSize - width) / 2,
                    bounds.Y + (bounds.Height - height) / 2, width, height);
                batch.Draw(data.GetTexture(), destination, source, Color.White * config.Opacity);
            }
        }
        catch
        {
        }
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.Name, bounds, UiStyle.Primary, config);
    }

    private static void DrawGraph(SpriteBatch batch, Rectangle bounds, MiningLedger.FarmerDay day,
        MiningRow[] rows, Point cursor, ModConfig config)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        int lineHeight = UiText.LineHeight(config.FontScale);
        const int labelWidth = 34;
        int axisHeight = lineHeight + 2;
        Rectangle plot = new(bounds.X + Padding + labelWidth, bounds.Y + 4,
            Math.Max(0, bounds.Width - Padding * 2 - labelWidth - 6),
            Math.Max(0, bounds.Bottom - 2 - axisHeight - (bounds.Y + 4)));
        if (plot.Width <= 0 || plot.Height <= 0)
            return;

        int max = 0;
        foreach (int[] buckets in day.Buckets)
        {
            foreach (int qty in buckets)
                max = Math.Max(max, qty);
        }
        UiStyle.DrawBorder(batch, plot, config.Opacity);
        if (max > 0)
        {
            int midY = plot.Y + plot.Height / 2;
            batch.Draw(Game1.staminaRect, new Rectangle(plot.X, midY, plot.Width, 1),
                UiStyle.Border * (config.Opacity * 0.5f));
        }
        DrawYLabels(batch, plot, labelWidth, max, config);
        DrawXLabels(batch, plot, config);

        if (max <= 0)
        {
            DrawCenteredText(batch, "No ore recorded today.", plot, UiStyle.Muted, config);
            return;
        }

        float bucketWidth = (float)plot.Width / MiningLedger.BucketCount;
        // Largest series first so smaller ones stay readable on top.
        int[] order = Enumerable.Range(0, MiningLedger.Ores.Length)
            .OrderByDescending(i => day.Totals[i]).ToArray();
        foreach (int ore in order)
        {
            if (day.Totals[ore] <= 0)
                continue;
            Color solid = MiningLedger.Ores[ore].Color * config.Opacity;
            // Translucent fills let lower series and gridlines show through overlaps.
            Color wash = MiningLedger.Ores[ore].Color * (config.Opacity * 0.45f);
            for (int bucket = 0; bucket < MiningLedger.BucketCount; bucket++)
            {
                int qty = day.Buckets[ore][bucket];
                if (qty <= 0)
                    continue;
                int height = Math.Max(1, (int)MathF.Round((float)qty / max * plot.Height));
                int left = plot.X + (int)MathF.Round(bucket * bucketWidth);
                int right = plot.X + (int)MathF.Round((bucket + 1) * bucketWidth);
                batch.Draw(Game1.staminaRect, new Rectangle(left, plot.Bottom - height,
                    Math.Max(1, right - left), height), wash);
            }
            Vector2 previous = default;
            bool hasPrevious = false;
            for (int bucket = 0; bucket < MiningLedger.BucketCount; bucket++)
            {
                Vector2 point = new(MathF.Round(plot.X + (bucket + 0.5f) * bucketWidth),
                    MathF.Round(plot.Bottom - (float)day.Buckets[ore][bucket] / max * plot.Height));
                if (hasPrevious)
                    DrawLine(batch, previous, point, solid);
                previous = point;
                hasPrevious = true;
            }
            for (int bucket = 0; bucket < MiningLedger.BucketCount; bucket++)
            {
                if (day.Buckets[ore][bucket] <= 0)
                    continue;
                int x = (int)MathF.Round(plot.X + (bucket + 0.5f) * bucketWidth);
                int y = (int)MathF.Round(plot.Bottom - (float)day.Buckets[ore][bucket] / max * plot.Height);
                batch.Draw(Game1.staminaRect, new Rectangle(x - 1, y - 1, 3, 3), solid);
            }
        }

        // "Now" marker for the live bucket.
        if (day.DaysPlayed == Game1.stats.DaysPlayed)
        {
            int now = MiningLedger.BucketFor(Game1.timeOfDay);
            int x = plot.X + (int)MathF.Round((now + 0.5f) * bucketWidth);
            batch.Draw(Game1.staminaRect, new Rectangle(Math.Clamp(x, plot.X, plot.Right - 1),
                plot.Y, 1, plot.Height), Color.White * (config.Opacity * 0.35f));
        }

        if (plot.Contains(cursor))
        {
            int bucket = Math.Clamp((int)((cursor.X - plot.X) / bucketWidth), 0, MiningLedger.BucketCount - 1);
            int left = plot.X + (int)MathF.Round(bucket * bucketWidth);
            int right = plot.X + (int)MathF.Round((bucket + 1) * bucketWidth);
            batch.Draw(Game1.staminaRect, new Rectangle(left, plot.Y,
                Math.Max(1, right - left), plot.Height), Color.White * (config.Opacity * 0.06f));
            DrawHourTooltip(batch, cursor, day, rows, bucket, config);
        }
    }

    private static void DrawHourTooltip(SpriteBatch batch, Point cursor, MiningLedger.FarmerDay day,
        MiningRow[] rows, int bucket, ModConfig config)
    {
        var entries = new List<(string Text, Color Color, Color? Chip)>();
        entries.Add((HourRangeLabel(bucket), UiStyle.Primary, null));
        int total = 0;
        for (int i = 0; i < MiningLedger.Ores.Length; i++)
        {
            int qty = day.Buckets[i][bucket];
            if (qty <= 0)
                continue;
            total += qty;
            entries.Add(($"{rows[i].Name}: {qty.ToString(CultureInfo.InvariantCulture)}",
                UiStyle.Primary, MiningLedger.Ores[i].Color));
        }
        entries.Add(total == 0 ? ("No ore", UiStyle.Muted, null)
            : ($"Total: {total.ToString(CultureInfo.InvariantCulture)}", UiStyle.Muted, null));

        int lineHeight = UiText.LineHeight(config.FontScale);
        const int pad = 6;
        float width = 0;
        foreach (var entry in entries)
        {
            float entryWidth = Game1.smallFont.MeasureString(entry.Text).X * config.FontScale;
            if (entry.Chip.HasValue)
                entryWidth += 11;
            width = Math.Max(width, entryWidth);
        }
        int boxWidth = (int)MathF.Ceiling(width) + pad * 2;
        int boxHeight = lineHeight * entries.Count + pad * 2;
        int x = cursor.X + 14;
        if (x + boxWidth > Game1.uiViewport.Width)
            x = cursor.X - 14 - boxWidth;
        x = Math.Clamp(x, 0, Math.Max(0, Game1.uiViewport.Width - boxWidth));
        int y = Math.Clamp(cursor.Y - 8, 0, Math.Max(0, Game1.uiViewport.Height - boxHeight));
        Rectangle box = new(x, y, boxWidth, boxHeight);
        batch.Draw(Game1.staminaRect, box, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, box, config.Opacity);
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            float rowY = box.Y + pad + i * lineHeight;
            float textX = box.X + pad;
            if (entry.Chip.HasValue)
            {
                batch.Draw(Game1.staminaRect, new Rectangle((int)textX,
                    (int)(rowY + (lineHeight - 8) / 2), 8, 8), entry.Chip.Value * config.Opacity);
                textX += 11;
            }
            Vector2 size = Game1.smallFont.MeasureString(entry.Text) * config.FontScale;
            UiText.Draw(batch, entry.Text, new Vector2(textX, rowY + (lineHeight - size.Y) / 2),
                entry.Color, config);
        }
    }

    private static void DrawYLabels(SpriteBatch batch, Rectangle plot, int labelWidth, int max, ModConfig config)
    {
        if (max <= 0)
        {
            Vector2 size = Game1.smallFont.MeasureString("0") * config.FontScale;
            UiText.Draw(batch, "0", new Vector2(plot.X - 4 - size.X, plot.Bottom - size.Y), UiStyle.Muted, config);
            return;
        }
        string[] labels = max >= 2
            ? new[] { max.ToString(CultureInfo.InvariantCulture), (max / 2).ToString(CultureInfo.InvariantCulture), "0" }
            : new[] { Math.Max(1, max).ToString(CultureInfo.InvariantCulture), "0" };
        float[] fractions = max >= 2 ? new[] { 0f, 0.5f, 1f } : new[] { 0f, 1f };
        for (int i = 0; i < labels.Length; i++)
        {
            string label = UiText.Fit(labels[i], labelWidth - 4, config.FontScale);
            Vector2 size = Game1.smallFont.MeasureString(label) * config.FontScale;
            float y = plot.Y + plot.Height * fractions[i] - size.Y / 2;
            if (i == 0)
                y = Math.Max(plot.Y - size.Y / 2, y);
            if (i == labels.Length - 1)
                y = Math.Min(plot.Bottom - size.Y, y);
            UiText.Draw(batch, label, new Vector2(plot.X - 4 - size.X, y), UiStyle.Muted, config);
        }
    }

    private static void DrawXLabels(SpriteBatch batch, Rectangle plot, ModConfig config)
    {
        float bucketWidth = (float)plot.Width / MiningLedger.BucketCount;
        foreach (int bucket in LabeledBuckets)
        {
            string label = HourLabel(bucket);
            Vector2 size = Game1.smallFont.MeasureString(label) * config.FontScale;
            float x = plot.X + (bucket + 0.5f) * bucketWidth - size.X / 2;
            x = Math.Clamp(x, plot.X, Math.Max(plot.X, plot.Right - size.X));
            UiText.Draw(batch, label, new Vector2(x, plot.Bottom + 2), UiStyle.Muted, config);
        }
    }

    private static string HourLabel(int bucket) => HourName((6 + bucket) % 24);

    private static string HourRangeLabel(int bucket)
        => $"{HourName((6 + bucket) % 24)} - {HourName((7 + bucket) % 24)}";

    private static string HourName(int hour) => hour switch
    {
        0 => "12a",
        12 => "12p",
        < 12 => $"{hour}a",
        _ => $"{hour - 12}p"
    };

    private static void DrawLine(SpriteBatch batch, Vector2 start, Vector2 end, Color color)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length < 0.5f)
        {
            batch.Draw(Game1.staminaRect, new Rectangle((int)start.X, (int)start.Y, 1, 1), color);
            return;
        }
        float angle = MathF.Atan2(delta.Y, delta.X);
        batch.Draw(Game1.staminaRect, start, null, color, angle, Vector2.Zero,
            new Vector2(length, 1), SpriteEffects.None, 0.91f);
    }

    private sealed class MiningRow
    {
        internal int OreIndex { get; }
        internal string QualifiedId { get; }
        internal int SpriteIndex { get; }
        internal string Name { get; }
        internal int Qty { get; }
        internal long? Value { get; }

        internal MiningRow(int oreIndex, string qualifiedId, int spriteIndex, string name, int qty, long? value)
        {
            OreIndex = oreIndex;
            QualifiedId = qualifiedId;
            SpriteIndex = spriteIndex;
            Name = name;
            Qty = qty;
            Value = value;
        }
    }
}
