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

partial class SocialWindow : OsWindow
{
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

}
