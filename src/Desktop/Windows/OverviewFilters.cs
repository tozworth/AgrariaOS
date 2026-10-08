using AgrariaOS.Configuration;
using AgrariaOS.Integration;
using AgrariaOS.Interactions;
using AgrariaOS.Movement;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System.Runtime.CompilerServices;

namespace AgrariaOS.Desktop.Windows;

partial class OverviewWindow : OsWindow
{
    private static string[] GetTabDefaults(string tabId)
        => tabId == "General" ? GeneralDefaultHidden : Array.Empty<string>();

    private HashSet<string> GetHiddenSet(string tabId)
    {
        try
        {
            string[] defaults = GetTabDefaults(tabId);
            if (!Context.IsWorldReady)
                return new HashSet<string>(defaults, StringComparer.Ordinal);
            long farmerId = Game1.player.UniqueMultiplayerID;
            if (!config.OverviewHiddenCategories.TryGetValue(farmerId, out var perTab) || perTab == null)
                return new HashSet<string>(defaults, StringComparer.Ordinal);
            if (!perTab.TryGetValue(tabId, out var hidden) || hidden == null)
                return new HashSet<string>(defaults, StringComparer.Ordinal);
            return new HashSet<string>(hidden.Where(category => !string.IsNullOrWhiteSpace(category)), StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(GetTabDefaults(tabId), StringComparer.Ordinal);
        }
    }

    private void SaveHidden(string tabId, HashSet<string> hidden)
    {
        try
        {
            if (!Context.IsWorldReady)
                return;
            long farmerId = Game1.player.UniqueMultiplayerID;
            if (!config.OverviewHiddenCategories.TryGetValue(farmerId, out var perTab) || perTab == null)
                config.OverviewHiddenCategories[farmerId] = perTab = new();
            perTab[tabId] = hidden.OrderBy(category => category, StringComparer.Ordinal).ToArray();
            saveConfig();
        }
        catch { }
    }

    private void HideCategory(string tabId, string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;
        HashSet<string> hidden = GetHiddenSet(tabId);
        if (hidden.Add(category))
        {
            SaveHidden(tabId, hidden);
            grid.ResetScroll();
            selection.Refresh(grid.Rows.Where(row => !hidden.Contains(row.Category) && RowIsValid(row))
                .Select(row => row.StableKey).ToArray());
        }
    }

    private void RestoreDefaults(string tabId)
    {
        SaveHidden(tabId, new HashSet<string>(GetTabDefaults(tabId), StringComparer.Ordinal));
        grid.ResetScroll();
        HashSet<string> hidden = GetHiddenSet(tabId);
        selection.Refresh(grid.Rows.Where(row => !hidden.Contains(row.Category) && RowIsValid(row))
            .Select(row => row.StableKey).ToArray());
    }

    private OverviewRow[] GetFilteredRows()
    {
        OverviewRow[] rows;
        try { rows = GetRows(); }
        catch { rows = cachedRows; }
        HashSet<string> hidden = GetHiddenSet(tabs.SelectedId);
        return hidden.Count == 0 ? rows : rows.Where(row => !hidden.Contains(row.Category)).ToArray();
    }

}
