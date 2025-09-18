using System.Collections.Generic;
using UnityEngine;

public static class AStarGrid2D
{
    struct Node
    {
        public int x, y;
        public int g, h;
        public (int x, int y) parent; // clave del padre, (-1,-1) si none
    }

    static readonly (int dx, int dy, int cost)[] Neigh =
    {
        ( 1,  0, 10), (-1,  0, 10), (0,  1, 10), (0, -1, 10),
        ( 1,  1, 14), ( 1, -1, 14), (-1, 1, 14), (-1, -1, 14),
    };

    public static bool FindPath(Vector2 startW, Vector2 goalW, List<Vector2> outPath)
    {
        outPath.Clear();
        var grid = NavGrid2D.Instance;
        if (!grid || !grid.baked) return false;

        if (!grid.WorldToCell(startW, out var s) || !grid.WorldToCell(goalW, out var g)) return false;
        if (!InBoundsWalkable(grid, s.x, s.y)) return false;
        if (!InBoundsWalkable(grid, g.x, g.y)) return false;

        // open/closed por clave (x,y)
        var open = new Dictionary<(int, int), Node>(256);
        var closed = new Dictionary<(int, int), Node>(256);

        var startKey = (s.x, s.y);
        var goalKey = (g.x, g.y);

        open[startKey] = new Node { x = s.x, y = s.y, g = 0, h = Heuristic(s, g), parent = (-1, -1) };

        while (open.Count > 0)
        {
            // seleccionar el mejor F (g+h)
            (int, int) bestKey = default;
            int bestF = int.MaxValue;
            foreach (var kv in open)
            {
                int f = kv.Value.g + kv.Value.h;
                if (f < bestF) { bestF = f; bestKey = kv.Key; }
            }

            var current = open[bestKey];
            open.Remove(bestKey);
            closed[bestKey] = current;

            if (bestKey == goalKey)
            {
                Reconstruct(grid, closed, bestKey, outPath);
                return true;
            }

            foreach (var n in Neigh)
            {
                int nx = current.x + n.dx;
                int ny = current.y + n.dy;
                var nk = (nx, ny);

                if (!InBoundsWalkable(grid, nx, ny)) continue;
                if (closed.ContainsKey(nk)) continue;

                // opcional: bloquear “corner cutting” en diagonales
                if (n.dx != 0 && n.dy != 0)
                {
                    int sx = current.x + n.dx;
                    int sy = current.y;
                    int tx = current.x;
                    int ty = current.y + n.dy;
                    if (!InBoundsWalkable(grid, sx, sy) || !InBoundsWalkable(grid, tx, ty))
                        continue;
                }

                int tentativeG = current.g + n.cost;

                if (open.TryGetValue(nk, out var old))
                {
                    if (tentativeG < old.g)
                    {
                        old.g = tentativeG;
                        old.parent = bestKey;
                        open[nk] = old;
                    }
                }
                else
                {
                    open[nk] = new Node
                    {
                        x = nx,
                        y = ny,
                        g = tentativeG,
                        h = Heuristic(new Vector2Int(nx, ny), g),
                        parent = bestKey
                    };
                }
            }
        }

        return false;
    }

    static bool InBoundsWalkable(NavGrid2D grid, int x, int y)
    {
        if (x < 0 || y < 0 || x >= grid.size.x || y >= grid.size.y) return false;
        return grid.walkable[x, y];
    }

    static int Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int dmin = Mathf.Min(dx, dy);
        int dmax = Mathf.Max(dx, dy);
        return 14 * dmin + 10 * (dmax - dmin); // octile
    }

    static void Reconstruct(NavGrid2D grid, Dictionary<(int, int), Node> closed, (int, int) key, List<Vector2> outPath)
    {
        var stack = new List<Vector2>(128);
        var curKey = key;

        while (curKey != (-1, -1))
        {
            var n = closed[curKey];
            stack.Add(grid.CellCenter(n.x, n.y));
            curKey = n.parent;
        }

        for (int i = stack.Count - 1; i >= 0; i--)
            outPath.Add(stack[i]);
    }
}
