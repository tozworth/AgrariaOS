using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.SaveSerialization;

namespace AgrariaOS.Mail;

internal sealed class MailArchive
{
    private const string DataKey = "Toz.AgrariaOS/MailArchive";
    internal static MailArchive? Active { get; set; }
    private readonly Dictionary<Farmer, List<MailRecord>> records = new();
    private readonly Dictionary<Farmer, (int Received, string Queue)> snapshots = new();
    private readonly Action<Exception> onError;
    internal bool OpeningDesktop { get; private set; }
    private MailRecord? desktopLetter;

    internal MailArchive(Action<Exception> onError) => this.onError = onError;
    internal void Clear() { records.Clear(); snapshots.Clear(); }
    internal IReadOnlyList<MailRecord> Letters => Context.IsWorldReady ? Load(Game1.player) : Array.Empty<MailRecord>();
    internal int UnreadCount => Letters.Count(letter => letter.Pending);

    private List<MailRecord> Load(Farmer farmer)
    {
        if (records.TryGetValue(farmer, out var list)) return list;
        // Let malformed data fail visibly; don't overwrite an archive containing rewards.
        list = farmer.modData.TryGetValue(DataKey, out string? json)
            ? JsonSerializer.Deserialize<List<MailRecord>>(json) ?? new() : new();
        records.Add(farmer, list);
        return list;
    }

    private void Save() => Game1.player.modData[DataKey] = JsonSerializer.Serialize(Load(Game1.player));

    internal void Sync()
    {
        if (!Context.IsWorldReady) return;
        List<MailRecord> list = Load(Game1.player);
        var snapshot = (Game1.player.mailReceived.Count, string.Join("\n", Game1.mailbox));
        if (snapshots.TryGetValue(Game1.player, out var previous) && previous == snapshot) return;
        Dictionary<string, string> data = DataLoader.Mail(Game1.content);
        bool changed = false;
        foreach (string id in Game1.player.mailReceived)
        {
            if (!data.TryGetValue(id, out string? raw) || list.Any(letter => letter.MailId == id)) continue;
            list.Add(Create(id, raw, historical: true));
            changed = true;
        }
        var remaining = list.Where(letter => letter.Pending).ToList();
        foreach (string id in Game1.mailbox)
        {
            MailRecord? existing = remaining.FirstOrDefault(letter => letter.MailId == id);
            if (existing != null) { remaining.Remove(existing); continue; }
            string raw = MailText.Resolve(id, data);
            if (raw.Length == 0) continue;
            MailRecord letter = Create(id, raw);
            letter.Pending = true;
            list.Add(letter);
            changed = true;
        }
        // Another mod may consume/remove mail without constructing a vanilla viewer.
        foreach (MailRecord removed in remaining) { removed.Pending = false; changed = true; }
        if (changed) Save();
        snapshots[Game1.player] = snapshot;
    }

    private static MailRecord Create(string id, string raw, bool historical = false) => new()
    {
        MailId = id, Title = MailText.Title(id, raw), Body = MailText.Body(raw), Historical = historical,
        HasItemCommand = MailText.HasItems(raw),
        Date = historical ? "" : $"{Game1.currentSeason} {Game1.dayOfMonth}, Y{Game1.year}",
        DateOrder = historical ? 0 : (Game1.year - 1) * 112 + (int)Game1.season * 28 + Game1.dayOfMonth,
        Updates = !historical && (id.StartsWith("passedOut", StringComparison.Ordinal)
            || id.Contains("Cooking", StringComparison.Ordinal) || DataLoader.Characters(Game1.content).ContainsKey(id)
            || raw.Contains("%item itemrecovery", StringComparison.Ordinal) || raw.Contains("%secretsanta", StringComparison.Ordinal)
            || !DataLoader.Mail(Game1.content).ContainsKey(id))
    };

    // Called after the live constructor; the selected random item and resolved
    // text are now known. Collection viewers never create new deliveries.
    internal MailRecord Capture(LetterViewerMenu viewer, string raw)
    {
        List<MailRecord> list = Load(Game1.player);
        // Finish serializing every reward before changing the record. On failure
        // the desktop keeps the live vanilla viewer and its original item objects.
        var attachments = viewer.itemsToGrab.Where(slot => slot.item != null).Select(slot =>
        {
            Item item = slot.item;
            var attachment = new MailAttachment { ItemId = item.QualifiedItemId, Name = item.DisplayName, Quantity = item.Stack };
            if (OpeningDesktop)
            {
                using var writer = new StringWriter();
                SaveSerializer.GetSerializer(typeof(Item)).Serialize(writer, item);
                attachment.ItemXml = writer.ToString();
                attachment.Remaining = item.Stack;
            }
            return attachment;
        }).ToList();
        MailRecord? letter = desktopLetter ?? list.FirstOrDefault(candidate => candidate.Pending && candidate.MailId == viewer.mailTitle);
        if (letter == null) { letter = Create(viewer.mailTitle, raw); list.Add(letter); }
        letter.Title = MailText.Title(viewer.mailTitle, raw);
        letter.Body = string.Join("^", viewer.mailMessage);
        letter.Attachments = attachments;
        if (viewer.moneyIncluded > 0) letter.Body += $"^Enclosed: {viewer.moneyIncluded:N0} g";
        if (!string.IsNullOrEmpty(viewer.learnedRecipe)) letter.Body += $"^Learned: {viewer.learnedRecipe}";
        letter.Pending = false;
        Save();
        return letter;
    }

    internal void Open(MailRecord letter)
    {
        if (!letter.Pending || !Context.IsWorldReady || Game1.activeClickableMenu != null) return;
        int index = Game1.mailbox.IndexOf(letter.MailId);
        if (index < 0) { Sync(); return; }
        // Use the actual mailbox path, including fee formatting and other mods'
        // patches. Put the chosen delivery first without disturbing the rest.
        if (index > 0) { Game1.mailbox.RemoveAt(index); Game1.mailbox.Insert(0, letter.MailId); }
        OpeningDesktop = true;
        desktopLetter = letter;
        try
        {
            Game1.currentLocation.mailbox();
            if (Game1.activeClickableMenu is LetterViewerMenu viewer)
            {
                // Capture after mailbox patches finish, so the final viewer's item
                // objects (including modded gifts) are the ones we retain.
                string raw = MailText.Resolve(letter.MailId, DataLoader.Mail(Game1.content));
                if (viewer.HasQuestOrSpecialOrder) viewer.AcceptQuest();
                Capture(viewer, raw);
                viewer.itemsToGrab.Clear();
                Game1.activeClickableMenu = null;
            }
        }
        catch (Exception ex) { onError(ex); }
        finally { OpeningDesktop = false; desktopLetter = null; }
    }

    internal void Claim(MailAttachment attachment)
    {
        if (attachment.Remaining <= 0 || attachment.ItemXml.Length == 0) return;
        try
        {
            using var reader = new StringReader(attachment.ItemXml);
            Item item = (Item)SaveSerializer.GetSerializer(typeof(Item)).Deserialize(reader)!;
            item.Stack = attachment.Remaining;
            Item? remainder = Game1.player.addItemToInventory(item);
            attachment.Remaining = remainder?.Stack ?? 0;
            if (remainder != null)
            {
                // The persisted instance retains quality, tool upgrades, modData,
                // etc. Stack is restored from Remaining on each claim attempt.
                Game1.showRedMessage("Backpack full. Remaining items stay attached.");
            }
            else attachment.ItemXml = "";
            Save();
        }
        catch (Exception ex) { onError(ex); }
    }
}
