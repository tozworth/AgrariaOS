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

internal sealed class SocialWindow : OsWindow
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
    private static bool IsSummaryTarget(SocialPage.SocialEntry? row)
    {
        try { return row != null && !row.IsPlayer && !row.IsChild && row.Friendship != null; }
        catch { return false; }
    }

    // Pops the vanilla profile window for the NPC, like clicking their vanilla slot.
    private static void OpenSummary(string internalName)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu != null)
            return;
        List<SocialPage.SocialEntry> entries;
        try { entries = GetEntries(); }
        catch { return; }
        SocialPage.SocialEntry? entry = null;
        try
        {
            entry = entries.FirstOrDefault(candidate => !candidate.IsPlayer && !candidate.IsChild
                && candidate.Friendship != null && candidate.InternalName == internalName);
        }
        catch { return; }
        if (entry?.Character is not NPC)
            return;
        try { Game1.activeClickableMenu = new ProfileMenu(entry, entries); }
        catch { }
    }
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    private static int? GetHeartsSort(SocialPage.SocialEntry entry)
        => entry.IsPlayer ? null : (int?)entry.HeartLevel;

    private static string GetHeartsText(SocialPage.SocialEntry entry)
    {
        if (entry.IsPlayer)
            return "";
        return $"{entry.HeartLevel}/{GetMaxHearts(entry)}";
    }

    private static int GetMaxHearts(SocialPage.SocialEntry entry)
    {
        try
        {
            return Math.Max(Utility.GetMaximumHeartsForCharacter(entry.Character), 10);
        }
        catch
        {
            return 10;
        }
    }

    private static void DrawHeartsCell(SpriteBatch batch, SocialPage.SocialEntry entry, Rectangle bounds, ModConfig config)
    {
        if (entry.IsPlayer)
            return;
        int max = GetMaxHearts(entry);
        UiText.DrawCell(batch, GetHeartsText(entry), bounds,
            UiStyle.HeartColor(max > 0 ? (float)entry.HeartLevel / max : 0f), config, rightAligned: true);
    }

    // Mirrors SocialPage.drawNPCSlot/drawFarmerSlot: datable NPCs (or roommates)
    // show Single/Dating/Married/etc, farmers always show a status, others stay blank.
    private static string GetRelationship(SocialPage.SocialEntry entry)
    {
        try
        {
            if (entry.IsPlayer)
                return GetFarmerRelationship(entry);
            if (!entry.IsDatable && !entry.IsRoommateForCurrentPlayer())
                return "";
            bool dating = entry.IsDatingCurrentPlayer();
            bool married = entry.IsMarriedToCurrentPlayer();
            bool roommate = entry.IsRoommateForCurrentPlayer();
            if (roommate)
                return Load(entry.Gender == Gender.Female
                    ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_Housemate_Female"
                    : "Strings\\StringsFromCSFiles:SocialPage_Relationship_Housemate_Male");
            if (married)
                return Load(entry.Gender == Gender.Female
                    ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_Wife"
                    : "Strings\\StringsFromCSFiles:SocialPage_Relationship_Husband");
            if (entry.IsMarriedToAnyone())
                return Load(entry.Gender == Gender.Female
                    ? "Strings\\UI:SocialPage_Relationship_MarriedToOtherPlayer_FemaleNpc"
                    : "Strings\\UI:SocialPage_Relationship_MarriedToOtherPlayer_MaleNpc");
            if (!Game1.player.isMarriedOrRoommates() && dating)
                return Load(entry.Gender == Gender.Female
                    ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_Girlfriend"
                    : "Strings\\StringsFromCSFiles:SocialPage_Relationship_Boyfriend");
            if (entry.IsDivorcedFromCurrentPlayer())
                return Load(entry.Gender == Gender.Female
                    ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_ExWife"
                    : "Strings\\StringsFromCSFiles:SocialPage_Relationship_ExHusband");
            return Load(entry.Gender == Gender.Female
                ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_Single_Female"
                : "Strings\\StringsFromCSFiles:SocialPage_Relationship_Single_Male");
        }
        catch
        {
            return "";
        }
    }

    private static string GetFarmerRelationship(SocialPage.SocialEntry entry)
    {
        if (entry.Character is not Farmer farmer)
            return "";
        Gender gender = entry.Gender;
        if (entry.IsMarriedToCurrentPlayer())
            return Load(gender == Gender.Male
                ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_Husband"
                : "Strings\\StringsFromCSFiles:SocialPage_Relationship_Wife");
        if (farmer.isMarriedOrRoommates() && !farmer.hasRoommate())
            return Load(gender == Gender.Male
                ? "Strings\\UI:SocialPage_Relationship_MarriedToOtherPlayer_MaleNpc"
                : "Strings\\UI:SocialPage_Relationship_MarriedToOtherPlayer_FemaleNpc");
        if (!Game1.player.isMarriedOrRoommates() && entry.IsDatingCurrentPlayer())
            return Load(gender == Gender.Male
                ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_Boyfriend"
                : "Strings\\StringsFromCSFiles:SocialPage_Relationship_Girlfriend");
        if (entry.IsDivorcedFromCurrentPlayer())
            return Load(gender == Gender.Male
                ? "Strings\\StringsFromCSFiles:SocialPage_Relationship_ExHusband"
                : "Strings\\StringsFromCSFiles:SocialPage_Relationship_ExWife");
        string single = Load("Strings\\StringsFromCSFiles:SocialPage_Relationship_Single_Female");
        if (Game1.content.ShouldUseGenderedCharacterTranslations())
            return gender == Gender.Male ? single.Split('/')[0] : single.Split('/').Last();
        return single;
    }

    private static string Load(string key)
    {
        try { return Game1.content.LoadString(key); }
        catch { return ""; }
    }

    // Mirrors drawNPCSlot's gift/talk icons: hidden for farmers, children and spouses.
    private static bool TracksDaily(SocialPage.SocialEntry entry)
    {
        try { return !entry.IsPlayer && !entry.IsChild && !entry.IsMarriedToCurrentPlayer(); }
        catch { return false; }
    }

    private static int? GetGiftsSort(SocialPage.SocialEntry entry)
        => TracksDaily(entry) ? entry.Friendship?.GiftsThisWeek ?? 0 : null;

    private static string GetGiftsText(SocialPage.SocialEntry entry)
        => TracksDaily(entry) ? $"{entry.Friendship?.GiftsThisWeek ?? 0}/2" : "";

    private static int? GetTalkedSort(SocialPage.SocialEntry entry)
    {
        if (!TracksDaily(entry))
            return null;
        return entry.Friendship?.TalkedToToday == true ? 1 : 0;
    }

    private static string GetTalkedText(SocialPage.SocialEntry entry)
    {
        if (!TracksDaily(entry))
            return "";
        return entry.Friendship?.TalkedToToday == true ? "Yes" : "No";
    }

    private static readonly Color GiftsPartialColor = new(205, 190, 110);

    private static void DrawGiftsCell(SpriteBatch batch, SocialPage.SocialEntry entry, Rectangle bounds, ModConfig config)
    {
        if (!TracksDaily(entry))
            return;
        int gifts = entry.Friendship?.GiftsThisWeek ?? 0;
        Color color = gifts >= 2 ? VitalsRenderer.FullColor : gifts == 1 ? GiftsPartialColor : VitalsRenderer.EmptyColor;
        UiText.DrawCell(batch, GetGiftsText(entry), bounds, color, config, rightAligned: true);
    }

    private static void DrawTalkedCell(SpriteBatch batch, SocialPage.SocialEntry entry, Rectangle bounds, ModConfig config)
    {
        if (!TracksDaily(entry))
            return;
        bool talked = entry.Friendship?.TalkedToToday == true;
        UiText.DrawCell(batch, GetTalkedText(entry), bounds,
            talked ? VitalsRenderer.FullColor : VitalsRenderer.EmptyColor, config);
    }

    private static void DrawNameCell(SpriteBatch batch, SocialPage.SocialEntry entry, Rectangle bounds, ModConfig config)
    {
        if (TryGetPortrait(entry, out Texture2D? texture, out Rectangle source)
            && texture != null && source.Width > 0 && source.Height > 0)
        {
            float scale = Math.Min((float)IconSize / source.Width, (float)IconSize / source.Height);
            if (scale >= 1)
                scale = MathF.Floor(scale);
            int width = Math.Max(1, (int)(source.Width * scale));
            int height = Math.Max(1, (int)(source.Height * scale));
            Rectangle destination = new(bounds.X + Padding + (IconSize - width) / 2,
                bounds.Y + (bounds.Height - height) / 2, width, height);
            batch.Draw(texture, destination, source, Color.White * config.Opacity);
        }
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, entry.DisplayName, bounds, UiStyle.Primary, config);
    }

    // Mirrors SocialPage.CreateSpriteComponent's mugshot so grid icons match the vanilla tab.
    private static bool TryGetPortrait(SocialPage.SocialEntry entry, out Texture2D? texture, out Rectangle source)
    {
        texture = null;
        source = Rectangle.Empty;
        try
        {
            if (entry.IsPlayer)
                return false;
            if (entry.Character is NPC npc && npc.Sprite?.Texture != null)
            {
                texture = npc.Sprite.Texture;
                source = npc.getMugShotSourceRect();
                return source.Width > 0 && source.Height > 0;
            }
        }
        catch
        {
            texture = null;
            source = Rectangle.Empty;
        }
        return false;
    }

    // Mirrors SocialPage.FindSocialCharacters without mutating friendship data:
    // farmers first, then NPCs by points/order-met/name, then children by name.
    private static List<SocialPage.SocialEntry> GetEntries()
    {
        var empty = new List<SocialPage.SocialEntry>();
        if (!Context.IsWorldReady || Game1.player == null)
            return empty;
        try
        {
            var farmers = new List<SocialPage.SocialEntry>();
            var villagers = new Dictionary<string, SocialPage.SocialEntry>();
            var children = new List<SocialPage.SocialEntry>();
            foreach (NPC npc in GetAllNpcs())
            {
                try
                {
                    Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship);
                    if (npc is Child)
                    {
                        children.Add(new SocialPage.SocialEntry(npc, friendship, null, npc.displayName));
                        continue;
                    }
                    if (!npc.CanSocialize)
                        continue;
                    CharacterData? data = npc.GetData();
                    string displayName = npc.displayName;
                    switch (data?.SocialTab)
                    {
                        case SocialTabBehavior.HiddenUntilMet:
                            if (friendship == null)
                                continue;
                            break;
                        case SocialTabBehavior.UnknownUntilMet:
                            if (friendship == null)
                                displayName = "???";
                            break;
                        case SocialTabBehavior.AlwaysShown:
                            friendship ??= new Friendship();
                            break;
                        case SocialTabBehavior.HiddenAlways:
                            continue;
                    }
                    villagers[npc.Name] = new SocialPage.SocialEntry(npc, friendship, data, displayName);
                }
                catch { }
            }
            int order = 0;
            try
            {
                foreach (var pair in Game1.player.friendshipData.Pairs)
                {
                    if (villagers.TryGetValue(pair.Key, out SocialPage.SocialEntry? entry))
                        entry.OrderMet = order++;
                }
            }
            catch { }
            try
            {
                foreach (Farmer farmer in Game1.getAllFarmers())
                {
                    if (!farmer.IsLocalPlayer && (farmer.IsMainPlayer || farmer.isCustomized.Value) && !farmer.IsDedicatedPlayer)
                    {
                        Friendship? friendship = Game1.player.team.GetFriendship(Game1.player.UniqueMultiplayerID, farmer.UniqueMultiplayerID);
                        farmers.Add(new SocialPage.SocialEntry(farmer, friendship));
                    }
                }
            }
            catch { }
            var ordered = new List<SocialPage.SocialEntry>();
            ordered.AddRange(farmers);
            try
            {
                ordered.AddRange(from entry in villagers.Values
                    orderby entry.Friendship?.Points descending, entry.OrderMet, entry.DisplayName
                    select entry);
            }
            catch
            {
                ordered.AddRange(villagers.Values);
            }
            try { ordered.AddRange(children.OrderBy(child => child.DisplayName)); }
            catch { ordered.AddRange(children); }
            return ordered;
        }
        catch
        {
            return empty;
        }
    }

    // Mirrors SocialPage.GetAllNpcs: world/event villagers plus friendship-data
    // fallbacks whose sprites exist, deduplicated by name.
    private static IEnumerable<NPC> GetAllNpcs()
    {
        var found = new Dictionary<string, NPC>();
        var nonSocial = new HashSet<string>();
        try
        {
            Utility.ForEachCharacter(npc =>
            {
                try
                {
                    if (npc is Child)
                        found[npc.Name + "$$child"] = npc;
                    else if (npc.IsVillager)
                    {
                        if (!npc.CanSocialize)
                            nonSocial.Add(npc.Name);
                        else if (!found.ContainsKey(npc.Name))
                            found[npc.Name] = npc;
                    }
                }
                catch { }
                return true;
            });
        }
        catch { }
        try
        {
            var currentEvent = Game1.currentLocation?.currentEvent;
            if (currentEvent != null)
            {
                foreach (NPC actor in currentEvent.actors)
                {
                    try
                    {
                        if (actor.IsVillager && actor.CanSocialize)
                            found[actor.Name] = actor;
                    }
                    catch { }
                }
            }
        }
        catch { }
        try
        {
            foreach (string key in Game1.player.friendshipData.Keys)
            {
                try
                {
                    if (nonSocial.Contains(key) || found.ContainsKey(key) || !NPC.TryGetData(key, out _))
                        continue;
                    string textureName = NPC.getTextureNameForCharacter(key);
                    string spriteAsset = "Characters\\" + textureName;
                    string portraitAsset = "Portraits\\" + textureName;
                    if (Game1.content.DoesAssetExist<Texture2D>(spriteAsset) && Game1.content.DoesAssetExist<Texture2D>(portraitAsset))
                    {
                        var sprite = new AnimatedSprite(spriteAsset, 0, 16, 32);
                        Texture2D portrait = Game1.content.Load<Texture2D>(portraitAsset);
                        found[key] = new NPC(sprite, Vector2.Zero, "Town", 0, key, portrait, eventActor: false);
                    }
                }
                catch { }
            }
        }
        catch { }
        return found.Values;
    }
}
