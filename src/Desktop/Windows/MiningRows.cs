using AgrariaOS.Configuration;
using AgrariaOS.Mining;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

partial class MiningLedgerWindow : OsWindow
{
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
