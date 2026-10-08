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

}
