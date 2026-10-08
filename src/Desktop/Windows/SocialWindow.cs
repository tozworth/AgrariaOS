using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.Integration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Characters;
using StardewValley.Menus;

namespace AgrariaOS.Desktop.Windows;

internal sealed partial class SocialWindow : OsWindow
{
    internal const string WindowId = "Social";
    private const int Padding = GridView<SocialPage.SocialEntry>.Padding;
    private const int IconSize = 20;
    private readonly LookupAnythingIntegration? lookup;
    private readonly Action<object> openLookup;
    private readonly GridView<SocialPage.SocialEntry> grid = new(
        GridColumn<SocialPage.SocialEntry>.Create("Name", 210, 44, entry => entry.DisplayName, entry => entry.DisplayName,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell, id: "name"),
        GridColumn<SocialPage.SocialEntry>.Create("Hearts", 70, 36, GetHeartsText, GetHeartsSort, rightAligned: true, drawCell: DrawHeartsCell, id: "hearts"),
        GridColumn<SocialPage.SocialEntry>.Create("Status", 150, 48, GetRelationship, GetRelationship,
            textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase, id: "status"),
        GridColumn<SocialPage.SocialEntry>.Create("Gifts", 64, 32, GetGiftsText, GetGiftsSort, rightAligned: true, drawCell: DrawGiftsCell, id: "gifts"),
        GridColumn<SocialPage.SocialEntry>.Create("Talked", 70, 36, GetTalkedText, GetTalkedSort, drawCell: DrawTalkedCell, id: "talked"));

    internal SocialWindow(LookupAnythingIntegration? lookup, Action<object> openLookup)
    {
        this.lookup = lookup;
        this.openLookup = openLookup;
    }

    internal override string Id => WindowId;
    internal override string Title => "Social";
    internal override string TitleSubtext
    {
        get
        {
            int count = GetEntries().Count;
            return count == 1 ? "1 person" : $"{count} people";
        }
    }
    internal override int Icon => UiStyle.SocialIcon;
    internal override Point DefaultSize => new(600, 420);

    protected override Point GetMinimumSize(ModConfig config)
        => new(320, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        grid.SetRows(GetEntries());
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        grid.Draw(batch, contentBounds, grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding),
            config, cursor, "No friends yet.");
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (grid.ReceiveClick(cursor, leftClick))
            return;
        // Without Lookup Anything, row clicks do nothing; headers still sort.
        if (!leftClick || lookup?.Available != true || !Context.IsWorldReady
            || !Context.IsPlayerFree || Game1.activeClickableMenu != null)
            return;
        SocialPage.SocialEntry? row;
        try { row = grid.GetRow(cursor); }
        catch { return; }
        if (row?.Character is not NPC npc)
            return;
        GameLocation location;
        try { location = npc.currentLocation ?? Game1.currentLocation; }
        catch { return; }
        if (location == null)
            return;
        object? subject;
        try { subject = lookup.GetEntitySubject(npc, location); }
        catch { return; }
        if (subject == null)
            return;
        try { openLookup(subject); }
        catch { }
    }
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);
    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        if (grid.GetHeaderContextMenuItems(cursor) is { } columns)
            return columns;
        SocialPage.SocialEntry? row;
        try { row = grid.GetRow(cursor); }
        catch { return null; }
        if (!IsSummaryTarget(row))
            return null;
        // Without Lookup Anything this is the only entry; the manager appends
        // its Show Info item when GetContextMenuInfo resolves the NPC.
        string name = row!.InternalName;
        return new ContextMenuItem[] { new("Summary", () => OpenSummary(name)) };
    }

    internal override ContextMenuInfo? GetContextMenuInfo(Point cursor)
    {
        if (lookup?.Available != true || !Context.IsWorldReady)
            return null;
        SocialPage.SocialEntry? row;
        try { row = grid.GetRow(cursor); }
        catch { return null; }
        if (row?.Character is not NPC npc)
            return null;
        GameLocation location;
        try { location = npc.currentLocation ?? Game1.currentLocation; }
        catch { return null; }
        if (location == null)
            return null;
        object? subject;
        try { subject = lookup.GetEntitySubject(npc, location); }
        catch { return null; }
        if (subject == null)
            return null;
        Farmer farmer = Game1.player;
        uint day = Game1.stats.DaysPlayed;
        bool IsValid()
        {
            try
            {
                return lookup.Available && Context.IsWorldReady && Context.IsPlayerFree
                    && Game1.player == farmer && Game1.stats.DaysPlayed == day;
            }
            catch { return false; }
        }
        if (!IsValid())
            return null;
        return new ContextMenuInfo(() => IsValid() ? subject : null, IsValid);
    }

    // Mirrors the vanilla tab: only met NPCs (never farmers or children) open a profile.

    // Pops the vanilla profile window for the NPC, like clicking their vanilla slot.
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);





    // Mirrors SocialPage.drawNPCSlot/drawFarmerSlot: datable NPCs (or roommates)
    // show Single/Dating/Married/etc, farmers always show a status, others stay blank.



    // Mirrors drawNPCSlot's gift/talk icons: hidden for farmers, children and spouses.





    private static readonly Color GiftsPartialColor = new(205, 190, 110);




    // Mirrors SocialPage.CreateSpriteComponent's mugshot so grid icons match the vanilla tab.

    // Mirrors SocialPage.FindSocialCharacters without mutating friendship data:
    // farmers first, then NPCs by points/order-met/name, then children by name.

    // Mirrors SocialPage.GetAllNpcs: world/event villagers plus friendship-data
    // fallbacks whose sprites exist, deduplicated by name.
}
