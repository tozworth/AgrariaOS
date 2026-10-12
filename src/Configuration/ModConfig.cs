using StardewModdingAPI;

namespace AgrariaOS.Configuration;

public enum PelicomEdge { Left, Right, Top, Bottom }

public sealed class TodoRecord
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class NoteRecord
{
    public string Id { get; set; } = "";
    // "note" is flat text; "todo" is a checklist.
    public string Kind { get; set; } = "note";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public List<TodoRecord> Items { get; set; } = new();
}

public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public bool ClickToMove { get; set; } = true;
    public bool HoverHighlight { get; set; } = true;
    public SButton ToggleKey { get; set; } = SButton.F8;
    public SButton OverviewKey { get; set; } = SButton.O;
    public int X { get; set; } = 24;
    public int Y { get; set; } = 20;
    public float TextScale { get; set; } = 1f;
    public bool PixelPerfectText { get; set; } = true;
    public float Opacity { get; set; } = 0.95f;
    public bool Use24HourClock { get; set; }
    public bool ShowLocation { get; set; } = true;
    public bool ShowSecurity { get; set; } = true;
    public bool ShowTime { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool ShowDayPhase { get; set; } = true;
    public bool ShowWeather { get; set; } = true;
    public bool ShowMoney { get; set; } = true;
    public bool ShowJournal { get; set; } = true;
    public bool ShowZoomControls { get; set; } = true;
    public bool ShowChatTimestamps { get; set; } = true;
    public Dictionary<long, PelicomEdge> PelicomPositions { get; set; } = new();
    // Pelicom icon order (entry ids, top-to-bottom or left-to-right), per farmer.
    public Dictionary<long, List<string>> PelicomOrder { get; set; } = new();
    // Membership of the bottom/right group; an empty list means no end icons.
    // Missing farmers retain the legacy last-two grouping until their first drag.
    public Dictionary<long, List<string>> PelicomEndEntries { get; set; } = new();
    // Farmer IDs survive reloads and split-screen reconnects; screen IDs don't.
    public Dictionary<long, Dictionary<string, WindowPlacement>> WindowLayouts { get; set; } = new();
    // Open windows (back-to-front) with minimized flags, per farmer. Chest and lookup windows are transient.
    public Dictionary<long, List<WindowOpenState>> OpenWindows { get; set; } = new();
    // Tab groups (tab order with the active tab), per farmer. Groups with fewer than two
    // surviving windows are dropped on load. Chest and lookup windows are transient.
    public Dictionary<long, List<TabGroupState>> WindowTabGroups { get; set; } = new();
    public Dictionary<long, Dictionary<string, string[]>> WindowHiddenColumns { get; set; } = new();
    public Dictionary<long, Dictionary<string, Dictionary<string, int>>> WindowColumnWidths { get; set; } = new();
    public Dictionary<long, Dictionary<string, string[]>> OverviewHiddenCategories { get; set; } = new();
    // Player notes and to-do lists, per farmer.
    public Dictionary<long, List<NoteRecord>> Notes { get; set; } = new();

    internal void Normalize()
    {
        X = Math.Max(0, X);
        Y = Math.Max(0, Y);
        TextScale = float.IsFinite(TextScale) ? Math.Clamp(TextScale, 0.5f, 2f) : 1f;
        Opacity = float.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.1f, 1f) : 0.95f;
        WindowLayouts ??= new();
        PelicomPositions ??= new();
        foreach (long farmerId in PelicomPositions.Keys.ToArray())
            if (!Enum.IsDefined(typeof(PelicomEdge), PelicomPositions[farmerId]))
                PelicomPositions.Remove(farmerId);
        PelicomOrder ??= new();
        PelicomEndEntries ??= new();
        foreach (long farmerId in PelicomEndEntries.Keys.ToArray())
        {
            if (PelicomEndEntries[farmerId] is not { } entries)
                PelicomEndEntries.Remove(farmerId);
            else
                PelicomEndEntries[farmerId] = entries.Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal).ToList();
        }
        foreach (long farmerId in PelicomOrder.Keys.ToArray())
        {
            List<string>? order = PelicomOrder[farmerId];
            if (order == null)
            {
                PelicomOrder.Remove(farmerId);
                continue;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            PelicomOrder[farmerId] = order.Where(id => !string.IsNullOrWhiteSpace(id) && seen.Add(id)).ToList();
            if (PelicomOrder[farmerId].Count == 0)
                PelicomOrder.Remove(farmerId);
        }
        foreach (long farmerId in WindowLayouts.Keys.ToArray())
        {
            Dictionary<string, WindowPlacement>? layouts = WindowLayouts[farmerId];
            if (layouts == null)
            {
                WindowLayouts.Remove(farmerId);
                continue;
            }
            foreach (string id in layouts.Keys.ToArray())
            {
                if (layouts[id] is not { Width: > 0, Height: > 0 })
                    layouts.Remove(id);
            }
        }
        WindowHiddenColumns ??= new();
        foreach (long farmerId in WindowHiddenColumns.Keys.ToArray())
        {
            Dictionary<string, string[]>? columns = WindowHiddenColumns[farmerId];
            if (columns == null)
            {
                WindowHiddenColumns.Remove(farmerId);
                continue;
            }
            foreach (string id in columns.Keys.ToArray())
                columns[id] = (columns[id] ?? Array.Empty<string>())
                    .Where(column => !string.IsNullOrWhiteSpace(column)).Distinct(StringComparer.Ordinal).ToArray();
        }
        WindowColumnWidths ??= new();
        foreach (long farmerId in WindowColumnWidths.Keys.ToArray())
        {
            Dictionary<string, Dictionary<string, int>>? windows = WindowColumnWidths[farmerId];
            if (windows == null)
            {
                WindowColumnWidths.Remove(farmerId);
                continue;
            }
            foreach (string id in windows.Keys.ToArray())
            {
                if (windows[id] is not { } widths)
                {
                    windows.Remove(id);
                    continue;
                }
                foreach (string column in widths.Keys.ToArray())
                    if (string.IsNullOrWhiteSpace(column) || widths[column] < 0)
                        widths.Remove(column);
            }
        }
        OpenWindows ??= new();
        foreach (long farmerId in OpenWindows.Keys.ToArray())
        {
            List<WindowOpenState>? open = OpenWindows[farmerId];
            if (open == null)
            {
                OpenWindows.Remove(farmerId);
                continue;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            OpenWindows[farmerId] = open.Where(state => state != null && !string.IsNullOrWhiteSpace(state.Id) && seen.Add(state.Id)).ToList();
        }
        WindowTabGroups ??= new();
        foreach (long farmerId in WindowTabGroups.Keys.ToArray())
        {
            List<TabGroupState>? groups = WindowTabGroups[farmerId];
            if (groups == null)
            {
                WindowTabGroups.Remove(farmerId);
                continue;
            }
            var cleaned = new List<TabGroupState>();
            foreach (TabGroupState group in groups)
            {
                if (group?.WindowIds == null)
                    continue;
                string[] ids = group.WindowIds.Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal).ToArray();
                if (ids.Length < 2)
                    continue;
                string active = string.IsNullOrWhiteSpace(group.ActiveId) || !ids.Contains(group.ActiveId, StringComparer.Ordinal)
                    ? ids[0] : group.ActiveId;
                cleaned.Add(new TabGroupState { WindowIds = ids.ToList(), ActiveId = active });
            }
            if (cleaned.Count == 0)
                WindowTabGroups.Remove(farmerId);
            else
                WindowTabGroups[farmerId] = cleaned;
        }
        Notes ??= new();
        foreach (long farmerId in Notes.Keys.ToArray())
        {
            List<NoteRecord>? docs = Notes[farmerId];
            if (docs == null)
            {
                Notes.Remove(farmerId);
                continue;
            }
            var seenDocs = new HashSet<string>(StringComparer.Ordinal);
            foreach (NoteRecord? doc in docs.ToArray())
            {
                if (doc == null)
                {
                    docs.Remove(doc!);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(doc.Id))
                    doc.Id = Guid.NewGuid().ToString("N");
                if (!seenDocs.Add(doc.Id))
                {
                    docs.Remove(doc);
                    continue;
                }
                if (doc.Kind != "todo")
                    doc.Kind = "note";
                doc.Title ??= "";
                doc.Body ??= "";
                doc.Items ??= new();
                var seenItems = new HashSet<string>(StringComparer.Ordinal);
                foreach (TodoRecord? item in doc.Items.ToArray())
                {
                    if (item == null)
                    {
                        doc.Items.Remove(item!);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(item.Id))
                        item.Id = Guid.NewGuid().ToString("N");
                    if (!seenItems.Add(item.Id))
                        doc.Items.Remove(item);
                    item.Text ??= "";
                }
            }
            if (docs.Count == 0)
                Notes.Remove(farmerId);
        }
        OverviewHiddenCategories ??= new();
        foreach (long farmerId in OverviewHiddenCategories.Keys.ToArray())
        {
            Dictionary<string, string[]>? tabs = OverviewHiddenCategories[farmerId];
            if (tabs == null)
            {
                OverviewHiddenCategories.Remove(farmerId);
                continue;
            }
            foreach (string id in tabs.Keys.ToArray())
                tabs[id] = (tabs[id] ?? Array.Empty<string>())
                    .Where(category => !string.IsNullOrWhiteSpace(category)).Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    // Existing 0.85 configs also get the sharper rendering without a migration/reset.
    internal float FontScale => PixelPerfectText ? Math.Clamp(MathF.Round(TextScale), 1f, 2f) : TextScale;
}
