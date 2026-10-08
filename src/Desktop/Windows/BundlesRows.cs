using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.Integration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

partial class BundlesWindow : OsWindow
{
    private static (BundleRow[] Rows, string Footer) BuildRows(Snapshot snapshot, TreeNode? selected)
    {
        if (selected == null)
            return (Array.Empty<BundleRow>(), "");
        if (selected.Id.StartsWith("bundle:", StringComparison.Ordinal))
        {
            if (!int.TryParse(selected.Id["bundle:".Length..], out int bundleIndex))
                return (Array.Empty<BundleRow>(), "");
            BundleInfo? bundle = snapshot.Bundles.FirstOrDefault(candidate => candidate.BundleIndex == bundleIndex);
            if (bundle == null)
                return (Array.Empty<BundleRow>(), "");
            return (bundle.Rows.ToArray(), FormatBundleFooter(bundle));
        }
        if (selected.Id.StartsWith("area:", StringComparison.Ordinal))
        {
            if (!int.TryParse(selected.Id["area:".Length..], out int area))
                return (Array.Empty<BundleRow>(), "");
            BundleInfo[] bundles = snapshot.Bundles.Where(candidate => candidate.Area == area).ToArray();
            if (bundles.Length == 0)
                return (Array.Empty<BundleRow>(), "");
            int complete = bundles.Count(bundle => bundle.Complete);
            string areaName = bundles[0].AreaDisplay;
            var rows = bundles.SelectMany(bundle => bundle.Rows).ToArray();
            return (rows, $"{areaName}: {complete}/{bundles.Length} bundles complete");
        }
        return (Array.Empty<BundleRow>(), "");
    }

    private static string FormatBundleFooter(BundleInfo bundle)
    {
        string progress = $"{Math.Min(bundle.Donated, bundle.Required)}/{bundle.Required} donated";
        if (bundle.Complete)
            progress += " — Complete!";
        if (!string.IsNullOrWhiteSpace(bundle.RewardName))
            progress += $"  |  Reward: {bundle.RewardName}";
        else if (!string.IsNullOrWhiteSpace(bundle.RewardRaw) && bundle.RewardRaw.Length > 1)
            progress += $"  |  Reward: {bundle.RewardRaw}";
        return $"{bundle.BundleName}: {progress}";
    }

    private static string? ResolveRewardName(string rewardRaw)
    {
        if (string.IsNullOrWhiteSpace(rewardRaw) || rewardRaw.Length <= 1)
            return null;
        try
        {
#pragma warning disable CS0618 // Reward data uses the legacy text format, like vanilla's Bundle.getReward.
            Item? reward = Utility.getItemFromStandardTextDescription(rewardRaw, Game1.player);
#pragma warning restore CS0618
            string? name = reward?.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    private static BundleRow ResolveRow(BundleInfo bundle, string idOrCategory, int stack, int quality, bool completed)
    {
        // Vault bundles trade gold, not items; vanilla shows a purchase button with the price.
        if (bundle.Area == VaultArea)
        {
            return new BundleRow(bundle.BundleName, bundle.BundleIndex, bundle.Complete,
                "Gold", null, null, stack, 0, completed);
        }
        bool isCategory = int.TryParse(idOrCategory, out int category) && category < 0;
        if (isCategory)
        {
            string representative;
            try
            {
                lock (RepresentativeCache)
                {
                    if (!RepresentativeCache.TryGetValue(category, out representative!))
                    {
                        representative = JunimoNoteMenu.GetRepresentativeItemId(
                            new BundleIngredientDescription(idOrCategory, stack, quality, completed));
                        RepresentativeCache[category] = representative;
                    }
                }
            }
            catch { representative = "0"; }
            string display = ResolveCategoryName(category, representative);
            string? iconId = null;
            int? spriteIndex = null;
            try
            {
                Item iconItem = ItemRegistry.Create(representative, 1, 0);
                iconId = iconItem.QualifiedItemId;
                spriteIndex = iconItem is StardewValley.Object obj ? obj.ParentSheetIndex : null;
                if (string.IsNullOrWhiteSpace(display))
                    display = iconItem.DisplayName;
            }
            catch { }
            if (string.IsNullOrWhiteSpace(display))
                display = representative;
            return new BundleRow(bundle.BundleName, bundle.BundleIndex, bundle.Complete,
                display, iconId, spriteIndex, stack, quality, completed);
        }
        string qualified = idOrCategory;
        Item? item = null;
        try { item = ItemRegistry.Create(qualified, stack, quality); }
        catch { item = null; }
        if ((item == null || string.IsNullOrWhiteSpace(item.DisplayName) || item.DisplayName.Contains("Error", StringComparison.OrdinalIgnoreCase))
            && !qualified.StartsWith("(", StringComparison.Ordinal))
        {
            try { item = ItemRegistry.Create("(O)" + qualified, stack, quality); }
            catch { }
        }
        string name = item?.DisplayName ?? qualified;
        if (string.IsNullOrWhiteSpace(name) || name.Contains("Error", StringComparison.OrdinalIgnoreCase))
            name = qualified;
        string? icon = item?.QualifiedItemId;
        int? sprite = item is StardewValley.Object createdObj ? createdObj.ParentSheetIndex : null;
        // Preserved goods (e.g. wine/jelly) resolve through the flavored-item helper like vanilla.
        // Bundle data currently carries no preserves ID, so this stays a fallback, not a parse branch.
        return new BundleRow(bundle.BundleName, bundle.BundleIndex, bundle.Complete,
            name, icon, sprite, stack, quality, completed);
    }

    private static string ResolveCategoryName(int category, string representative)
    {
        try
        {
            return category switch
            {
                -2 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.569"),
                -75 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.570"),
                -4 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.571"),
                -5 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.572"),
                -6 => Game1.content.LoadString("Strings\\StringsFromCSFiles:CraftingRecipe.cs.573"),
                _ => ItemRegistry.GetDataOrErrorItem(representative).DisplayName,
            };
        }
        catch
        {
            try { return ItemRegistry.GetDataOrErrorItem(representative).DisplayName; }
            catch { return representative; }
        }
    }

    private sealed class BundleRow
    {
        internal string BundleName { get; }
        internal int BundleIndex { get; }
        internal bool BundleComplete { get; }
        internal string ItemName { get; }
        internal string? IconId { get; }
        internal int? SpriteIndex { get; }
        internal int Qty { get; }
        internal string QtyText => Qty.ToString("N0", CultureInfo.InvariantCulture);
        internal int Quality { get; }
        internal string QualityText => Quality switch
        {
            1 => "Silver",
            2 => "Gold",
            4 => "Iridium",
            0 => "",
            _ => $"Q{Quality.ToString(CultureInfo.InvariantCulture)}"
        };
        internal bool Completed { get; }
        internal string StatusText => Completed ? "✓" : "—";

        internal BundleRow(string bundleName, int bundleIndex, bool bundleComplete, string itemName,
            string? iconId, int? spriteIndex, int qty, int quality, bool completed)
        {
            BundleName = bundleName;
            BundleIndex = bundleIndex;
            BundleComplete = bundleComplete;
            ItemName = itemName;
            IconId = iconId;
            SpriteIndex = spriteIndex;
            Qty = qty;
            Quality = quality;
            Completed = completed;
        }
    }

}
