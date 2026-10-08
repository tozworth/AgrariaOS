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

// EVE-style overview: every object on the current map with Name/Category/Distance.
// Right-click offers entity-specific actions plus Approach;
// Show Info is appended automatically via GetContextMenuInfo when Lookup Anything
// recognizes the row. Ctrl/Shift select groups; double-click approaches without tools.
internal sealed partial class OverviewWindow : OsWindow
{
    internal const string WindowId = "Overview";
    private const int Padding = GridView<OverviewRow>.Padding;
    private const int IconSize = 20;
    private const double ScanIntervalMs = 500;
    private const double DoubleClickMs = 400;

    private readonly LookupAnythingIntegration lookup;
    private readonly Func<ClickToMove> getMovement;
    private readonly Action releaseFocus;
    private readonly ModConfig config;
    private readonly Action saveConfig;
    private readonly OverviewContextMenuRouter contextMenus = new();
    private readonly TabBar tabs = new(new WindowTab("General", "General"), new WindowTab("All", "All"));
    private static readonly string[] GeneralDefaultHidden = new[] { "Bush", "Crop", "Furniture" };
    private readonly GridView<OverviewRow> grid = new(
        GridColumn<OverviewRow>.Create("Name", 264, 44, row => row.Name, row => row.Name,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell, id: "name"),
        GridColumn<OverviewRow>.Create("Category", 140, 36, row => row.Category, row => row.Category,
            textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase, id: "category"),
        GridColumn<OverviewRow>.Create("Distance", 80, 36, row => row.DistanceText, row => row.Distance,
            rightAligned: true, id: "distance"));

    private OverviewRow[] cachedRows = Array.Empty<OverviewRow>();
    private GameLocation? scanLocation;
    private double lastScanMs = double.NegativeInfinity;
    private readonly OverviewSelection selection = new();
    private Farmer? selectionFarmer;
    private uint selectionDay;
    private string? lastClickKey;
    private double lastClickMs = double.NegativeInfinity;
    private GameLocation? staticLocation;
    private List<StaticTile> staticTiles = new();

    internal OverviewWindow(ModConfig config, Action saveConfig, LookupAnythingIntegration lookup, Func<ClickToMove> getMovement, Action releaseFocus)
    {
        this.config = config;
        this.saveConfig = saveConfig;
        this.lookup = lookup;
        this.getMovement = getMovement;
        this.releaseFocus = releaseFocus;
    }

    internal override string Id => WindowId;
    internal override string Title => "Overview";
    internal override string TitleSubtext
    {
        get
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null)
                return "";
            try
            {
                int count = GetFilteredRows().Length;
                return $"{Game1.currentLocation.Name} ({count})";
            }
            catch
            {
                return "";
            }
        }
    }
    internal override int Icon => 107;
    internal override Point DefaultSize => new(600, 420);

    protected override Point GetMinimumSize(ModConfig config)
        => new(320, TitleHeight + TabBar.Height(config) + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        OverviewRow[] rows;
        try { rows = GetRows(); }
        catch { rows = cachedRows; }
        HashSet<string> hidden = GetHiddenSet(tabs.SelectedId);
        if (hidden.Count > 0)
            rows = rows.Where(row => !hidden.Contains(row.Category)).ToArray();
        grid.SetRows(rows);
        RefreshSelection();
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        Rectangle body = tabs.Draw(batch, contentBounds, config, cursor);
        grid.Draw(batch, body, grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding),
            config, cursor, Context.IsWorldReady ? "No objects." : "No location.",
            isSelected: row => selection.Contains(row.StableKey),
            footerText: $"Selected {selection.Count}/{ClickToMove.MaxQueued}  |  Ctrl: toggle  Shift: range");
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return;
        string previousTab = tabs.SelectedId;
        if (tabs.ReceiveClick(cursor))
        {
            if (tabs.SelectedId != previousTab)
                grid.ResetScroll();
            return;
        }
        if (grid.ReceiveClick(cursor, leftClick))
            return;
        if (!Context.IsWorldReady)
            return;
        OverviewRow? row = null;
        try { row = grid.GetRow(cursor); }
        catch { row = null; }
        bool ctrl = IsControlHeld();
        bool shift = IsShiftHeld();
        if (row == null)
        {
            if (!ctrl && !shift)
                selection.Clear();
            return;
        }
        bool valid;
        try { valid = row.IsValid(); }
        catch { valid = false; }
        if (!valid)
            return;
        string key = row.StableKey;
        double now;
        try { now = Game1.currentGameTime.TotalGameTime.TotalMilliseconds; }
        catch { now = 0; }
        bool doubleClick = !ctrl && !shift && lastClickKey != null && key == lastClickKey && now - lastClickMs < DoubleClickMs;
        lastClickKey = ctrl || shift ? null : key;
        lastClickMs = now;
        RefreshSelection();
        if (!selection.Click(key, ValidRowKeys(), ctrl, shift))
            Game1.playSound("cancel");
        if (doubleClick)
        {
            lastClickKey = null;
            try { Approach(row); }
            catch { }
        }
    }

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        if (tabs.GetTabAt(cursor) is { } tabId)
        {
            string capturedTab = tabId;
            return new ContextMenuItem[]
            {
                new("Restore to defaults", () => RestoreDefaults(capturedTab))
            };
        }
        if (grid.GetHeaderContextMenuItems(cursor) is { } columns)
            return columns;
        OverviewRow? row;
        try { row = grid.GetRow(cursor); }
        catch { return null; }
        if (row == null)
            return null;
        bool valid;
        try { valid = row.IsValid(); }
        catch { valid = false; }
        if (!valid)
            return null;
        RefreshSelection();
        if (!selection.Contains(row.StableKey))
            selection.SelectOnly(row.StableKey);
        OverviewRow[] targets = grid.Rows.Where(target => selection.Contains(target.StableKey) && RowIsValid(target)).ToArray();
        var items = new List<ContextMenuItem>();
        GameLocation location = Game1.currentLocation;
        Farmer farmer = Game1.player;
        uint day = Game1.stats.DaysPlayed;
        bool IsValid(OverviewRow target) => target.IsValid() && Game1.player == farmer && Game1.currentLocation == location
            && Game1.stats.DaysPlayed == day;
        try
        {
            var requests = targets.Select(target => new OverviewMenuRequest(location, farmer, target.Entity, target.GetTile,
                () => IsValid(target), CanAct, QueueInteraction, () => TryProbe(target))).ToArray();
            items.AddRange((targets.Length > 1 ? contextMenus.CreateMany(requests, QueueInteractions)
                : contextMenus.Create(requests[0])).Select(GuardAction));
        }
        catch { }
        items.Add(new ContextMenuItem("Approach", () =>
        {
            try
            {
                if (targets.Length > 1)
                {
                    var requests = targets.Where(IsValid).Select(target =>
                    {
                        Point tile = target.GetTile();
                        Rectangle Bounds()
                        {
                            Point live = target.GetTile();
                            return new Rectangle(live.X, live.Y, 1, 1);
                        }
                        ApproachTarget? move = target.Entity is Character
                            ? new ApproachTarget(Bounds(), () => IsValid(target), Bounds)
                            : ApproachTarget.ResolveSolid(location, farmer, tile,
                                candidate => ClickToMove.CanWalkFor(location, farmer, candidate));
                        Point Hit() => move?.HitTile(farmer.TilePoint) ?? target.GetTile();
                        return new InteractRequest(move, Hit, () => IsValid(target) && move?.IsValid() != false, who =>
                        {
                            Point hit = Hit();
                            who.faceDirection(who.getGeneralDirectionTowards(new Vector2(hit.X * 64 + 32, hit.Y * 64 + 32)));
                        }) { Identity = target.Entity };
                    }).ToArray();
                    QueueInteractions(requests);
                }
                else if (IsValid(row))
                    Approach(row);
            }
            catch { }
        }, CanExecute: () => CanAct() && targets.Any(IsValid)));
        string category = row.Category;
        string currentTab = tabs.SelectedId;
        items.Add(new ContextMenuItem($"Hide '{category}'", () => HideCategory(currentTab, category)));
        return items.ToArray();
    }

    private static ContextMenuItem GuardAction(ContextMenuItem item) => item with
    {
        OnClick = () =>
        {
            try
            {
                if (item.IsEnabledNow)
                    item.OnClick?.Invoke();
            }
            catch { }
        },
        Children = item.Children?.Select(GuardAction).ToArray()
    };

    internal override ContextMenuInfo? GetContextMenuInfo(Point cursor)
    {
        if (lookup?.Available != true)
            return null;
        OverviewRow? row;
        try { row = grid.GetRow(cursor); }
        catch { return null; }
        if (row == null)
            return null;
        try
        {
            if (!row.IsValid())
                return null;
        }
        catch { return null; }
        GameLocation loc = Game1.currentLocation;
        Farmer farmer = Game1.player;
        uint day = Game1.stats.DaysPlayed;
        object? subject = null;
        try
        {
            if (row.Entity != null)
                subject = lookup.GetEntitySubject(row.Entity, loc);
            if (subject == null)
            {
                Point tile = row.GetTile();
                subject = lookup.GetTileSubject(loc, new Vector2(tile.X, tile.Y));
            }
        }
        catch { subject = null; }
        if (subject == null)
            return null;
        bool IsValid()
        {
            try
            {
                return row.IsValid() && Context.IsPlayerFree
                    && Game1.player == farmer && Game1.currentLocation == loc && Game1.stats.DaysPlayed == day;
            }
            catch { return false; }
        }
        if (!IsValid())
            return null;
        return new ContextMenuInfo(() => IsValid() ? subject : null, IsValid);
    }

    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

































}
