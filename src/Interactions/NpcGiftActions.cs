using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;

namespace AgrariaOS.Interactions;

internal static class NpcGiftActions
{
    internal sealed record OwnedGift(StardewValley.Object Item, int Taste);

    internal static OwnedGift[] GetOwnedGifts(NPC npc, Farmer farmer)
    {
        if (!npc.CanReceiveGifts())
            return Array.Empty<OwnedGift>();
        return farmer.Items.OfType<StardewValley.Object>().Where(IsGiftItem)
            .Select(item => new OwnedGift(item, npc.getGiftTasteForThisItem(item)))
            // Vanilla treats tea as a special positive gift outside the daily/weekly limits.
            .Select(gift => gift.Taste == NPC.gift_taste_stardroptea ? gift with { Taste = NPC.gift_taste_love } : gift)
            .Where(gift => gift.Taste is NPC.gift_taste_love or NPC.gift_taste_like)
            .OrderBy(gift => gift.Item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(gift => gift.Item.Quality).ToArray();
    }

    private static bool IsGiftItem(StardewValley.Object item)
        => item.Stack > 0 && !item.questItem.Value && item.canBeGivenAsGift()
            && !ItemContextTagManager.HasBaseTag(item.QualifiedItemId, "not_giftable");

    internal static bool CanGive(NPC npc, Farmer farmer, StardewValley.Object gift)
    {
        if (!npc.CanReceiveGifts() || npc.IsInvisible || npc.isSleeping.Value || farmer.TemporaryItem != null
            || !farmer.Items.Any(item => ReferenceEquals(item, gift)) || !IsGiftItem(gift))
            return false;
        int taste = npc.getGiftTasteForThisItem(gift);
        if (taste is not (NPC.gift_taste_love or NPC.gift_taste_like or NPC.gift_taste_stardroptea))
            return false;
        farmer.friendshipData.TryGetValue(npc.Name, out var friendship);
        if (friendship?.IsDivorced() == true)
            return false;
        if (gift.QualifiedItemId != "(O)StardropTea"
            && ((friendship?.GiftsToday ?? 0) >= 1
                || ((friendship?.GiftsThisWeek ?? 0) >= NPC.maxGiftsPerWeek
                    && farmer.spouse != npc.Name && npc is not Child && !npc.isBirthday())))
            return false;
        // These are native refusal branches before tryToReceiveActiveObject reaches receiveGift.
        if (Game1.IsGreenRainingHere() && Game1.year == 1 && !npc.isMarried())
            return false;
        if (farmer.activeDialogueEvents.Keys.Any(key => key.Contains("dumped") && npc.Dialogue.ContainsKey(key)))
            return false;
        return npc.TryGetDialogue("RejectItem_" + gift.QualifiedItemId) == null
            && !gift.GetContextTags().Any(tag => npc.TryGetDialogue("RejectItem_" + tag) != null)
            && (!gift.HasTypeObject() || npc.TryGetDialogue("reject_" + gift.ItemId) == null);
    }

    internal static string GetLabel(StardewValley.Object gift)
    {
        string quality = gift.Quality switch { 1 => " (Silver)", 2 => " (Gold)", 4 => " (Iridium)", _ => "" };
        return $"{gift.DisplayName}{quality} x{gift.Stack}";
    }

    internal static void Give(NPC npc, Farmer farmer, StardewValley.Object gift)
    {
        if (!CanGive(npc, farmer, gift))
            return;
        // checkAction normally creates the initial friendship. Avoid its introduction/talk
        // branches, which can postpone the selected gift until another click.
        if (!farmer.friendshipData.ContainsKey(npc.Name))
            farmer.friendshipData[npc.Name] = new Friendship();
        // Use the exact backpack stack even when it is outside the toolbar or stowed.
        // Native gift dispatch owns consumption, dialogue, quests, quality and jealousy.
        farmer.TemporaryItem = gift;
        try { npc.tryToReceiveActiveObject(farmer); }
        finally { farmer.TemporaryItem = null; }
    }

    internal static void Talk(NPC npc, Farmer farmer, GameLocation location)
    {
        if (farmer.TemporaryItem != null)
            return;
        bool stowed = farmer.netItemStowed.Value;
        try
        {
            // ActiveObject returns null while stowed; checkAction then performs only the
            // NPC's native dialogue/shop/social action, without offering a carried gift.
            farmer.netItemStowed.Value = true;
            farmer.UpdateItemStow();
            farmer.faceGeneralDirection(npc.getStandingPosition(), 0, opposite: false, useTileCalculations: false);
            npc.checkAction(farmer, location);
        }
        finally
        {
            farmer.netItemStowed.Value = stowed;
            farmer.UpdateItemStow();
        }
    }
}
