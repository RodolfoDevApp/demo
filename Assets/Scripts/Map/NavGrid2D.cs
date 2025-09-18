using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[AddComponentMenu("Navigation2D/Nav Grid Baker 2D")]
public class NavGrid2D : MonoBehaviour
{
    [Header("Fuente de colisiones")]
    public Tilemap collidersTilemap;
    public LayerMask obstacleMask = ~0;

    [Header("Grilla")]
    public float cellSize = 0.5f;
    public int extraBorderCells = 2;

    [Header("Area de bake")]
    public Bounds worldBounds;

    [Header("Debug")]
    public bool drawGizmos = false;

    public bool baked { get; private set; }
    public Vector2Int size;            // ancho x alto en celdas
    public Vector2 origin;             // esquina inferior izquierda
    public bool[,] walkable;           // true si se puede pasar

    public static NavGrid2D Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        if (collidersTilemap && worldBounds.size == Vector3.zero)
        {
            worldBounds = collidersTilemap.localBounds;
            worldBounds.center = collidersTilemap.transform.TransformPoint(worldBounds.center);
            worldBounds.size = Vector3.Scale(worldBounds.size, collidersTilemap.transform.lossyScale);
        }
        Bake();
    }

    public void Bake()
    {
        if (cellSize <= 0.05f) cellSize = 0.05f;
        var min = new Vector2(worldBounds.min.x, worldBounds.min.y);
        var max = new Vector2(worldBounds.max.x, worldBounds.max.y);

        origin = min - Vector2.one * (extraBorderCells * cellSize);
        size = new Vector2Int(
            Mathf.CeilToInt((max.x - min.x) / cellSize) + extraBorderCells * 2,
            Mathf.CeilToInt((max.y - min.y) / cellSize) + extraBorderCells * 2
        );

        walkable = new bool[size.x, size.y];

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2 p = CellCenter(x, y);
                // bloquea si tile solido o si hay collider solido en la mascara
                bool solidTile = collidersTilemap ? collidersTilemap.HasTile(collidersTilemap.WorldToCell(p)) : false;
                bool solidPhys = Physics2D.OverlapPoint(p, obstacleMask);
                walkable[x, y] = !(solidTile || solidPhys);
            }
        }
        baked = true;
    }

    public Vector2 CellCenter(int x, int y) => origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

    public bool WorldToCell(Vector2 w, out Vector2Int c)
    {
        Vector2 local = (w - origin) / cellSize;
        c = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        return c.x >= 0 && c.y >= 0 && c.x < size.x && c.y < size.y;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || walkable == null) return;
        Gizmos.color = new Color(0, 1, 0, 0.15f);
        for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                if (walkable[x, y])
                    Gizmos.DrawCube(CellCenter(x, y), Vector3.one * cellSize * 0.95f);
    }
}
