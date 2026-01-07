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

    // -------- Events ------------
    public event Action<TurnState> TurnStateChanged;
    public event Action<int, int> HeatChanged;                 // (heat, maxHeat)
    public event Action<int, int> ActionsChanged;              // (actionsLeft, actionsPerTurn)
    public event Action<string> GameLost;                      // message
    public event Action<string> GameWon;                       // message
    public event Action GameReset;                             // when starting/restarting

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

        ResetRun();
    }

    public void ResetRun()
    {
        state = TurnState.PlayerTurn;
        heat = startingHeat;
        consecutiveBurnCount = 0;

        if (player && startTile)
            player.SnapToTile(startTile);

        StartPlayerTurn();
    }

    // ---------------- TURN FLOW ----------------

    public void StartPlayerTurn()
    {
        if (state == TurnState.Won || state == TurnState.Lost) return;

        state = TurnState.PlayerTurn;
        actionsLeft = actionsPerTurn;

        // Enable input
        if (inputManager) inputManager.enabled = true;

        RaiseTurnStateChanged();
        RaiseHeatChanged();
        RaiseActionsChanged();
    }

    public void EndPlayerTurn()
    {
        if (state != TurnState.PlayerTurn) return;

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        state = TurnState.EnemyTurn;
        RaiseTurnStateChanged();

        // Disable input during enemy turn
        if (inputManager) inputManager.enabled = false;

        // MVP: enemies do nothing or move 1 step (later)
        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;

            // Option A (day 1): do nothing
            // yield return null;

            // Option B (day 2): move 1 step (you implement in EnemyUnit)
            //yield return e.TakeTurnCoroutine();
        }

        yield return new WaitForSeconds(2f);

        StartPlayerTurn();
    }

    // ---------------- ACTIONS ----------------

    public bool CanSpendAction()
        => state == TurnState.PlayerTurn && actionsLeft > 0;

    public void ApplyActionTile(ActionType actionType, int heatDelta)
    {
        if (!CanSpendAction()) return;

        actionsLeft--;
        heat = Mathf.Clamp(heat + heatDelta, 0, maxHeat);

        RaiseActionsChanged();
        RaiseHeatChanged();

        if (CheckWinLoseAfterHeat()) return;

        if (actionsLeft <= 0)
            EndPlayerTurn();
    }

    // ---------------- MOVEMENT RULES ----------------

    public bool CanEnterTile(Tile tile)
    {
        if (state != TurnState.PlayerTurn) return false;
        if (tile == null) return false;
        if (!tile.IsWalkable) return false;
        if (actionsLeft <= 0) return false;

        // Enemy occupancy check
        if (IsTileOccupiedByEnemy(tile))
            return false;

        // Burn streak cap
        if (tile.type == TileType.Burn && consecutiveBurnCount >= maxConsecutiveBurnTiles)
            return false;

        return true;
    }

    public bool CanMoveToTile(Tile tile)
    {
        if (state != TurnState.PlayerTurn) return false;
        if (actionsLeft <= 0) return false;
        if (player == null || player.currentTile == null) return false;
        if (!BoardManager.Instance.AreAdjacent(player.currentTile.gridPos, tile.gridPos)) return false;
        return CanEnterTile(tile);
    }

    public void OnPlayerEnteredTile(Tile tile)
    {
        if (state != TurnState.PlayerTurn) return;

        // Burn streak update
        if (tile.type == TileType.Burn) consecutiveBurnCount++;
        else consecutiveBurnCount = 0;

        // NOTE: tile.heatDeltaOnEnter should represent the movement heat effect for that tile.
        ApplyActionTile(ActionType.Move, tile.heatDeltaOnEnter);

        // Consumables
        if (tile.type == TileType.Shade || tile.type == TileType.Drink)
            tile.ConsumeIfNeeded();

        // Win check on entering tile
        if (tile == goalTile && state != TurnState.Lost)
        {
            Win("Goal reached.");
            return;
        }
    }

    // ---------------- ATTACK RULES ----------------
    public bool CanAttackEnemyOnTile(Tile targetTile)
    {
        if (state != TurnState.PlayerTurn) return false;
        if (actionsLeft <= 0) return false;
        if (player == null || player.currentTile == null) return false;

        var enemy = GetEnemyOnTile(targetTile);
        if (enemy == null) return false;

        // Must be adjacent (no diagonals)
        if (!BoardManager.Instance.AreAdjacent(player.currentTile.gridPos, targetTile.gridPos))
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

        Debug.Log($"Attacking enemy on tile {targetTile.gridPos}.");

        enemy.TakeDamage(attackDamage);

        // Consume 1 action + add heat
        ApplyActionTile(ActionType.Attack, attackHeatCost);
    }

    // ---------------- ENEMIES / ATTACK ----------------

    public bool IsTileOccupiedByEnemy(Tile tile)
    {
        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;
            if (e.currentTile == tile) return true;
        }
        return false;
    }

    public void RemoveEnemy(EnemyUnit enemy)
    {
        // safe cleanup
        enemies.Remove(enemy);
    }

    // ---------------- WIN / LOSE ----------------

    private bool CheckWinLoseAfterHeat()
    {
        if (heat >= maxHeat)
        {
            Lose("Game Over");
            return true;
        }
        return false;
    }

    private void Win(string msg)
    {
        state = TurnState.Won;

        RaiseTurnStateChanged();
        GameWon?.Invoke(msg);

        if (inputManager) inputManager.enabled = false;
    }

    private void Lose(string msg)
    {
        state = TurnState.Lost;

        RaiseTurnStateChanged();
        GameLost?.Invoke(msg);
        
        if (inputManager) inputManager.enabled = false;
    }
}


