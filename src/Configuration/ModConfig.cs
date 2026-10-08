using StardewModdingAPI;

namespace AgrariaOS.Configuration;

public enum PelicomEdge { Left, Right, Top, Bottom }

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
    public bool ShowWeather { get; set; } = true;
    public bool ShowChatTimestamps { get; set; } = true;
    public Dictionary<long, PelicomEdge> PelicomPositions { get; set; } = new();
    // Farmer IDs survive reloads and split-screen reconnects; screen IDs don't.
    public Dictionary<long, Dictionary<string, WindowPlacement>> WindowLayouts { get; set; } = new();
    // Open windows (back-to-front) with minimized flags, per farmer. Chest and lookup windows are transient.
    public Dictionary<long, List<WindowOpenState>> OpenWindows { get; set; } = new();
    public Dictionary<long, Dictionary<string, string[]>> WindowHiddenColumns { get; set; } = new();
    public Dictionary<long, Dictionary<string, Dictionary<string, int>>> WindowColumnWidths { get; set; } = new();
    public Dictionary<long, Dictionary<string, string[]>> OverviewHiddenCategories { get; set; } = new();

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
