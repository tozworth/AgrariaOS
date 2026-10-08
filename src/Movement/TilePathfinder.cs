using Microsoft.Xna.Framework;

namespace AgrariaOS.Movement;

/// <summary>Four-way A* with full-size coordinates and a bounded amount of work per click.</summary>
internal static class TilePathfinder
{
    internal static readonly Point[] Directions = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };

    internal readonly record struct RouteStop(int TargetIndex, Point Tile);

    /// <summary>Shortest open tour through reachable target groups, including the choice
    /// of interaction side. BFS supplies obstacle-aware distances; subset DP checks every
    /// order and endpoint combination. Targets and collisions are a snapshot; live legs
    /// still validate/replan normally. No return to the start is required.</summary>
    internal static IReadOnlyList<RouteStop> FindRoute(Point start,
        IReadOnlyList<IReadOnlyCollection<Point>> targets, Func<Point, bool> canWalk, int limit = 20000)
    {
        if (targets.Count > ClickToMove.MaxQueued)
            throw new ArgumentOutOfRangeException(nameof(targets));
        Dictionary<Point, bool> walkable = new();
        bool Walk(Point tile)
        {
            if (tile == start)
                return true;
            if (!walkable.TryGetValue(tile, out bool allowed))
                walkable[tile] = allowed = canWalk(tile);
            return allowed;
        }
        var nodes = targets.SelectMany((tiles, index) => tiles.Distinct().Where(Walk)
            .Select(tile => new RouteStop(index, tile))).ToArray();
        if (nodes.Length == 0)
            return Array.Empty<RouteStop>();
        var destinations = nodes.Select(node => node.Tile).ToHashSet();
        Dictionary<Point, int> Distances(Point origin)
        {
            var missing = new HashSet<Point>(destinations);
            Dictionary<Point, int> distances = new() { [origin] = 0 };
            Queue<Point> open = new();
            open.Enqueue(origin);
            int visited = 0;
            while (open.TryDequeue(out Point tile) && visited++ < limit)
            {
                missing.Remove(tile);
                if (missing.Count == 0)
                    break;
                foreach (Point direction in Directions)
                {
                    Point next = tile + direction;
                    if (!distances.ContainsKey(next) && Walk(next))
                    {
                        distances[next] = distances[tile] + 1;
                        open.Enqueue(next);
                    }
                }
            }
            // Only endpoint distances survive each search; don't retain entire maps
            // for every possible interaction side.
            return distances.Where(pair => destinations.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        var initial = Distances(start);
        nodes = nodes.Where(node => initial.ContainsKey(node.Tile)).ToArray();
        if (nodes.Length == 0)
            return Array.Empty<RouteStop>();
        destinations = nodes.Select(node => node.Tile).ToHashSet();
        var distancesByTile = new Dictionary<Point, Dictionary<Point, int>> { [start] = initial };
        foreach (Point tile in destinations)
            if (!distancesByTile.ContainsKey(tile))
                distancesByTile[tile] = Distances(tile);

        int masks = 1 << targets.Count;
        int[,] costs = new int[masks, nodes.Length];
        int[,] parents = new int[masks, nodes.Length];
        for (int mask = 0; mask < masks; mask++)
            for (int end = 0; end < nodes.Length; end++)
            {
                costs[mask, end] = int.MaxValue;
                parents[mask, end] = -1;
            }
        for (int end = 0; end < nodes.Length; end++)
            costs[1 << nodes[end].TargetIndex, end] = initial[nodes[end].Tile];
        for (int mask = 1; mask < masks; mask++)
        {
            for (int end = 0; end < nodes.Length; end++)
            {
                if (costs[mask, end] == int.MaxValue)
                    continue;
                var distances = distancesByTile[nodes[end].Tile];
                for (int next = 0; next < nodes.Length; next++)
                {
                    int bit = 1 << nodes[next].TargetIndex;
                    if ((mask & bit) != 0 || !distances.TryGetValue(nodes[next].Tile, out int distance))
                        continue;
                    int nextMask = mask | bit;
                    int cost = costs[mask, end] + distance;
                    if (cost < costs[nextMask, next])
                    {
                        costs[nextMask, next] = cost;
                        parents[nextMask, next] = end;
                    }
                }
            }
        }
        // Unreachable groups are omitted, rather than preventing reachable work.
        int bestMask = 0, bestEnd = -1, bestCount = 0, bestCost = int.MaxValue;
        for (int mask = 1; mask < masks; mask++)
        {
            int count = System.Numerics.BitOperations.PopCount((uint)mask);
            for (int end = 0; end < nodes.Length; end++)
            {
                int cost = costs[mask, end];
                if (cost != int.MaxValue && (count > bestCount || count == bestCount && cost < bestCost))
                {
                    bestMask = mask; bestEnd = end; bestCount = count; bestCost = cost;
                }
            }
        }
        var route = new List<RouteStop>();
        while (bestEnd >= 0)
        {
            route.Add(nodes[bestEnd]);
            int previous = parents[bestMask, bestEnd];
            bestMask &= ~(1 << nodes[bestEnd].TargetIndex);
            bestEnd = previous;
        }
        route.Reverse();
        return route;
    }

    internal static Stack<Point>? Find(Point start, IReadOnlyCollection<Point> destinations,
        Func<Point, bool> canWalk, int limit = 20000)
    {
        if (destinations.Count == 0)
            return null;
        HashSet<Point> goals = destinations.ToHashSet();
        int Estimate(Point tile) => destinations.Min(goal => Math.Abs(tile.X - goal.X) + Math.Abs(tile.Y - goal.Y));
        PriorityQueue<(Point Tile, int Cost), int> open = new();
        Dictionary<Point, int> costs = new() { [start] = 0 };
        Dictionary<Point, Point> parents = new();
        Dictionary<Point, bool> walkable = new();
        open.Enqueue((start, 0), Estimate(start));
        int visited = 0;
        while (open.TryDequeue(out var node, out _) && visited < limit)
        {
            if (costs[node.Tile] != node.Cost)
                continue;
            visited++;
            if (goals.Contains(node.Tile))
            {
                Stack<Point> path = new();
                Point tile = node.Tile;
                path.Push(tile);
                while (parents.TryGetValue(tile, out Point parent))
                {
                    path.Push(parent);
                    tile = parent;
                }
                return path;
            }
            foreach (Point direction in Directions)
            {
                Point next = node.Tile + direction;
                int cost = node.Cost + 1;
                if (costs.TryGetValue(next, out int previous) && previous <= cost)
                    continue;
                if (!walkable.TryGetValue(next, out bool allowed))
                    walkable[next] = allowed = canWalk(next);
                if (!allowed)
                    continue;
                costs[next] = cost;
                parents[next] = node.Tile;
                open.Enqueue((next, cost), cost + Estimate(next));
            }
        }
        return null;
    }

    /// <summary>Closest reachable tile to an unreachable click. Runs the same A* toward the
    /// desired tile while tracking the visited walkable tile with the smallest Manhattan
    /// distance, then returns the path there. Returns null when already as close as possible.</summary>
    internal static Stack<Point>? FindClosest(Point start, Point desired,
        Func<Point, bool> canWalk, int limit = 20000)
    {
        int Estimate(Point tile) => Math.Abs(tile.X - desired.X) + Math.Abs(tile.Y - desired.Y);
        PriorityQueue<(Point Tile, int Cost), int> open = new();
        Dictionary<Point, int> costs = new() { [start] = 0 };
        Dictionary<Point, Point> parents = new();
        Dictionary<Point, bool> walkable = new();
        open.Enqueue((start, 0), Estimate(start));
        Point best = start;
        int bestDist = Estimate(start);
        int visited = 0;
        while (open.TryDequeue(out var node, out _) && visited < limit)
        {
            if (costs[node.Tile] != node.Cost)
                continue;
            visited++;
            int dist = Estimate(node.Tile);
            // Only dequeued tiles are walkable (plus the start); the solid desired tile
            // itself never becomes best because it is never enqueued.
            if (dist < bestDist)
            {
                best = node.Tile;
                bestDist = dist;
                if (bestDist == 0)
                    break;
            }
            foreach (Point direction in Directions)
            {
                Point next = node.Tile + direction;
                int cost = node.Cost + 1;
                if (costs.TryGetValue(next, out int previous) && previous <= cost)
                    continue;
                if (!walkable.TryGetValue(next, out bool allowed))
                    walkable[next] = allowed = canWalk(next);
                if (!allowed)
                    continue;
                costs[next] = cost;
                parents[next] = node.Tile;
                open.Enqueue((next, cost), cost + Estimate(next));
            }
        }
        if (best == start)
            return null;
        Stack<Point> path = new();
        Point tile = best;
        path.Push(tile);
        while (parents.TryGetValue(tile, out Point parent))
        {
            path.Push(parent);
            tile = parent;
        }
        return path;
    }
}
