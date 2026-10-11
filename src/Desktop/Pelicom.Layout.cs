using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS.Desktop;

partial class Pelicom : IClickableMenu
{
    private void SetPosition(PelicomEdge edge)
    {
        config.PelicomPositions[Game1.player.UniqueMultiplayerID] = edge;
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
        CancelPress();
        saveConfig();
    }

    internal static PelicomEdge GetPosition(ModConfig config)
        => config.PelicomPositions.GetValueOrDefault(Game1.player.UniqueMultiplayerID, PelicomEdge.Left);

    internal static Rectangle GetWorkArea(ModConfig config, int margin = 8)
    {
        PelicomEdge edge = GetPosition(config);
        int left = margin + (edge == PelicomEdge.Left ? StripWidth : 0);
        int top = margin + (edge == PelicomEdge.Top ? StripWidth : 0);
        int right = Game1.uiViewport.Width - margin - (edge == PelicomEdge.Right ? StripWidth : 0);
        int bottom = Game1.uiViewport.Height - margin - (edge == PelicomEdge.Bottom ? StripWidth : 0);
        return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    // Visible entry indices in display order. The last BottomEntryCount entries are
    // anchored to the far end of the strip. Bundles is omitted when unavailable.
    private List<int> GetDisplayOrder()
    {
        bool showBundles;
        try { showBundles = isBundlesAvailable(); }
        catch { showBundles = false; }
        List<string> stored;
        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            if (config.PelicomOrder.TryGetValue(farmerId, out List<string>? saved) && saved != null)
                stored = saved;
            else
                stored = new List<string>(DefaultOrder);
        }
        catch { stored = new List<string>(DefaultOrder); }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<int>(Entries.Length);
        foreach (string id in stored)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                continue;
            if (!EntryIndexById.TryGetValue(id, out int index))
                continue;
            if (index == BundlesEntryIndex && !showBundles)
                continue;
            ordered.Add(index);
        }
        foreach (string id in DefaultOrder)
        {
            if (!seen.Add(id))
                continue;
            int index = EntryIndexById[id];
            if (index == BundlesEntryIndex && !showBundles)
                continue;
            ordered.Add(index);
        }
        return ordered;
    }

    private void CommitReorder(int draggedEntry, int dropVisibleIndex)
    {
        long farmerId;
        try { farmerId = Game1.player.UniqueMultiplayerID; }
        catch { return; }
        bool showBundles;
        try { showBundles = isBundlesAvailable(); }
        catch { showBundles = false; }
        if (!config.PelicomOrder.TryGetValue(farmerId, out List<string>? saved) || saved == null)
        {
            saved = new List<string>(DefaultOrder);
            config.PelicomOrder[farmerId] = saved;
        }
        // Normalize the stored order to the full known set before moving.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var full = new List<string>(Entries.Length);
        foreach (string id in saved)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                continue;
            if (!EntryIndexById.ContainsKey(id))
                continue;
            full.Add(id);
        }
        foreach (string id in DefaultOrder)
        {
            if (seen.Add(id))
                full.Add(id);
        }
        string draggedId = Entries[draggedEntry].Id;
        full.RemoveAll(id => id == draggedId);
        // Map the visible insertion index (excluding the dragged entry) back to
        // the full order, skipping the hidden Bundles slot.
        var visibleWithoutDragged = new List<string>(full.Count);
        foreach (string id in full)
        {
            if (EntryIndexById.TryGetValue(id, out int index) && index == BundlesEntryIndex && !showBundles)
                continue;
            visibleWithoutDragged.Add(id);
        }
        int clamped = Math.Clamp(dropVisibleIndex, 0, visibleWithoutDragged.Count);
        int storedIndex;
        if (clamped >= visibleWithoutDragged.Count)
            storedIndex = full.Count;
        else
            storedIndex = full.IndexOf(visibleWithoutDragged[clamped]);
        if (storedIndex < 0)
            storedIndex = full.Count;
        full.Insert(storedIndex, draggedId);
        config.PelicomOrder[farmerId] = full;
        saveConfig();
    }

    private void ResetOrder()
    {
        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            if (config.PelicomOrder.Remove(farmerId))
                saveConfig();
        }
        catch { }
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
        CancelPress();
    }

    public override bool isWithinBounds(int x, int y) => IsInteractive && drawnBounds.Contains(x, y);

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        // Drawing owns UI-space geometry; discard stale bounds until the next frame.
        drawnBounds = Rectangle.Empty;
        Array.Clear(drawnSlots);
        CancelPress();
    }

}
