using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Mining;

// Background tally of ore the local farmers pick up from the world each day.
// Debris pickups (mined stones, monster drops, magnet collection) are the mining
// signal: chest shuffling and shop purchases never touch that path, so moving
// stacks between the backpack and chests can't inflate the ledger.
internal sealed class MiningLedger
{
    // One bucket per in-game hour, 6am through the 1-2am bucket.
    internal const int BucketCount = 20;

    internal static readonly OreDefinition[] Ores =
    {
        new("(O)378", 378, "Copper Ore", new Color(216, 138, 80)),
        new("(O)380", 380, "Iron Ore", new Color(190, 195, 205)),
        new("(O)384", 384, "Gold Ore", new Color(232, 209, 128)),
        new("(O)386", 386, "Iridium Ore", new Color(196, 169, 255)),
        new("(O)382", 382, "Coal", new Color(115, 115, 120)),
    };

    private readonly Dictionary<long, FarmerDay> days = new();

    internal static int IndexOf(string? qualifiedId)
    {
        for (int i = 0; i < Ores.Length; i++)
        {
            if (string.Equals(Ores[i].QualifiedId, qualifiedId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    // Game1.timeOfDay runs 600 (6am) to 2600 (2am) in 10-minute steps.
    internal static int BucketFor(int timeOfDay)
    {
        int hour = timeOfDay / 100;
        if (hour < 6)
            return 0;
        if (hour >= 26)
            return BucketCount - 1;
        if (hour >= 24)
            return hour - 24 + 18;
        return hour - 6;
    }

    internal void Record(long farmerId, string? qualifiedId, int qty)
    {
        int ore = IndexOf(qualifiedId);
        if (ore < 0 || qty <= 0 || !Context.IsWorldReady)
            return;
        FarmerDay day = GetDay(farmerId);
        int bucket = BucketFor(Game1.timeOfDay);
        day.Buckets[ore][bucket] += qty;
        day.Totals[ore] += qty;
    }

    internal FarmerDay GetDay(long farmerId)
    {
        uint today = Context.IsWorldReady ? Game1.stats.DaysPlayed : 0;
        if (!days.TryGetValue(farmerId, out FarmerDay? day) || day.DaysPlayed != today)
        {
            day = new FarmerDay(today);
            days[farmerId] = day;
        }
        return day;
    }

    internal void ClearAll() => days.Clear();

    internal sealed record OreDefinition(string QualifiedId, int SpriteIndex, string FallbackName, Color Color);

    internal sealed class FarmerDay
    {
        internal uint DaysPlayed { get; }
        internal int[][] Buckets { get; }
        internal int[] Totals { get; }
        internal int Total => Totals.Sum();

        internal FarmerDay(uint daysPlayed)
        {
            DaysPlayed = daysPlayed;
            Buckets = new int[Ores.Length][];
            for (int i = 0; i < Buckets.Length; i++)
                Buckets[i] = new int[BucketCount];
            Totals = new int[Ores.Length];
        }
    }
}
