using AgrariaOS.Configuration;
using AgrariaOS.Integration;
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
// Right-click offers Interact (when the world probe finds an action) plus Approach;
// Show Info is appended automatically via GetContextMenuInfo when Lookup Anything
// recognizes the row. Ctrl-click or double-click approaches without swinging tools.
internal sealed class OverviewWindow : OsWindow
{
    internal const string WindowId = "Overview";
    private const int Padding = GridView<OverviewRow>.Padding;
    private const int IconSize = 20;
    private const double ScanIntervalMs = 500;
    private const double DoubleClickMs = 400;

    private readonly LookupAnythingIntegration lookup;
    private readonly Func<ClickToMove> getMovement;
    private readonly Action releaseFocus;
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
    private OverviewRow? selected;
    private string? lastClickKey;
    private double lastClickMs = double.NegativeInfinity;
    private GameLocation? staticLocation;
    private List<StaticTile> staticTiles = new();

    internal OverviewWindow(LookupAnythingIntegration lookup, Func<ClickToMove> getMovement, Action releaseFocus)
    {
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
                int count = GetRows().Length;
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
        => new(320, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        OverviewRow[] rows;
        try { rows = GetRows(); }
        catch { rows = cachedRows; }
        grid.SetRows(rows);
        if (selected != null && !selected.IsValid())
            selected = null;
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        grid.Draw(batch, contentBounds, grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding),
            config, cursor, Context.IsWorldReady ? "No objects." : "No location.",
            isSelected: row => selected != null && row.StableKey == selected.StableKey);
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return;
        if (grid.ReceiveClick(cursor, leftClick))
            return;
        if (!Context.IsWorldReady)
            return;
        OverviewRow? row = null;
        try { row = grid.GetRow(cursor); }
        catch { row = null; }
        bool ctrl = IsControlHeld();
        if (row == null)
        {
            if (!ctrl)
                selected = null;
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
        bool doubleClick = lastClickKey != null && key == lastClickKey && now - lastClickMs < DoubleClickMs;
        lastClickKey = key;
        lastClickMs = now;
        selected = row;
        if (ctrl || doubleClick)
        {
            if (doubleClick)
                lastClickKey = null;
            try { Approach(row); }
            catch { }
        }
    }

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
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
        selected = row;
        bool hasInteract;
        try { hasInteract = TryProbe(row) != null; }
        catch { hasInteract = false; }
        var items = new List<ContextMenuItem>();
        if (hasInteract)
        {
            items.Add(new ContextMenuItem("Interact", () =>
            {
                try
                {
                    if (row.IsValid())
                        Interact(row);
                }
                catch { }
            }));
        }
        items.Add(new ContextMenuItem("Approach", () =>
        {
            try
            {
                if (row.IsValid())
                    Approach(row);
            }
            catch { }
        }));
        return items.ToArray();
    }

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
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    private static bool IsControlHeld()
    {
        try
        {
            KeyboardState keys = Game1.GetKeyboardState();
            return keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        }
        catch { return false; }
    }

    private void Interact(OverviewRow row)
    {
        if (!CanAct())
            return;
        GameLocation loc = Game1.currentLocation;
        Farmer farmer = Game1.player;
        Point tile = row.GetTile();
        InteractRequest? request = InteractProbe.TryResolve(loc, farmer, tile, includeInRange: true);
        if (request == null)
            return;
        ClickToMove mover = getMovement();
        if (mover.IsBusy)
            mover.EnqueueInteract(request, tile);
        else
            mover.StartInteract(request, tile);
        try { releaseFocus(); }
        catch { }
    }

    private void Approach(OverviewRow row)
    {
        if (!CanAct())
            return;
        Point tile = row.GetTile();
        ClickToMove mover = getMovement();
        if (mover.IsBusy)
        {
            if (!mover.EnqueueApproach(tile))
                mover.StartApproach(tile);
        }
        else
            mover.StartApproach(tile);
        try { releaseFocus(); }
        catch { }
    }

    private static bool CanAct()
    {
        try
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree || !Context.CanPlayerMove)
                return false;
            if (Game1.eventUp || Game1.currentMinigame != null || Game1.farmEvent != null
                || Game1.paused || Game1.HostPaused || Game1.fadeToBlack || Game1.freezeControls)
                return false;
            Farmer player = Game1.player;
            if (player.UsingTool || player.isEating || player.isRidingHorse() || player.IsSitting()
                || player.swimming.Value || player.isRafting)
                return false;
            return true;
        }
        catch { return false; }
    }

    private InteractRequest? TryProbe(OverviewRow row)
    {
        if (!row.IsValid())
            return null;
        GameLocation loc = Game1.currentLocation;
        Farmer farmer = Game1.player;
        Point tile = row.GetTile();
        return InteractProbe.TryResolve(loc, farmer, tile, includeInRange: true);
    }

    private OverviewRow[] GetRows()
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null)
        {
            cachedRows = Array.Empty<OverviewRow>();
            scanLocation = null;
            return cachedRows;
        }
        double now;
        try { now = Game1.currentGameTime.TotalGameTime.TotalMilliseconds; }
        catch { now = 0; }
        GameLocation loc = Game1.currentLocation;
        if (cachedRows.Length > 0 && ReferenceEquals(loc, scanLocation) && now - lastScanMs < ScanIntervalMs)
            return cachedRows;
        OverviewRow[] rows = Scan(loc);
        cachedRows = rows;
        scanLocation = loc;
        lastScanMs = now;
        return rows;
    }

    private OverviewRow[] Scan(GameLocation loc)
    {
        Farmer farmer = Game1.player;
        Point farmerTile = farmer.TilePoint;
        var list = new List<OverviewRow>();
        try
        {
            EnsureStaticTiles(loc);
        }
        catch { }

        foreach (NPC npc in loc.characters)
        {
            try
            {
                if (npc == null || npc.IsInvisible)
                    continue;
                string name = !string.IsNullOrWhiteSpace(npc.displayName) ? npc.displayName
                    : !string.IsNullOrWhiteSpace(npc.Name) ? npc.Name : "NPC";
                string category = npc.IsMonster ? "Monster"
                    : npc is Pet ? "Pet" : npc is Horse ? "Horse" : "NPC";
                list.Add(OverviewRow.ForCharacter(loc, npc, name, category, farmerTile));
            }
            catch { }
        }

        try
        {
            foreach (FarmAnimal animal in loc.animals.Values)
            {
                try
                {
                    if (animal == null)
                        continue;
                    string name = !string.IsNullOrWhiteSpace(animal.displayName) ? animal.displayName
                        : GetAnimalTypeName(animal);
                    list.Add(OverviewRow.ForFarmAnimal(loc, animal, name, GetAnimalTypeName(animal), farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var pair in loc.Objects.Pairs)
            {
                try
                {
                    StardewValley.Object obj = pair.Value;
                    if (obj == null)
                        continue;
                    Point tile = new((int)pair.Key.X, (int)pair.Key.Y);
                    string name = string.IsNullOrWhiteSpace(obj.DisplayName) ? "Object" : obj.DisplayName;
                    string category;
                    try { category = obj.getCategoryName(); }
                    catch { category = "Object"; }
                    if (string.IsNullOrWhiteSpace(category))
                        category = "Object";
                    list.Add(OverviewRow.ForObject(loc, obj, pair.Key, tile, name, category, farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (Furniture furniture in loc.furniture)
            {
                try
                {
                    if (furniture == null)
                        continue;
                    string name = string.IsNullOrWhiteSpace(furniture.DisplayName) ? "Furniture" : furniture.DisplayName;
                    list.Add(OverviewRow.ForFurniture(loc, furniture, name, "Furniture", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var pair in loc.terrainFeatures.Pairs)
            {
                try
                {
                    TerrainFeature feature = pair.Value;
                    if (feature == null)
                        continue;
                    Point tile = new((int)pair.Key.X, (int)pair.Key.Y);
                    if (feature is HoeDirt dirt)
                    {
                        if (dirt.crop == null)
                            continue;
                        string name = GetCropName(dirt);
                        list.Add(OverviewRow.ForTerrain(loc, feature, pair.Key, tile, name, "Crop", farmerTile));
                    }
                    else if (feature is FruitTree)
                    {
                        list.Add(OverviewRow.ForTerrain(loc, feature, pair.Key, tile, "Fruit Tree", "Tree", farmerTile));
                    }
                    else if (feature is Tree tree)
                    {
                        string name = tree.stump.Value ? "Tree Stump" : tree.tapped.Value ? "Tapped Tree" : "Tree";
                        list.Add(OverviewRow.ForTerrain(loc, feature, pair.Key, tile, name, "Tree", farmerTile));
                    }
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (LargeTerrainFeature large in loc.largeTerrainFeatures)
            {
                try
                {
                    if (large == null)
                        continue;
                    Rectangle box;
                    try { box = large.getBoundingBox(); }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    Point tile = new(box.Center.X / 64, box.Center.Y / 64);
                    if (large is Bush bush)
                    {
                        bool ready;
                        try { ready = bush.readyForHarvest(); }
                        catch { ready = false; }
                        list.Add(OverviewRow.ForLargeTerrain(loc, large, tile, ready ? "Bush (Ready)" : "Bush", "Bush", farmerTile));
                    }
                    else
                        list.Add(OverviewRow.ForLargeTerrain(loc, large, tile, "Terrain", "Terrain", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (ResourceClump clump in loc.resourceClumps)
            {
                try
                {
                    if (clump == null)
                        continue;
                    Point tile = new((int)clump.Tile.X, (int)clump.Tile.Y);
                    list.Add(OverviewRow.ForClump(loc, clump, tile, GetClumpName(clump), "Resource", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (Building building in loc.buildings)
            {
                try
                {
                    if (building == null)
                        continue;
                    Point tile = new(building.tileX.Value, building.tileY.Value);
                    string name = string.IsNullOrWhiteSpace(building.buildingType.Value) ? "Building" : building.buildingType.Value;
                    list.Add(OverviewRow.ForBuilding(loc, building, tile, name, "Building", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (MapSeat seat in loc.mapSeats)
            {
                try
                {
                    if (seat == null)
                        continue;
                    Point tile = new((int)seat.tilePosition.X, (int)seat.tilePosition.Y);
                    list.Add(OverviewRow.ForSeat(loc, seat, tile, "Seat", "Seat", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        foreach (StaticTile tile in staticTiles)
            list.Add(OverviewRow.ForStaticTile(loc, tile, farmerTile));

        list.AddRange(ScanDynamicTiles(loc, farmerTile));

        if (loc is FarmHouse house)
        {
            try
            {
                if (house.fridgePosition != Point.Zero)
                    list.Add(OverviewRow.ForFridge(loc, house.fridgePosition, farmerTile));
            }
            catch { }
        }

        list.Sort((a, b) =>
        {
            int compare = a.Distance.CompareTo(b.Distance);
            return compare != 0 ? compare : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });
        return list.ToArray();
    }

    private void EnsureStaticTiles(GameLocation loc)
    {
        if (ReferenceEquals(loc, staticLocation))
            return;
        staticLocation = loc;
        staticTiles = new List<StaticTile>();
        int width, height;
        try
        {
            width = loc.Map.Layers[0].LayerWidth;
            height = loc.Map.Layers[0].LayerHeight;
        }
        catch { return; }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                try
                {
                    string? action = loc.doesTileHaveProperty(x, y, "Action", "Buildings");
                    if (action != null)
                    {
                        string first = action.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Action";
                        if (string.IsNullOrWhiteSpace(first))
                            first = "Action";
                        staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Action, first, "Door", action, 0, "", ""));
                    }
                }
                catch { }
            }
        }
        try
        {
            if (loc is BusStop)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "outdoors"); }
                        catch { continue; }
                        if (index == 1057)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.BusTicket, "Bus Ticket Machine", "Service", null, index, "Buildings", "outdoors"));
                        else if (index is 958 or 1080 or 1081)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Minecart", "Warp", null, index, "Buildings", "outdoors"));
                    }
                }
            }
            else if (loc is VolcanoDungeon)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "dungeon"); }
                        catch { continue; }
                        if (index == 367)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Volcano Exit", "Warp", null, index, "Buildings", "dungeon"));
                        else if (index == 77)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Dwarf Shop", "Service", null, index, "Buildings", "dungeon"));
                    }
                }
            }
            else if (loc is CommunityCenter)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "indoors"); }
                        catch { continue; }
                        if (index == 1799 || (index >= 1824 && index <= 1833))
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Bundle", "Quest", null, index, "Buildings", "indoors"));
                    }
                }
            }
            else if (loc is Sewer)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index;
                        try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "st"); }
                        catch { continue; }
                        if (index == 21)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Sewer Exit", "Warp", null, index, "Buildings", "st"));
                        else if (index == 84)
                            staticTiles.Add(new StaticTile(new Point(x, y), StaticKind.Warp, "Krobus Shop", "Service", null, index, "Buildings", "st"));
                    }
                }
            }
        }
        catch { }
    }

    private static List<OverviewRow> ScanDynamicTiles(GameLocation loc, Point farmerTile)
    {
        var rows = new List<OverviewRow>();
        if (loc is not MineShaft)
            return rows;
        int width, height;
        try
        {
            width = loc.Map.Layers[0].LayerWidth;
            height = loc.Map.Layers[0].LayerHeight;
        }
        catch { return rows; }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index;
                try { index = loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "mine"); }
                catch { continue; }
                if (index is 284 or 112 or 115 or 173 or 174 or 194 or 315 or 316 or 317)
                {
                    string name = index switch
                    {
                        284 => "Ladder",
                        112 or 115 => "Shaft",
                        173 or 174 => "Elevator",
                        _ => "Mine Exit"
                    };
                    rows.Add(OverviewRow.ForDynamicTile(loc, new Point(x, y), name, "Warp", "Buildings", "mine", index, farmerTile));
                }
            }
        }
        return rows;
    }

    private static string GetAnimalTypeName(FarmAnimal animal)
    {
        try
        {
            if (!string.IsNullOrEmpty(animal.displayType))
                return animal.displayType;
        }
        catch { }
        try
        {
            if (!string.IsNullOrEmpty(animal.type.Value))
                return animal.type.Value;
        }
        catch { }
        return "Animal";
    }

    private static string GetCropName(HoeDirt dirt)
    {
        try
        {
            Crop? crop = dirt.crop;
            if (crop == null)
                return "Crop";
            string produce = "";
            try
            {
                string id = $"(O){crop.indexOfHarvest.Value}";
                produce = ItemRegistry.GetDataOrErrorItem(id).DisplayName;
            }
            catch { }
            if (string.IsNullOrWhiteSpace(produce))
                produce = "Crop";
            bool dead;
            try { dead = crop.dead.Value; }
            catch { dead = false; }
            bool ready;
            try { ready = dirt.readyForHarvest(); }
            catch { ready = false; }
            return dead ? $"{produce} (Dead)" : ready ? $"{produce} (Ready)" : produce;
        }
        catch { return "Crop"; }
    }

    private static string GetClumpName(ResourceClump clump)
    {
        try
        {
            if (clump is GiantCrop)
                return "Giant Crop";
            int index = clump.parentSheetIndex.Value;
            return index switch
            {
                600 or 602 => "Stump",
                44 or 46 => "Fallen Log",
                622 => "Meteorite",
                672 or 752 or 754 or 756 or 758 or 148 => "Boulder",
                _ => "Resource Clump"
            };
        }
        catch { return "Resource Clump"; }
    }

    private static void DrawNameCell(SpriteBatch batch, OverviewRow row, Rectangle bounds, ModConfig config)
    {
        try
        {
            Rectangle icon = new(bounds.X + Padding, bounds.Y + (bounds.Height - IconSize) / 2, IconSize, IconSize);
            TryDrawIcon(batch, row, icon, config.Opacity);
        }
        catch { }
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.Name, bounds, UiStyle.Primary, config);
    }

    private static bool TryDrawIcon(SpriteBatch batch, OverviewRow row, Rectangle bounds, float opacity)
    {
        try
        {
            if (row.Entity is Item item)
            {
                DrawItemIcon(batch, item, bounds, opacity);
                return true;
            }
            if (row.Entity is FarmAnimal farmAnimal)
            {
                if (TryGetAnimalSprite(farmAnimal, out Texture2D? texture, out Rectangle source) && texture != null)
                {
                    DrawFitted(batch, texture, source, bounds, Color.White * opacity);
                    return true;
                }
                return false;
            }
            if (row.Entity is NPC npc)
            {
                var sprite = npc.Sprite;
                if (sprite?.Texture == null || sprite.SourceRect.Width <= 0 || sprite.SourceRect.Height <= 0)
                    return false;
                DrawFitted(batch, sprite.Texture, sprite.SourceRect, bounds, Color.White * opacity);
                return true;
            }
            if (row.Entity is Tree tree && tree.texture.Value is { } treeTexture)
            {
                Rectangle source = tree.growthStage.Value switch
                {
                    0 => new Rectangle(32, 128, 16, 16),
                    1 => new Rectangle(0, 128, 16, 16),
                    2 => new Rectangle(16, 128, 16, 16),
                    < 5 => new Rectangle(0, 96, 16, 32),
                    _ => tree.stump.Value ? Tree.stumpSourceRect : Tree.treeTopSourceRect
                };
                Color tint = tree.growthStage.Value < 5 && tree.fertilized.Value ? Color.HotPink : Color.White;
                DrawFitted(batch, treeTexture, source, bounds, tint * opacity,
                    tree.flipped.Value ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static void DrawFitted(SpriteBatch batch, Texture2D texture, Rectangle source, Rectangle bounds,
        Color color, SpriteEffects effects = SpriteEffects.None)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return;
        float scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
        batch.Draw(texture, destination, source, color, 0f, Vector2.Zero, effects, 0.91f);
    }

    private static void DrawItemIcon(SpriteBatch batch, Item item, Rectangle bounds, float opacity)
    {
        var data = ItemRegistry.GetDataOrErrorItem(item is MeleeWeapon weapon ? weapon.GetDrawnItemId() : item.QualifiedItemId);
        Rectangle source = data.GetSourceRect(0, GetIconSpriteIndex(item));
        float scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
        Color tint = item is ColoredObject colored && colored.ColorSameIndexAsParentSheetIndex ? colored.color.Value : Color.White;
        batch.Draw(data.GetTexture(), destination, source, tint * opacity);
        if (item is ColoredObject overlay && !overlay.ColorSameIndexAsParentSheetIndex)
            batch.Draw(data.GetTexture(), destination, data.GetSourceRect(1, item.ParentSheetIndex), overlay.color.Value * opacity);
        if (item.Quality > 0)
        {
            Rectangle star = item.Quality < 4 ? new Rectangle(338 + (item.Quality - 1) * 8, 400, 8, 8) : new Rectangle(346, 392, 8, 8);
            batch.Draw(Game1.mouseCursors, new Rectangle(bounds.Right - 8, bounds.Bottom - 8, 8, 8), star, Color.White * opacity);
        }
    }

    private static int? GetIconSpriteIndex(Item item)
        => item is StardewValley.Object ? item.ParentSheetIndex : null;

    private static bool TryGetAnimalSprite(Character animal, out Texture2D? texture, out Rectangle source)
    {
        texture = null;
        source = Rectangle.Empty;
        try
        {
            switch (animal)
            {
                case FarmAnimal farmAnimal:
                {
                    var sprite = farmAnimal.GetOrLoadTexture();
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    if (sprite.SourceRect.Height > 16)
                    {
                        source = string.Equals(farmAnimal.type.Value, "Ostrich", StringComparison.Ordinal)
                            ? new Rectangle(0, sprite.SourceRect.Height * 2 - 32, sprite.SourceRect.Width, 28)
                            : new Rectangle(0, sprite.SourceRect.Height * 2 - 28, sprite.SourceRect.Width, 28);
                    }
                    else
                        source = new Rectangle(0, 16, 16, 16);
                    return true;
                }
                case Pet pet:
                {
                    var sprite = pet.Sprite;
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    source = new Rectangle(0, sprite.SourceRect.Height * 2 - 24, sprite.SourceRect.Width, 24);
                    return true;
                }
                case Horse horse:
                {
                    var sprite = horse.Sprite;
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    source = new Rectangle(0, sprite.SourceRect.Height * 2 - 26, sprite.SourceRect.Width, 24);
                    return true;
                }
            }
        }
        catch
        {
            texture = null;
            source = Rectangle.Empty;
        }
        return false;
    }

    private enum RowKind
    {
        Character,
        FarmAnimal,
        Object,
        Furniture,
        Terrain,
        LargeTerrain,
        Clump,
        Building,
        Seat,
        StaticTile,
        DynamicTile,
        Fridge
    }

    internal enum StaticKind
    {
        Action,
        BusTicket,
        Warp
    }

    internal sealed record StaticTile(Point Tile, StaticKind Kind, string Name, string Category,
        string? Action, int Index, string Layer, string Sheet);

    internal sealed class OverviewRow
    {
        internal string Name { get; }
        internal string Category { get; }
        internal float Distance { get; }
        internal string DistanceText { get; }
        internal string StableKey { get; }
        internal object? Entity { get; }
        private readonly GameLocation location;
        private readonly RowKind kind;
        private readonly Point tile;
        private readonly Vector2 tileKey;
        private readonly StaticTile? staticTile;
        private readonly string? layer;
        private readonly string? sheet;
        private readonly int tileIndex;
        private readonly string? action;
        private readonly Point fridgePosition;

        private OverviewRow(GameLocation location, RowKind kind, object? entity, Point tile, Vector2 tileKey,
            string name, string category, float distance, string stableKey,
            StaticTile? staticTile = null, string? layer = null, string? sheet = null, int tileIndex = 0,
            string? action = null, Point fridgePosition = default)
        {
            this.location = location;
            this.kind = kind;
            Entity = entity;
            this.tile = tile;
            this.tileKey = tileKey;
            Name = name;
            Category = category;
            Distance = distance;
            DistanceText = $"{distance:0.#}m";
            StableKey = stableKey;
            this.staticTile = staticTile;
            this.layer = layer;
            this.sheet = sheet;
            this.tileIndex = tileIndex;
            this.action = action;
            this.fridgePosition = fridgePosition;
        }

        private static float TileDistance(Point farmerTile, Point tile)
            => Vector2.Distance(new Vector2(farmerTile.X, farmerTile.Y), new Vector2(tile.X, tile.Y));

        internal static OverviewRow ForCharacter(GameLocation loc, NPC npc, string name, string category, Point farmerTile)
        {
            Point tile;
            try { tile = npc.TilePoint; }
            catch { tile = farmerTile; }
            return new OverviewRow(loc, RowKind.Character, npc, tile, new Vector2(tile.X, tile.Y),
                name, category, TileDistance(farmerTile, tile),
                $"char:{RuntimeHelpers.GetHashCode(npc)}");
        }

        internal static OverviewRow ForFarmAnimal(GameLocation loc, FarmAnimal animal, string name, string category, Point farmerTile)
        {
            Point tile;
            try { tile = animal.TilePoint; }
            catch { tile = farmerTile; }
            return new OverviewRow(loc, RowKind.FarmAnimal, animal, tile, new Vector2(tile.X, tile.Y),
                name, category, TileDistance(farmerTile, tile),
                $"animal:{RuntimeHelpers.GetHashCode(animal)}");
        }

        internal static OverviewRow ForObject(GameLocation loc, StardewValley.Object obj, Vector2 key, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Object, obj, tile, key, name, category, TileDistance(farmerTile, tile),
                $"obj:{RuntimeHelpers.GetHashCode(obj)}");

        internal static OverviewRow ForFurniture(GameLocation loc, Furniture furniture, string name, string category, Point farmerTile)
        {
            Point tile;
            try { tile = new Point((int)furniture.TileLocation.X, (int)furniture.TileLocation.Y); }
            catch { tile = farmerTile; }
            return new OverviewRow(loc, RowKind.Furniture, furniture, tile, new Vector2(tile.X, tile.Y),
                name, category, TileDistance(farmerTile, tile),
                $"furn:{RuntimeHelpers.GetHashCode(furniture)}");
        }

        internal static OverviewRow ForTerrain(GameLocation loc, TerrainFeature feature, Vector2 key, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Terrain, feature, tile, key, name, category, TileDistance(farmerTile, tile),
                $"terrain:{RuntimeHelpers.GetHashCode(feature)}");

        internal static OverviewRow ForLargeTerrain(GameLocation loc, LargeTerrainFeature large, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.LargeTerrain, large, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"large:{RuntimeHelpers.GetHashCode(large)}");

        internal static OverviewRow ForClump(GameLocation loc, ResourceClump clump, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Clump, clump, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"clump:{RuntimeHelpers.GetHashCode(clump)}");

        internal static OverviewRow ForBuilding(GameLocation loc, Building building, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Building, building, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"building:{RuntimeHelpers.GetHashCode(building)}");

        internal static OverviewRow ForSeat(GameLocation loc, MapSeat seat, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Seat, seat, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"seat:{RuntimeHelpers.GetHashCode(seat)}");

        internal static OverviewRow ForStaticTile(GameLocation loc, StaticTile tile, Point farmerTile)
            => new(loc, RowKind.StaticTile, null, tile.Tile, new Vector2(tile.Tile.X, tile.Tile.Y),
                tile.Name, tile.Category, TileDistance(farmerTile, tile.Tile),
                $"static:{tile.Kind}:{tile.Tile.X},{tile.Tile.Y}",
                staticTile: tile, layer: tile.Layer, sheet: tile.Sheet, tileIndex: tile.Index, action: tile.Action);

        internal static OverviewRow ForDynamicTile(GameLocation loc, Point tile, string name, string category,
            string layer, string sheet, int index, Point farmerTile)
            => new(loc, RowKind.DynamicTile, null, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"dynamic:{tile.X},{tile.Y}:{index}",
                layer: layer, sheet: sheet, tileIndex: index);

        internal static OverviewRow ForFridge(GameLocation loc, Point fridge, Point farmerTile)
            => new(loc, RowKind.Fridge, null, fridge, new Vector2(fridge.X, fridge.Y), "Fridge", "Appliance",
                TileDistance(farmerTile, fridge), $"fridge:{fridge.X},{fridge.Y}",
                fridgePosition: fridge);

        internal Point GetTile()
        {
            switch (kind)
            {
                case RowKind.Character when Entity is NPC npc:
                    return npc.TilePoint;
                case RowKind.FarmAnimal when Entity is FarmAnimal animal:
                    return animal.TilePoint;
                case RowKind.Furniture when Entity is Furniture furniture:
                    return new Point((int)furniture.TileLocation.X, (int)furniture.TileLocation.Y);
                default:
                    return tile;
            }
        }

        internal bool IsValid()
        {
            if (!Context.IsWorldReady || Game1.currentLocation != location)
                return false;
            switch (kind)
            {
                case RowKind.Character when Entity is NPC npc:
                    return location.characters.Any(candidate => ReferenceEquals(candidate, npc));
                case RowKind.FarmAnimal when Entity is FarmAnimal animal:
                    return location.animals.Values.Any(candidate => ReferenceEquals(candidate, animal));
                case RowKind.Object when Entity is StardewValley.Object obj:
                    return location.Objects.TryGetValue(tileKey, out var live) && ReferenceEquals(live, obj);
                case RowKind.Furniture when Entity is Furniture furniture:
                    return location.furniture.Contains(furniture);
                case RowKind.Terrain when Entity is TerrainFeature feature:
                    return location.terrainFeatures.TryGetValue(tileKey, out var liveFeature) && ReferenceEquals(liveFeature, feature);
                case RowKind.LargeTerrain when Entity is LargeTerrainFeature large:
                    return location.largeTerrainFeatures.Contains(large);
                case RowKind.Clump when Entity is ResourceClump clump:
                    return location.resourceClumps.Contains(clump);
                case RowKind.Building when Entity is Building building:
                    return location.buildings.Contains(building);
                case RowKind.Seat when Entity is MapSeat seat:
                    return location.mapSeats.Contains(seat);
                case RowKind.StaticTile when staticTile != null:
                    if (!location.isTileOnMap(tile))
                        return false;
                    if (staticTile.Kind == StaticKind.Action)
                    {
                        try { return location.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings") == action; }
                        catch { return false; }
                    }
                    try { return location.getTileIndexAt(new xTile.Dimensions.Location(tile.X, tile.Y), layer!, sheet!) == tileIndex; }
                    catch { return false; }
                case RowKind.DynamicTile:
                    if (!location.isTileOnMap(tile))
                        return false;
                    try { return location.getTileIndexAt(new xTile.Dimensions.Location(tile.X, tile.Y), layer!, sheet!) == tileIndex; }
                    catch { return false; }
                case RowKind.Fridge:
                    return location is FarmHouse house && house.fridgePosition == fridgePosition;
                default:
                    return false;
            }
        }
    }
}
