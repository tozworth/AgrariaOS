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
    // Vanilla draws on-screen menus before HUD messages in the same UI batch,
    // so this follows the bell's current visible slot, including custom ordering.
    internal Rectangle NotificationBounds => IsVisible ? drawnSlots[NotificationsEntryIndex] : Rectangle.Empty;

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

    private List<int> GetDisplayOrder() => GetDisplayOrder(out _);

    private List<int> GetDisplayOrder(out int endCount)
    {
        bool showBundles;
        try { showBundles = isBundlesAvailable(); }
        catch { showBundles = false; }
        List<int> full = GetFullOrder();
        HashSet<string> endEntries = GetEndEntries(full);
        List<int> visible = full.Where(index => index != BundlesEntryIndex || showBundles).ToList();
        endCount = visible.Count(index => endEntries.Contains(Entries[index].Id));
        return visible.Where(index => !endEntries.Contains(Entries[index].Id))
            .Concat(visible.Where(index => endEntries.Contains(Entries[index].Id))).ToList();
    }

    private HashSet<string> GetEndEntries(List<int> full)
    {
        if (config.PelicomEndEntries.TryGetValue(Game1.player.UniqueMultiplayerID, out var saved))
            return new HashSet<string>(saved, StringComparer.Ordinal);
        return full.TakeLast(DefaultEndEntryCount).Select(index => Entries[index].Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private List<int> GetFullOrder()
    {
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
            ordered.Add(index);
        }
        foreach (string id in DefaultOrder)
        {
            if (!seen.Add(id))
                continue;
            int index = EntryIndexById[id];
            int exitPosition = ordered.IndexOf(ExitEntryIndex);
            if (index == OptionsEntryIndex && exitPosition >= 0)
                ordered.Insert(exitPosition, index);
            else
                ordered.Add(index);
        }
        return ordered;
    }

    private void CommitReorder(int draggedEntry, int dropVisibleIndex, bool intoEndGroup)
    {
        long farmerId;
        try { farmerId = Game1.player.UniqueMultiplayerID; }
        catch { return; }
        bool showBundles;
        try { showBundles = isBundlesAvailable(); }
        catch { showBundles = false; }
        List<int> normalized = GetFullOrder();
        HashSet<string> endEntries = GetEndEntries(normalized);
        List<string> full = normalized.Select(index => Entries[index].Id)
            .OrderBy(id => endEntries.Contains(id)).ToList();
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
        if (intoEndGroup)
            endEntries.Add(draggedId);
        else
            endEntries.Remove(draggedId);
        config.PelicomOrder[farmerId] = full;
        config.PelicomEndEntries[farmerId] = full.Where(endEntries.Contains).ToList();
        saveConfig();
    }

    private void ResetOrder()
    {
        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            bool changed = config.PelicomOrder.Remove(farmerId);
            changed |= config.PelicomEndEntries.Remove(farmerId);
            if (changed)
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
