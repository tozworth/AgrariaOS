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

internal sealed class BundlesWindow : OsWindow
{
    internal const string WindowId = "Bundles";
    internal const int BundleIcon = 84;
    private const int Padding = GridView<BundleRow>.Padding;
    private const int IconSize = 20;
    private const int VaultArea = 4;

    private readonly TreeView tree = new();
    private readonly LookupAnythingIntegration? lookupAnything;
    private readonly GridView<BundleRow> grid = new(
        GridColumn<BundleRow>.Create("Bundle", 140, 36, row => row.BundleName, row => row.BundleName,
            comparer: StringComparer.CurrentCultureIgnoreCase, textColor: UiStyle.Muted, id: "bundle"),
        GridColumn<BundleRow>.Create("Item", 220, 44, row => row.ItemName, row => row.ItemName,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawItemCell, id: "item"),
        GridColumn<BundleRow>.Create("Qty", 64, 28, row => row.QtyText, row => row.Qty,
            rightAligned: true, id: "quantity"),
        GridColumn<BundleRow>.Create("Quality", 80, 36, row => row.QualityText, row => row.Quality, id: "quality"),
        GridColumn<BundleRow>.Create("Status", 70, 32, row => row.StatusText, row => row.Completed ? 1 : 0,
            drawCell: DrawStatusCell, id: "status"));

    private Rectangle treeBounds = Rectangle.Empty;
    private Rectangle gridBounds = Rectangle.Empty;
    private Snapshot cachedSnapshot = new();
    private double lastSnapshotMs = double.NegativeInfinity;
    private const double SnapshotIntervalMs = 500;
    private static readonly Dictionary<int, string> RepresentativeCache = new();

    internal BundlesWindow(LookupAnythingIntegration? lookupAnything = null)
    {
        this.lookupAnything = lookupAnything;
    }

    internal override string Id => WindowId;
    internal override string Title => "Bundles";
    internal override string TitleSubtext
    {
        get
        {
            try
            {
                Snapshot snapshot = GetSnapshot();
                return snapshot.TotalBundles == 0 ? "" : $"{snapshot.CompletedBundles}/{snapshot.TotalBundles} bundles";
            }
            catch
            {
                return "";
            }
        }
    }
    internal override int Icon => BundleIcon;
    internal override Point DefaultSize => new(700, 440);

    protected override Point GetMinimumSize(ModConfig config)
        => new(480, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    // Mirrors InventoryPage.ShouldShowJunimoNoteIcon: the rail only offers
    // bundles while the vanilla inventory would show its junimo button.
    internal static bool IsAvailable()
    {
        try
        {
            if (!Context.IsWorldReady)
                return false;
            return InventoryPage.ShouldShowJunimoNoteIcon();
        }
        catch
        {
            return false;
        }
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        Snapshot snapshot;
        try { snapshot = GetSnapshot(); }
        catch { snapshot = new Snapshot(); }
        tree.SetRoots(snapshot.Roots);
        tree.EnsureSelection();

        BundleRow[] rows;
        string footer;
        try
        {
            (rows, footer) = BuildRows(snapshot, tree.SelectedNode);
        }
        catch
        {
            rows = Array.Empty<BundleRow>();
            footer = "";
        }
        grid.SetRows(rows);

        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int treeWidth = Math.Clamp(contentBounds.Width * 35 / 100, 180, 280);
        if (contentBounds.Width < 320)
            treeWidth = Math.Max(0, contentBounds.Width / 2 - 8);
        treeBounds = new Rectangle(contentBounds.X, contentBounds.Y, Math.Max(0, treeWidth), contentBounds.Height);
        int dividerX = contentBounds.X + treeWidth + 3;
        gridBounds = new Rectangle(Math.Min(contentBounds.Right, dividerX + 4), contentBounds.Y,
            Math.Max(0, contentBounds.Right - dividerX - 4), contentBounds.Height);

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        tree.Draw(batch, treeBounds, rowHeight, config, cursor, Context.IsWorldReady ? "No bundles." : "No save loaded.");
        if (gridBounds.Width > 0 && gridBounds.Height > 0)
        {
            batch.Draw(Game1.staminaRect, new Rectangle(dividerX, contentBounds.Y, 1, contentBounds.Height), UiStyle.Border * config.Opacity);
            string empty = tree.SelectedNode == null ? "Select a bundle." : "No ingredients.";
            grid.Draw(batch, gridBounds, rowHeight, config, cursor, empty, footerText: footer);
        }
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (treeBounds.Contains(cursor) && tree.ReceiveClick(cursor, leftClick))
            return;
        if (gridBounds.Contains(cursor))
            grid.ReceiveClick(cursor, leftClick);
    }

    internal override void DragContent(Point cursor)
    {
        tree.Drag(cursor);
        grid.Drag(cursor);
    }

    internal override void EndContentDrag()
    {
        tree.EndDrag();
        grid.EndDrag();
    }

    internal override void ReceiveScroll(int delta)
    {
        Point cursor;
        try { cursor = new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true)); }
        catch { cursor = Point.Zero; }
        if (treeBounds.Contains(cursor))
            tree.ReceiveScroll(delta);
        else
            grid.ReceiveScroll(delta);
    }

    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        if (gridBounds.Contains(cursor))
        {
            if (grid.GetHeaderContextMenuItems(cursor) is { } columns)
                return columns;
            BundleRow? row;
            try { row = grid.GetRow(cursor); }
            catch { return null; }
            if (row == null || string.IsNullOrEmpty(row.IconId) || !IsRowCurrent(row))
                return null;
            // No domain actions; the manager appends Show Info via GetContextMenuInfo.
            if (lookupAnything?.Available != true)
                return null;
            return Array.Empty<ContextMenuItem>();
        }
        return null;
    }

    internal override ContextMenuInfo? GetContextMenuInfo(Point cursor)
    {
        if (lookupAnything?.Available != true || !gridBounds.Contains(cursor))
            return null;
        BundleRow? row;
        try { row = grid.GetRow(cursor); }
        catch { return null; }
        if (row == null || string.IsNullOrEmpty(row.IconId) || !IsRowCurrent(row))
            return null;
        string iconId = row.IconId;
        int quality = row.Quality;
        Func<bool> isCurrent = GetContextItemGuard(row);
        return new ContextMenuInfo(() =>
        {
            try
            {
                Item item = ItemRegistry.Create(iconId, 1, quality);
                return lookupAnything.GetInventorySubject(item);
            }
            catch { return null; }
        }, () => isCurrent() && Context.IsPlayerFree);
    }

    private Func<bool> GetContextItemGuard(BundleRow row)
    {
        Farmer farmer = Game1.player;
        uint day = Game1.stats.DaysPlayed;
        return () => Context.IsWorldReady && Game1.player == farmer
            && Game1.stats.DaysPlayed == day && IsValid && !IsMinimized;
    }

    private bool IsRowCurrent(BundleRow row)
    {
        try
        {
            if (!Context.IsWorldReady || !IsValid)
                return false;
            Snapshot snapshot = GetSnapshot();
            return snapshot.Bundles.Any(bundle => bundle.BundleIndex == row.BundleIndex
                && bundle.Rows.Any(candidate => candidate.IconId == row.IconId
                    && candidate.Qty == row.Qty && candidate.Quality == row.Quality
                    && candidate.ItemName == row.ItemName));
        }
        catch { return false; }
    }

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

    private sealed class Snapshot
    {
        internal List<TreeNode> Roots { get; } = new();
        internal List<BundleInfo> Bundles { get; } = new();
        internal int TotalBundles => Bundles.Count;
        internal int CompletedBundles => Bundles.Count(bundle => bundle.Complete);
    }

    private sealed class BundleInfo
    {
        internal int Area;
        internal string AreaDisplay = "";
        internal int BundleIndex;
        internal string BundleName = "";
        internal string RewardRaw = "";
        internal string RewardName = "";
        internal bool Complete;
        internal int Donated;
        internal int Required;
        internal List<BundleRow> Rows { get; } = new();
    }

    private Snapshot GetSnapshot()
    {
        double now;
        try { now = Game1.currentGameTime.TotalGameTime.TotalMilliseconds; }
        catch { now = 0; }
        if (lastSnapshotMs > double.NegativeInfinity && now - lastSnapshotMs < SnapshotIntervalMs)
            return cachedSnapshot;
        Snapshot snapshot;
        try { snapshot = BuildSnapshot(); }
        catch { return cachedSnapshot; }
        cachedSnapshot = snapshot;
        lastSnapshotMs = now;
        return snapshot;
    }

    private static Snapshot BuildSnapshot()
    {
        var snapshot = new Snapshot();
        if (!Context.IsWorldReady)
            return snapshot;
        Dictionary<string, string> bundleData;
        Dictionary<int, bool[]> bundlesState;
        try { bundleData = Game1.netWorldState.Value.BundleData; }
        catch { return snapshot; }
        if (bundleData == null || bundleData.Count == 0)
            return snapshot;
        try
        {
            bundlesState = Game1.netWorldState.Value.Bundles.Pairs
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        }
        catch
        {
            bundlesState = new Dictionary<int, bool[]>();
        }

        var byArea = new SortedDictionary<int, List<(int Index, string Raw)>>();
        foreach (var pair in bundleData)
        {
            string[] keyParts;
            try { keyParts = pair.Key.Split('/'); }
            catch { continue; }
            if (keyParts.Length < 2)
                continue;
            int area;
            try { area = CommunityCenter.getAreaNumberFromName(keyParts[0]); }
            catch { continue; }
            if (area < 0)
                continue;
            if (!int.TryParse(keyParts[1], out int bundleIndex))
                continue;
            if (!byArea.TryGetValue(area, out var list))
                byArea[area] = list = new List<(int, string)>();
            list.Add((bundleIndex, pair.Value));
        }

        foreach (var areaPair in byArea)
        {
            int area = areaPair.Key;
            string areaDisplay;
            try { areaDisplay = CommunityCenter.getAreaDisplayNameFromNumber(area); }
            catch
            {
                try { areaDisplay = CommunityCenter.getAreaNameFromNumber(area); }
                catch { areaDisplay = $"Area {area}"; }
            }
            if (string.IsNullOrWhiteSpace(areaDisplay))
                areaDisplay = $"Area {area}";
            var areaNode = new TreeNode($"area:{area}", areaDisplay);
            int areaComplete = 0;
            foreach ((int bundleIndex, string raw) in areaPair.Value.OrderBy(entry => entry.Index))
            {
                BundleInfo? bundle = ParseBundle(area, areaDisplay, bundleIndex, raw,
                    bundlesState.TryGetValue(bundleIndex, out bool[]? completed) ? completed : Array.Empty<bool>());
                if (bundle == null)
                    continue;
                snapshot.Bundles.Add(bundle);
                if (bundle.Complete)
                    areaComplete++;
                areaNode.Children.Add(new TreeNode($"bundle:{bundle.BundleIndex}", bundle.BundleName, bundle.Complete, bundle.BundleIndex));
            }
            if (areaNode.Children.Count == 0)
                continue;
            // Rebuild with the correct completion flag now that children are known.
            var finished = new TreeNode(areaNode.Id, areaNode.Label, areaComplete == areaNode.Children.Count);
            finished.Children.AddRange(areaNode.Children);
            snapshot.Roots.Add(finished);
        }
        return snapshot;
    }

    private static BundleInfo? ParseBundle(int area, string areaDisplay, int bundleIndex, string raw, bool[] completed)
    {
        string[] fields;
        try { fields = raw.Split('/'); }
        catch { return null; }
        if (fields.Length < 3)
            return null;
        string internalName = fields[0];
        string rewardRaw = fields.Length > 1 ? fields[1] : "";
        string ingredientsRaw = fields.Length > 2 ? fields[2] : "";
        string displayName = fields.Length > 6 && !string.IsNullOrWhiteSpace(fields[6]) ? fields[6] : internalName;
        string[] tokens;
        try { tokens = ingredientsRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries); }
        catch { return null; }
        int count = tokens.Length / 3;
        if (count <= 0)
            return null;
        int required = count;
        if (fields.Length > 4 && int.TryParse(fields[4], out int slots) && slots > 0)
            required = Math.Min(slots, count);
        int donated = 0;
        for (int i = 0; i < count; i++)
        {
            if (i < completed.Length && completed[i])
                donated++;
        }
        bool complete = donated >= required;
        var bundle = new BundleInfo
        {
            Area = area,
            AreaDisplay = areaDisplay,
            BundleIndex = bundleIndex,
            BundleName = displayName,
            RewardRaw = rewardRaw,
            Complete = complete,
            Donated = donated,
            Required = required
        };
        try { bundle.RewardName = ResolveRewardName(rewardRaw) ?? ""; }
        catch { bundle.RewardName = ""; }
        for (int i = 0; i < count; i++)
        {
            string idOrCategory = tokens[i * 3];
            int.TryParse(tokens[i * 3 + 1], out int stack);
            int.TryParse(tokens[i * 3 + 2], out int quality);
            bool done = i < completed.Length && completed[i];
            bundle.Rows.Add(ResolveRow(bundle, idOrCategory, Math.Max(1, stack), quality, done));
        }
        return bundle;
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

    private static void DrawItemCell(SpriteBatch batch, BundleRow row, Rectangle bounds, ModConfig config)
    {
        if (!string.IsNullOrEmpty(row.IconId))
        {
            try
            {
                var data = ItemRegistry.GetDataOrErrorItem(row.IconId);
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
                    if (row.Quality > 0)
                    {
                        Rectangle star = row.Quality < 4 ? new Rectangle(338 + (row.Quality - 1) * 8, 400, 8, 8) : new Rectangle(346, 392, 8, 8);
                        batch.Draw(Game1.mouseCursors, new Rectangle(destination.Right - 8, destination.Bottom - 8, 8, 8), star, Color.White * config.Opacity);
                    }
                }
            }
            catch { }
        }
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.ItemName, bounds, row.Completed ? UiStyle.Muted : UiStyle.Primary, config);
    }

    private static void DrawStatusCell(SpriteBatch batch, BundleRow row, Rectangle bounds, ModConfig config)
    {
        if (!row.Completed)
        {
            UiText.DrawCell(batch, row.StatusText, bounds, UiStyle.Muted, config);
            return;
        }
        // Pixel checkmark keeps donated rows independent of font glyph coverage.
        Point center = bounds.Center;
        Color color = VitalsRenderer.FullColor * config.Opacity;
        for (int i = 0; i < 4; i++)
        {
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 4 + i, center.Y + i, 1, 1), color);
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 4 + i, center.Y + i + 1, 1, 1), color);
        }
        for (int i = 0; i < 7; i++)
        {
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 1 + i, center.Y + 3 - i, 1, 1), color);
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 1 + i, center.Y + 4 - i, 1, 1), color);
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
