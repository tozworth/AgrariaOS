using System.Globalization;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Wallet;

// Session journal of gold flowing in and out of each farmer's pockets.
// Rich entries come from the trade and shipping hooks; a per-tick balance
// poll catches everything else, with money-change hints attributing the
// source (shop, quest, transit, construction, ...).
internal sealed class WalletLedger
{
    internal const int MaxEntries = 200;
    internal const string KindTrade = "Trade";
    internal const string KindShipping = "Shipping";
    internal const string KindQuest = "Quest";
    internal const string KindTrash = "Trash";
    internal const string KindOther = "Other";

    // Static trade helpers can't take the ledger through constructors.
    internal static WalletLedger? Active { get; set; }

    internal sealed class Entry
    {
        internal long Order;
        internal string Season = "";
        internal int Day;
        internal int Year;
        internal int Time;
        internal string Kind = KindOther;
        internal string Description = "";
        internal int Amount;
        internal int BalanceAfter;
    }

    private sealed class Hint
    {
        internal long FarmerId;
        internal int Delta;
        internal string Text = "";
        internal string Kind = KindOther;
        internal DateTime Seen;
    }

    private readonly Dictionary<long, List<Entry>> entries = new();
    private readonly Dictionary<long, int> baselines = new();
    private readonly List<Hint> hints = new();
    private static readonly TimeSpan HintLifetime = TimeSpan.FromSeconds(5);

    internal void ClearAll()
    {
        entries.Clear();
        baselines.Clear();
        hints.Clear();
    }

    internal Entry[] GetEntries(long farmerId)
        => entries.TryGetValue(farmerId, out List<Entry>? list) ? list.ToArray() : Array.Empty<Entry>();

    // Explicit rich record after gold moved (trade). Resets the poll baseline
    // so the same delta isn't recorded again as a generic entry, and drops any
    // money hint the same change already produced.
    internal void Record(long farmerId, int amount, string description, string kind, int balanceAfter)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;
        Insert(farmerId, amount, description, kind, balanceAfter);
        baselines[farmerId] = balanceAfter;
        hints.RemoveAll(hint => hint.FarmerId == farmerId && hint.Delta == amount);
    }

    // Shipment logging at bin time; the gold itself arrives overnight and shows
    // up as its own payout entry later. Leaves the poll baseline alone.
    internal void RecordShipment(long farmerId, string itemName, int qty, int expectedValue, int balanceAfter)
    {
        if (qty <= 0 || string.IsNullOrWhiteSpace(itemName))
            return;
        Insert(farmerId, expectedValue, $"Shipped {qty}x {itemName}", KindShipping, balanceAfter);
    }

    internal void NoteChange(long farmerId, int delta, string text, string kind)
    {
        if (delta == 0 || string.IsNullOrWhiteSpace(text))
            return;
        PruneHints();
        hints.Add(new Hint { FarmerId = farmerId, Delta = delta, Text = text, Kind = kind, Seen = DateTime.UtcNow });
        if (hints.Count > 40)
            hints.RemoveRange(0, hints.Count - 40);
    }

    internal void Sync()
    {
        if (!Context.IsWorldReady)
            return;
        Farmer farmer;
        try { farmer = Game1.player; }
        catch { return; }
        long farmerId;
        int now;
        try
        {
            farmerId = farmer.UniqueMultiplayerID;
            now = farmer.Money;
        }
        catch { return; }
        if (!baselines.TryGetValue(farmerId, out int last))
        {
            // First sighting (e.g. after loading a save): adopt the balance
            // instead of journaling the whole fortune as income.
            baselines[farmerId] = now;
            PruneHints();
            return;
        }
        int delta = now - last;
        if (delta == 0)
        {
            PruneHints();
            return;
        }
        baselines[farmerId] = now;
        Hint? hint = hints.FirstOrDefault(candidate => candidate.FarmerId == farmerId && candidate.Delta == delta);
        if (hint != null)
            hints.Remove(hint);
        string description = hint?.Text
            ?? (delta > 0 ? $"Earned {Math.Abs(delta).ToString("N0", CultureInfo.InvariantCulture)} g"
                : $"Spent {Math.Abs(delta).ToString("N0", CultureInfo.InvariantCulture)} g");
        Insert(farmerId, delta, description, hint?.Kind ?? KindOther, now);
        PruneHints();
    }

    private void Insert(long farmerId, int amount, string description, string kind, int balanceAfter)
    {
        if (!entries.TryGetValue(farmerId, out List<Entry>? list))
            entries[farmerId] = list = new List<Entry>();
        list.Insert(0, Capture(amount, description, kind, balanceAfter));
        while (list.Count > MaxEntries)
            list.RemoveAt(list.Count - 1);
    }

    private static Entry Capture(int amount, string description, string kind, int balanceAfter)
    {
        string season = "";
        int day = 0, year = 0, time = 0;
        long order = 0;
        try
        {
            season = Game1.currentSeason ?? "";
            day = Game1.dayOfMonth;
            year = Game1.year;
            time = Game1.timeOfDay;
            order = ((long)Game1.stats.DaysPlayed << 16) + (uint)Math.Max(0, time);
        }
        catch { }
        return new Entry
        {
            Order = order,
            Season = season,
            Day = day,
            Year = year,
            Time = time,
            Kind = kind,
            Description = description,
            Amount = amount,
            BalanceAfter = balanceAfter
        };
    }

    private void PruneHints()
    {
        DateTime cutoff = DateTime.UtcNow - HintLifetime;
        hints.RemoveAll(hint => hint.Seen < cutoff);
    }

    internal static (string Text, string Kind) DescribeCaller(string typeName, string methodName, int delta)
    {
        string haystack = $"{typeName}.{methodName}";
        if (ContainsAny(haystack, "newday", "shipping", "overnight"))
            return ("Shipping payout", KindShipping);
        if (ContainsAny(haystack, "shop"))
            return ("Shop transaction", KindTrade);
        if (ContainsAny(haystack, "quest"))
            return ("Quest reward", KindQuest);
        if (ContainsAny(haystack, "bus", "minecart"))
            return ("Transit fare", KindOther);
        if (ContainsAny(haystack, "carpenter", "robin", "construction"))
            return ("Construction", KindOther);
        if (ContainsAny(haystack, "purchaseanimals", "marnie", "livestock"))
            return ("Livestock purchase", KindOther);
        if (ContainsAny(haystack, "clint", "blacksmith", "toolupgrade", "upgradetool"))
            return ("Tool upgrade", KindOther);
        if (ContainsAny(haystack, "junimo", "bundle", "vault", "communitycenter"))
            return ("Bundle donation", KindOther);
        if (ContainsAny(haystack, "museum"))
            return ("Museum reward", KindOther);
        if (ContainsAny(haystack, "trash"))
            return ("Trash reclamation", KindTrash);
        string shortType = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
        int plus = shortType.IndexOf('+');
        if (plus > 0)
            shortType = shortType[..plus];
        if (string.IsNullOrWhiteSpace(shortType))
            return (delta > 0 ? "Gold earned" : "Gold spent", KindOther);
        return ($"Gold {(delta > 0 ? "earned" : "spent")} ({shortType})", KindOther);
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
