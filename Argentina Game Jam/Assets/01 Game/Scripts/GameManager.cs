using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TurnState { PlayerTurn, EnemyTurn, Busy, Won, Lost }
public enum ActionType { Move, Attack }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Rules")]
    public int maxHeat = 100;
    public int startingHeat = 0;

    [Header("Actions")]
    public int actionsPerTurn = 3;
    public int actionsLeft { get; private set; }
    public int attackHeatCost = 10;
    public int attackDamage = 1;

    [Header("Attack Rotation")]
    public float attackRotationSpeed = 720f;

    [Header("Burn Rule")]
    public int maxConsecutiveBurnTiles = 2;
    public int consecutiveBurnCount { get; private set; }

    [Header("Runtime")]
    public TurnState state { get; private set; } = TurnState.PlayerTurn;
    public int heat { get; private set; }

    [Header("Win/Lose")]
    public Tile startTile;
    public Tile goalTile;

    [Header("Refs")]
    public PlayerController player;
    public InputManager inputManager;

    [Header("Enemies")]
    public List<EnemyUnit> enemies = new();

    private List<EnemyInitialData> _enemyInitialData = new();

    // -------- Events ------------
    public event Action<TurnState> TurnStateChanged;
    public event Action<int, int> HeatChanged;
    public event Action<int, int> ActionsChanged;
    public event Action<string> GameLost;
    public event Action<string> GameWon;
    public event Action GameReset;

    // -------- Helpers ----------
    private void RaiseTurnStateChanged() => TurnStateChanged?.Invoke(state);
    private void RaiseHeatChanged() => HeatChanged?.Invoke(heat, maxHeat);
    private void RaiseActionsChanged() => ActionsChanged?.Invoke(actionsLeft, actionsPerTurn);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameManager detected. Destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        enemies.Clear();
        enemies.AddRange(FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None));

        SaveEnemyInitialData();
        ResetRun();
    }

    private void SaveEnemyInitialData()
    {
        _enemyInitialData.Clear();

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            _enemyInitialData.Add(new EnemyInitialData
            {
                enemy = enemy,
                initialTile = enemy.currentTile,
                initialHealth = enemy.health
            });
        }

        Debug.Log($"Saved initial data for {_enemyInitialData.Count} enemies.");
    }

    public void ResetRun()
    {
        state = TurnState.PlayerTurn;
        heat = startingHeat;
        consecutiveBurnCount = 0;

        ResetEnemies();

        if (player != null)
        {
            if (startTile)
                player.SnapToTile(startTile);

            var animController = player.GetComponent<PlayerAnimationController>();
            animController?.ResetToIdle();
        }

        GameReset?.Invoke();
        StartPlayerTurn();
    }

    private void ResetEnemies()
    {
        enemies.Clear();

        foreach (var data in _enemyInitialData)
        {
            if (data.enemy == null) continue;

            data.enemy.ResetEnemy(data.initialHealth);

            if (data.initialTile != null)
                data.enemy.SnapToTile(data.initialTile);

            enemies.Add(data.enemy);
        }

        Debug.Log($"Reset: {enemies.Count} enemies restored.");
    }

    // ---------------- TURN FLOW ----------------

    public void StartPlayerTurn()
    {
        if (state == TurnState.Won || state == TurnState.Lost) return;

        state = TurnState.PlayerTurn;
        actionsLeft = actionsPerTurn;

        if (inputManager) inputManager.enabled = true;

        RaiseTurnStateChanged();
        RaiseHeatChanged();
        RaiseActionsChanged();

        Debug.Log("═══════════════════════════════════════");
        Debug.Log("🔵 PLAYER TURN - STARTED");
        Debug.Log($"   Actions: {actionsLeft}/{actionsPerTurn}");
        Debug.Log("═══════════════════════════════════════");
    }

    public void EndPlayerTurn()
    {
        if (state != TurnState.PlayerTurn) return;

        Debug.Log("═══════════════════════════════════════");
        Debug.Log("🔵 PLAYER TURN - ENDED");
        Debug.Log("═══════════════════════════════════════");

        // TAG CHECK: only here (end of player turn)
        if (CheckLoseTaggedEndOfTurn())
        {
            // Lose() already called inside the method
            return;
        }

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        if (state == TurnState.Won || state == TurnState.Lost) yield break;

        Debug.Log("════════════════════════════════════════");
        Debug.Log("🔴 ENEMY TURN - STARTED");
        Debug.Log("════════════════════════════════════════");

        // Wait for player movement to finish (safety)
        if (player != null)
        {
            float waitTime = 0f;
            float maxWait = 3f;

            while (player.IsMoving && waitTime < maxWait)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.15f);

        state = TurnState.EnemyTurn;
        RaiseTurnStateChanged();

        if (inputManager) inputManager.enabled = false;

        // Count alive enemies
        int aliveCount = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && !enemies[i].IsDead) aliveCount++;
        }

        Debug.Log($"Enemies alive: {aliveCount}");

        if (aliveCount == 0)
        {
            Debug.Log("No alive enemies. Returning to player.");
            yield return new WaitForSeconds(0.2f);
            StartPlayerTurn();
            yield break;
        }

        // Each enemy gets ONE action: move 1 tile toward the player using A*
        int processedCount = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || enemy.IsDead) continue;

            processedCount++;
            Debug.Log($"Enemy {processedCount}/{aliveCount}: {enemy.name}");

            // IMPORTANT:
            // Your EnemyUnit.TakeTurnCoroutine() currently may ATTACK if adjacent.
            // For "tag-only" enemies, you should remove/disable attack inside EnemyUnit,
            // or create a TakeTagTurnCoroutine() that only moves.
            yield return enemy.TakeTurnCoroutine();

            yield return new WaitForSeconds(0.15f);

            if (state == TurnState.Won || state == TurnState.Lost) yield break;
        }

        Debug.Log($"Enemies processed: {processedCount}");
        yield return new WaitForSeconds(0.15f);

        Debug.Log("════════════════════════════════════════");
        Debug.Log("🔵 RETURNING TURN TO PLAYER");
        Debug.Log("════════════════════════════════════════");

        StartPlayerTurn();
    }

    // ---------------- ACTIONS ----------------

    public bool CanSpendAction()
        => state == TurnState.PlayerTurn && actionsLeft > 0;

    // Optional helper if you still use it elsewhere
    public void ApplyActionTile(ActionType actionType, int heatDelta)
    {
        if (!CanSpendAction()) return;

        actionsLeft--;
        heat = Mathf.Clamp(heat + heatDelta, 0, maxHeat);

        RaiseActionsChanged();
        RaiseHeatChanged();

        Debug.Log($"Action: {actionType} | Actions left: {actionsLeft}/{actionsPerTurn} | Heat: {heat}/{maxHeat}");

        if (CheckLoseHeat()) return;

        if (actionsLeft <= 0 && state == TurnState.PlayerTurn)
            EndPlayerTurn();
    }

    // Kept for compatibility, but with "tag-only" enemies you likely won't call this anymore
    public void ApplyEnemyAttackHeat(int heatDelta)
    {
        heat = Mathf.Clamp(heat + heatDelta, 0, maxHeat);
        RaiseHeatChanged();
        CheckLoseHeat();
    }

    // ---------------- MOVEMENT RULES ----------------

    public bool CanEnterTile(Tile tile)
    {
        if (state != TurnState.PlayerTurn) return false;
        if (tile == null) return false;
        if (!tile.IsWalkable) return false;
        if (actionsLeft <= 0) return false;

        if (IsTileOccupiedByEnemy(tile))
            return false;

        if (tile.type == TileType.Burn && consecutiveBurnCount >= maxConsecutiveBurnTiles)
            return false;

        return true;
    }

    public bool CanMoveToTile(Tile tile)
    {
        if (state != TurnState.PlayerTurn) return false;
        if (actionsLeft <= 0) return false;
        if (player == null || player.currentTile == null) return false;
        if (!BoardManager.Instance.AreAdjacent4D(player.currentTile.gridPos, tile.gridPos)) return false;
        return CanEnterTile(tile);
    }

    public void OnPlayerEnteredTile(Tile tile)
    {
        if (state != TurnState.PlayerTurn) return;

        Debug.Log($"Player entered tile {tile.gridPos} (type: {tile.type})");

        // Update consecutive burn counter
        if (tile.type == TileType.Burn) consecutiveBurnCount++;
        else consecutiveBurnCount = 0;

        // Spend action + apply heat
        actionsLeft--;
        heat = Mathf.Clamp(heat + tile.heatDeltaOnEnter, 0, maxHeat);

        RaiseActionsChanged();
        RaiseHeatChanged();

        Debug.Log($"Action spent | Actions left: {actionsLeft}/{actionsPerTurn} | Heat: {heat}/{maxHeat}");

        // Consume tile if needed
        if (tile.type == TileType.Shade || tile.type == TileType.Drink)
            tile.ConsumeIfNeeded();

        // Check win
        if (tile == goalTile && state != TurnState.Lost)
        {
            Win("Goal reached.");
            return;
        }

        // Check lose by heat immediately
        if (CheckLoseHeat()) return;

        // Auto end turn when no actions left (no manual end turn)
        if (actionsLeft <= 0 && state == TurnState.PlayerTurn)
            EndPlayerTurn();
    }

    // ---------------- ATTACK RULES ----------------
    // (You can keep this for now. If you want "no attack" later, we can remove it safely.)
    public bool CanAttackEnemyOnTile(Tile targetTile)
    {
        if (state != TurnState.PlayerTurn) return false;
        if (actionsLeft <= 0) return false;
        if (player == null || player.currentTile == null) return false;

        var enemy = GetEnemyOnTile(targetTile);
        if (enemy == null) return false;

        if (!BoardManager.Instance.AreAdjacent4D(player.currentTile.gridPos, targetTile.gridPos))
            return false;

        return true;
    }

    public EnemyUnit GetEnemyOnTile(Tile tile)
    {
        if (tile == null) return null;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (enemy.currentTile == tile) return enemy;
        }
        return null;
    }

    public void AttackEnemyOnTile(Tile targetTile)
    {
        if (!CanAttackEnemyOnTile(targetTile))
        {
            Debug.Log("Attack not possible.");
            return;
        }

        var enemy = GetEnemyOnTile(targetTile);
        if (enemy == null)
        {
            Debug.Log("No enemy found on target tile.");
            return;
        }

        Debug.Log($"Attacking enemy on tile {targetTile.gridPos}");
        StartCoroutine(AttackRoutine(enemy));
    }

    private IEnumerator AttackRoutine(EnemyUnit enemy)
    {
        if (player == null || enemy == null) yield break;

        Vector3 directionToEnemy = (enemy.transform.position - player.transform.position).normalized;

        if (directionToEnemy != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);

            while (Quaternion.Angle(player.transform.rotation, targetRotation) > 0.1f)
            {
                player.transform.rotation = Quaternion.RotateTowards(
                    player.transform.rotation,
                    targetRotation,
                    attackRotationSpeed * Time.deltaTime
                );
                yield return null;
            }

            player.transform.rotation = targetRotation;
        }

        player.GetComponent<PlayerAnimationController>()?.PlayAttack();
        yield return new WaitForSeconds(0.1f);

        enemy.TakeDamage(attackDamage);

        // Spend action + apply heat cost
        actionsLeft--;
        heat = Mathf.Clamp(heat + attackHeatCost, 0, maxHeat);

        RaiseActionsChanged();
        RaiseHeatChanged();

        Debug.Log($"Attack done | Actions left: {actionsLeft}/{actionsPerTurn} | Heat: {heat}/{maxHeat}");

        if (CheckLoseHeat()) yield break;

        if (actionsLeft <= 0 && state == TurnState.PlayerTurn)
            EndPlayerTurn();
    }

    // ---------------- ENEMIES ----------------

    public bool IsTileOccupiedByEnemy(Tile tile)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null || e.IsDead) continue;
            if (e.currentTile == tile) return true;
        }
        return false;
    }

    public void RemoveEnemy(EnemyUnit enemy)
    {
        enemies.Remove(enemy);
    }

    // ---------------- WIN / LOSE ----------------

    private bool CheckLoseHeat()
    {
        if (heat >= maxHeat)
        {
            Lose("Game Over: Heat limit reached!");
            return true;
        }
        return false;
    }

    private bool CheckLoseTaggedEndOfTurn()
    {
        if (player == null || player.currentTile == null)
        {
            Debug.LogWarning("CheckLoseTaggedEndOfTurn: Player or Player.currentTile is null.");
            return false;
        }

        Vector2Int playerPos = player.currentTile.gridPos;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyUnit e = enemies[i];
            if (e == null || e.IsDead || e.currentTile == null) continue;

            Vector2Int enemyPos = e.currentTile.gridPos;

            if (BoardManager.Instance != null && BoardManager.Instance.AreAdjacent8D(enemyPos, playerPos))
            {
                Lose("Game Over: You were tagged!");
                return true;
            }
        }

        return false;
    }

    private void Win(string msg)
    {
        state = TurnState.Won;

        RaiseTurnStateChanged();
        GameWon?.Invoke(msg);

        if (inputManager) inputManager.enabled = false;

        Debug.Log($"Victory: {msg}");
    }

    private void Lose(string msg)
    {
        player.GetComponent<PlayerAnimationController>()?.PlayDeath();

        state = TurnState.Lost;

        RaiseTurnStateChanged();
        GameLost?.Invoke(msg);

        if (inputManager) inputManager.enabled = false;

        Debug.Log($"Defeat: {msg}");
    }
}

[System.Serializable]
public class EnemyInitialData
{
    public EnemyUnit enemy;
    public Tile initialTile;
    public int initialHealth;
}
