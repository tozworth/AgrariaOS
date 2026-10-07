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
}
