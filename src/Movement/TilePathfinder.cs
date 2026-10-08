using Microsoft.Xna.Framework;

namespace AgrariaOS.Movement;

/// <summary>Four-way A* with full-size coordinates and a bounded amount of work per click.</summary>
internal static class TilePathfinder
{
    internal static readonly Point[] Directions = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };

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
