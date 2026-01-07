using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    private readonly Dictionary<Vector2Int, Tile> _tiles = new();

    [Header("Auto GridPos")]
    public Transform gridOrigin;
    public float cellSize = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate BoardManager detected. Destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Tile[] tiles = GetComponentsInChildren<Tile>(true);

        AutoGridPositionsInTiles(tiles);
        RegisterAllTilesInDictionary(tiles);
    }

    private void RegisterAllTilesInDictionary(Tile[] tiles)
    {
        _tiles.Clear();

        foreach (Tile t in tiles)
        {
            if (_tiles.ContainsKey(t.gridPos))
            {
                Debug.LogWarning($"Duplicate gridPos detected: {t.gridPos} (Tile: {t.name})");
                continue;
            }
            _tiles.Add(t.gridPos, t);
        }
    }

    private void AutoGridPositionsInTiles(Tile[] tiles)
    {
        if (gridOrigin == null)
        {
            Debug.LogWarning("Grid origin is not assigned. Auto gridPos skipped.");
            return;
        }

        if (cellSize <= 0f)
        {
            Debug.LogWarning("Cell size must be > 0. Auto gridPos skipped.");
            return;
        }

        foreach (Tile t in tiles)
        {
            Vector3 p = t.transform.position;
            Vector3 o = gridOrigin.position;

            float fx = (p.x - o.x) / cellSize;
            float fy = (p.z - o.z) / cellSize;

            int gx = Mathf.RoundToInt(fx);
            int gy = Mathf.RoundToInt(fy);

            t.gridPos = new Vector2Int(gx, gy);
        }

        Debug.Log($"Auto-assigned gridPos for {tiles.Length} tiles.");
    }

    public Tile GetTile(Vector2Int pos)
        => _tiles.TryGetValue(pos, out var t) ? t : null;

    public bool AreAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) == 1; // sin diagonales
    }
}
