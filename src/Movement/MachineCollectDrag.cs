using Microsoft.Xna.Framework;
using StardewValley;

namespace AgrariaOS.Movement;

/// <summary>A per-screen target stroke; each crossed tile is sampled once per hold.</summary>
internal sealed class MachineCollectDrag
{
    private readonly HashSet<Point> visited = new();
    private Vector2 previous;
    private GameLocation? location;
    private Farmer? farmer;
    private uint day;

    internal bool IsActive => location != null;

    internal void Begin(GameLocation here, Farmer who, uint today, Vector2 pixel)
    {
        Reset();
        location = here;
        farmer = who;
        day = today;
        previous = pixel / 64f;
    }

    internal bool Matches(GameLocation here, Farmer who, uint today)
        => location == here && farmer == who && day == today;

    internal IEnumerable<Point> Sample(Vector2 pixel)
    {
        Vector2 from = previous;
        Vector2 to = pixel / 64f;
        previous = to;
        Point tile = new((int)MathF.Floor(from.X), (int)MathF.Floor(from.Y));
        Point end = new((int)MathF.Floor(to.X), (int)MathF.Floor(to.Y));
        Vector2 delta = to - from;
        int sx = Math.Sign(delta.X), sy = Math.Sign(delta.Y);
        float dx = sx == 0 ? float.PositiveInfinity : 1f / Math.Abs(delta.X);
        float dy = sy == 0 ? float.PositiveInfinity : 1f / Math.Abs(delta.Y);
        float tx = sx == 0 ? float.PositiveInfinity : (sx > 0 ? tile.X + 1 - from.X : from.X - tile.X) * dx;
        float ty = sy == 0 ? float.PositiveInfinity : (sy > 0 ? tile.Y + 1 - from.Y : from.Y - tile.Y) * dy;
        // Traverse the actual cursor segment so fast strokes don't skip targets.
        while (true)
        {
            if (visited.Add(tile))
                yield return tile;
            if (tile == end)
                break;
            if (tile.Y == end.Y || (tile.X != end.X && tx < ty))
            {
                tile.X += sx;
                tx += dx;
            }
            else if (tile.X == end.X || ty < tx)
            {
                tile.Y += sy;
                ty += dy;
            }
            else
            {
                tile.X += sx;
                tile.Y += sy;
                tx += dx;
                ty += dy;
            }
        }
    }

    internal void Reset()
    {
        location = null;
        farmer = null;
        visited.Clear();
    }
}
