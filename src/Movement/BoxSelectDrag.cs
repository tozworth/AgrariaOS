using Microsoft.Xna.Framework;
using StardewValley;

namespace AgrariaOS.Movement;

/// <summary>A world-space rectangle, committed only on release. No swept-area selection.</summary>
internal sealed class BoxSelectDrag
{
    private Vector2 anchor;
    private Vector2 endpoint;
    private GameLocation? location;
    private Farmer? farmer;
    private uint day;

    internal bool IsActive => location != null;
    internal bool HasDragged { get; private set; }
    internal Point AnchorTile => new((int)MathF.Floor(anchor.X / 64), (int)MathF.Floor(anchor.Y / 64));
    internal Rectangle Bounds
    {
        get
        {
            int x = (int)MathF.Floor(Math.Min(anchor.X, endpoint.X));
            int y = (int)MathF.Floor(Math.Min(anchor.Y, endpoint.Y));
            int right = (int)MathF.Floor(Math.Max(anchor.X, endpoint.X)) + 1;
            int bottom = (int)MathF.Floor(Math.Max(anchor.Y, endpoint.Y)) + 1;
            return new Rectangle(x, y, right - x, bottom - y);
        }
    }

    internal void Begin(GameLocation here, Farmer who, uint today, Vector2 pixel)
    {
        Reset();
        location = here;
        farmer = who;
        day = today;
        anchor = endpoint = pixel;
    }

    internal bool Matches(GameLocation here, Farmer who, uint today)
        => location == here && farmer == who && day == today;

    internal void Update(Vector2 pixel)
    {
        endpoint = pixel;
        HasDragged |= Vector2.DistanceSquared(anchor, endpoint) >= 8 * 8;
    }

    // Tiles intersecting the final box. Clamp before scanning so off-map drags
    // can't create unbounded loops; include the tile under each corner.
    internal IEnumerable<Point> Tiles(Rectangle mapTiles)
    {
        Rectangle box = Bounds;
        int left = Math.Max(mapTiles.Left, (int)MathF.Floor(box.Left / 64f));
        int top = Math.Max(mapTiles.Top, (int)MathF.Floor(box.Top / 64f));
        int right = Math.Min(mapTiles.Right - 1, (int)MathF.Floor((box.Right - 1) / 64f));
        int bottom = Math.Min(mapTiles.Bottom - 1, (int)MathF.Floor((box.Bottom - 1) / 64f));
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                yield return new Point(x, y);
    }

    internal IReadOnlyList<InteractRequest> ResolveTargets(Rectangle mapTiles, Func<Point, InteractRequest?> probe)
    {
        var requests = new List<InteractRequest>();
        var identities = new Dictionary<object, HashSet<InteractionKind>>(ReferenceEqualityComparer.Instance);
        var footprints = new HashSet<(Rectangle, InteractionKind)>();
        var points = new HashSet<(Point, InteractionKind)>();
        foreach (Point tile in Tiles(mapTiles))
        {
            InteractRequest? request = probe(tile);
            if (request == null || !request.Valid())
                continue;
            if (request.Identity is { } identity)
            {
                if (!identities.TryGetValue(identity, out var kinds))
                    identities[identity] = kinds = new();
                if (!kinds.Add(request.Kind))
                    continue;
            }
            else if (request.Move is { } move)
            {
                if (!footprints.Add((move.Tiles, request.Kind)))
                    continue;
            }
            else if (!points.Add((request.Tile(), request.Kind)))
                continue;
            requests.Add(request);
        }
        return requests;
    }

    internal void Reset()
    {
        location = null;
        farmer = null;
        HasDragged = false;
        anchor = endpoint = Vector2.Zero;
    }
}
