using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyActions))]
public class EnemyUnit : MonoBehaviour
{
    [Header("Stats")]
    public int health = 2;

    [Header("Attack (unused for Tag-only prototype)")]
    public int attackHeatDamage = 5;
    public GameObject attackEffectPrefab;
    public float attackEffectDuration = 5f;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 720f;

    [Header("Runtime")]
    public Tile currentTile;
    public bool IsDead => health <= 0;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private GameObject _visualMesh;
    private EnemyActions _actions;

    private bool _isExecutingTurn;

    private void Awake()
    {
        _actions = GetComponent<EnemyActions>();

        if (transform.childCount > 0)
        {
            _visualMesh = transform.GetChild(0).gameObject;
        }
        else
        {
            Debug.LogWarning($"EnemyUnit '{name}' has no children. Visual mesh should be a child GameObject.");
        }
    }

    private void Start()
    {
        if (currentTile == null)
            DebugLog("WARNING: currentTile not set on Start.");
        else
            SnapToTile(currentTile);
    }

    public void SnapToTile(Tile tile)
    {
        currentTile = tile;
        if (tile != null)
            transform.position = tile.transform.position;
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        DebugLog($"Enemy took damage: {amount}. HP: {health}");

        if (health <= 0)
        {
            DebugLog("Enemy defeated.");
            if (GameManager.Instance != null)
                GameManager.Instance.RemoveEnemy(this);

            if (_visualMesh != null) _visualMesh.SetActive(false);
            else gameObject.SetActive(false);
        }
    }

    public void ResetEnemy(int initialHealth)
    {
        health = initialHealth;
        _isExecutingTurn = false;

        if (_visualMesh != null) _visualMesh.SetActive(true);
        else gameObject.SetActive(true);

        DebugLog($"Enemy '{name}' reset with {health} HP.");
    }

    public IEnumerator TakeTurnCoroutine()
    {
        if (_isExecutingTurn)
        {
            DebugLog("WARNING: Turn already executing. Aborting.");
            yield break;
        }

        if (IsDead)
        {
            DebugLog("INFO: Enemy is dead. Skipping turn.");
            yield break;
        }

        if (currentTile == null)
        {
            DebugLog("ERROR: currentTile is NULL. Cannot take turn.");
            yield break;
        }

        var gm = GameManager.Instance;
        if (gm == null)
        {
            DebugLog("ERROR: GameManager.Instance is NULL. Cannot take turn.");
            yield break;
        }

        var player = gm.player;
        if (player == null || player.currentTile == null)
        {
            DebugLog("ERROR: Player or Player.currentTile is NULL. Cannot take turn.");
            yield break;
        }

        if (_actions == null)
        {
            DebugLog("ERROR: EnemyActions component is missing. Cannot take turn.");
            yield break;
        }

        _isExecutingTurn = true;

        Vector2Int myPos = currentTile.gridPos;
        Vector2Int playerPos = player.currentTile.gridPos;

        // Tag is checked in EndPlayerTurn using 8D adjacency,
        // so enemies should also consider 8D adjacency as "already in tagging range".
        bool inTagRange = false;
        if (BoardManager.Instance != null)
            inTagRange = BoardManager.Instance.AreAdjacent4D(myPos, playerPos);

        DebugLog($"Turn start. MyPos={myPos} PlayerPos={playerPos} Adjacent position(4D)={inTagRange}");

        // TAG-ONLY: if already adjacent (8D), DO NOTHING.
        // The GameManager will check tag at end of player's turn.
        if (inTagRange)
        {
            DebugLog("Adjacent position: holding position.");
            yield return new WaitForSeconds(0.15f); // optional: tiny pause for readability
            _isExecutingTurn = false;
            yield break;
        }

        // Otherwise move 1 step using A* toward any tile adjacent to player
        bool IsBlocked(Vector2Int pos)
        {
            Tile t = BoardManager.Instance.GetTile(pos);
            return t != null && IsTileOccupiedByOtherEnemy(t);
        }

        if (AStarPathfinder.TryGetNextStepTowardPlayerAdj(
                start: myPos,
                playerPos: playerPos,
                isBlocked: IsBlocked,
                nextStep: out Vector2Int nextStep,
                pathLength: out int pathLen))
        {
            Tile nextTile = BoardManager.Instance.GetTile(nextStep);
            if (nextTile != null)
            {
                DebugLog($"Moving next step: {nextStep} (pathLen={pathLen})");
                yield return _actions.MoveToTileCoroutine(nextTile);
            }
            else
            {
                DebugLog("ERROR: Next step tile resolved to NULL. Staying still.");
                yield return new WaitForSeconds(0.15f);
            }
        }
        else
        {
            DebugLog("No valid A* move found. Staying still.");
            yield return new WaitForSeconds(0.15f);
        }

        DebugLog("Turn end.");
        _isExecutingTurn = false;
    }

    private bool IsTileOccupiedByOtherEnemy(Tile tile)
    {
        var gm = GameManager.Instance;
        if (gm == null || tile == null) return false;

        foreach (var enemy in gm.enemies)
        {
            if (enemy == null || enemy.IsDead || enemy == this) continue;
            if (enemy.currentTile == tile) return true;
        }

        return false;
    }

    public void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[{name}] {message}");
    }
}

