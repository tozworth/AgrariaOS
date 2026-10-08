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

    private static bool IsSummaryTarget(SocialPage.SocialEntry? row)
    {
        try { return row != null && !row.IsPlayer && !row.IsChild && row.Friendship != null; }
        catch { return false; }
    }

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

}
