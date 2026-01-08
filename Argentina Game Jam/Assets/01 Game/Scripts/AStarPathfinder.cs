using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinder
{
    private static readonly Vector2Int[] Dir4 =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private class Node
    {
        public Vector2Int pos;
        public int g;
        public int h;
        public int f => g + h;
        public Node parent;

        public Node(Vector2Int pos, int g, int h, Node parent)
        {
            this.pos = pos;
            this.g = g;
            this.h = h;
            this.parent = parent;
        }
    }

    /// Returns the next step (one tile) from start towards any goal adjacent to the player.
    public static bool TryGetNextStepTowardPlayerAdj(Vector2Int start, Vector2Int playerPos, System.Func<Vector2Int, bool> isBlocked, out Vector2Int nextStep, out int pathLength)
    {
        nextStep = default;
        pathLength = 0;

        // Build candidate goals: 4 tiles adjacent to player (filtered)
        var goals = new List<Vector2Int>(4);
        for (int i = 0; i < 4; i++)
            goals.Add(playerPos + Dir4[i]);

        goals.RemoveAll(g =>
        {
            var t = BoardManager.Instance.GetTile(g);
            if (t == null) return true;
            if (!t.IsWalkable) return true;
            if (g == playerPos) return true;
            if (isBlocked != null && isBlocked(g)) return true;
            return false;
        });

        if (goals.Count == 0) return false;

        var path = AStarToAnyGoal(start, goals, playerPos, isBlocked);
        if (path == null || path.Count < 2) return false;

        nextStep = path[1];
        pathLength = path.Count;
        return true;
    }

    private static List<Vector2Int> AStarToAnyGoal(
        Vector2Int start,
        List<Vector2Int> goals,
        Vector2Int playerPos,
        System.Func<Vector2Int, bool> isBlocked
    )
    {
        var open = new List<Node>();
        var openMap = new Dictionary<Vector2Int, Node>();
        var closed = new HashSet<Vector2Int>();
        var goalSet = new HashSet<Vector2Int>(goals);

        var startNode = new Node(start, 0, HeuristicToClosestGoal(start, goals), null);
        open.Add(startNode);
        openMap[start] = startNode;

        while (open.Count > 0)
        {
            // pick lowest f (tie: lowest h)
            int best = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].f < open[best].f || (open[i].f == open[best].f && open[i].h < open[best].h))
                    best = i;
            }

            Node current = open[best];
            open.RemoveAt(best);
            openMap.Remove(current.pos);

            if (goalSet.Contains(current.pos))
                return Reconstruct(current);

            closed.Add(current.pos);

            for (int i = 0; i < 4; i++)
            {
                Vector2Int npos = current.pos + Dir4[i];

                if (closed.Contains(npos)) continue;
                if (npos == playerPos) continue; // never step onto player tile

                var tile = BoardManager.Instance.GetTile(npos);
                if (tile == null) continue;
                if (!tile.IsWalkable) continue;
                if (isBlocked != null && isBlocked(npos)) continue;

                int tentativeG = current.g + 1;

                if (openMap.TryGetValue(npos, out Node existing))
                {
                    if (tentativeG >= existing.g) continue;
                    existing.g = tentativeG;
                    existing.parent = current;
                    existing.h = HeuristicToClosestGoal(npos, goals);
                    continue;
                }

                var node = new Node(npos, tentativeG, HeuristicToClosestGoal(npos, goals), current);
                open.Add(node);
                openMap[npos] = node;
            }
        }

        return null;
    }

    private static int HeuristicToClosestGoal(Vector2Int p, List<Vector2Int> goals)
    {
        int best = int.MaxValue;
        for (int i = 0; i < goals.Count; i++)
        {
            int d = Mathf.Abs(p.x - goals[i].x) + Mathf.Abs(p.y - goals[i].y);
            if (d < best) best = d;
        }
        return best;
    }

    private static List<Vector2Int> Reconstruct(Node end)
    {
        var path = new List<Vector2Int>();
        Node cur = end;
        while (cur != null)
        {
            path.Add(cur.pos);
            cur = cur.parent;
        }
        path.Reverse();
        return path;
    }
}

