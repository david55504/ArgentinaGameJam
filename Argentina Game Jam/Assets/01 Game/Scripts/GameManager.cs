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
            if (animController != null)
            {
                animController.ResetToIdle();
            }
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
            {
                data.enemy.SnapToTile(data.initialTile);
            }

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
        Debug.Log("🔵 TURNO DEL JUGADOR - INICIADO");
        Debug.Log($"   Acciones: {actionsLeft}/{actionsPerTurn}");
        Debug.Log("═══════════════════════════════════════");
    }

    public void EndPlayerTurn()
    {
        if (state != TurnState.PlayerTurn) return;

        Debug.Log("═══════════════════════════════════════");
        Debug.Log("🔵 TURNO DEL JUGADOR - FINALIZADO");
        Debug.Log("═══════════════════════════════════════");
        
        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        Debug.Log("════════════════════════════════════════");
        Debug.Log("🔴 ENEMY TURN ROUTINE - INICIANDO");
        Debug.Log("════════════════════════════════════════");

        // Esperar a que el jugador termine de moverse
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

        yield return new WaitForSeconds(0.2f);

        state = TurnState.EnemyTurn;
        RaiseTurnStateChanged();
        
        if (inputManager) inputManager.enabled = false;

        // Contar enemigos vivos
        int aliveCount = 0;
        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead) aliveCount++;
        }

        Debug.Log($"📊 ENEMIGOS: {aliveCount} vivos");

        if (aliveCount == 0)
        {
            Debug.Log("⚠️ No hay enemigos vivos");
            yield return new WaitForSeconds(0.5f);
            StartPlayerTurn();
            yield break;
        }

        Debug.Log("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        Debug.Log("┃  PROCESANDO TURNOS DE ENEMIGOS     ┃");
        Debug.Log("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");

        int processedCount = 0;
        
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            
            if (enemy == null || enemy.IsDead) continue;

            processedCount++;
            Debug.Log($"▶ Enemigo {processedCount}/{aliveCount}: {enemy.name}");
            
            yield return enemy.TakeTurnCoroutine();
            yield return new WaitForSeconds(0.3f);
        }

        Debug.Log("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        Debug.Log($"✅ {processedCount} enemigos completaron su turno");

        yield return new WaitForSeconds(0.3f);

        Debug.Log("════════════════════════════════════════");
        Debug.Log("🔵 DEVOLVIENDO TURNO AL JUGADOR");
        Debug.Log("════════════════════════════════════════");

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
        
        Debug.Log($"⚡ Acción: {actionType} | Restantes: {actionsLeft}/{actionsPerTurn} | Heat: {heat}/{maxHeat}");

        if (CheckWinLoseAfterHeat()) return;

        // En Sistema A, NO terminamos el turno aquí automáticamente
        // El turno solo termina cuando actionsLeft == 0 DESPUÉS de que los enemigos respondan
    }

    public void ApplyEnemyAttackHeat(int heatDelta)
    {
        heat = Mathf.Clamp(heat + heatDelta, 0, maxHeat);
        RaiseHeatChanged();
        CheckWinLoseAfterHeat();
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
        if (!BoardManager.Instance.AreAdjacent(player.currentTile.gridPos, tile.gridPos)) return false;
        return CanEnterTile(tile);
    }

    public void OnPlayerEnteredTile(Tile tile)
    {
        if (state != TurnState.PlayerTurn) return;

        Debug.Log($"🟢 Jugador entró a tile {tile.gridPos} (tipo: {tile.type})");

        // Actualizar contador de tiles quemados consecutivos
        if (tile.type == TileType.Burn) consecutiveBurnCount++;
        else consecutiveBurnCount = 0;

        // Gastar acción y aplicar calor
        actionsLeft--;
        heat = Mathf.Clamp(heat + tile.heatDeltaOnEnter, 0, maxHeat);
        
        RaiseActionsChanged();
        RaiseHeatChanged();
        
        Debug.Log($"⚡ Acción gastada | Restantes: {actionsLeft}/{actionsPerTurn} | Heat: {heat}/{maxHeat}");

        // Consumir tile si es necesario
        if (tile.type == TileType.Shade || tile.type == TileType.Drink)
            tile.ConsumeIfNeeded();

        // Verificar victoria
        if (tile == goalTile && state != TurnState.Lost)
        {
            Win("Goal reached.");
            return;
        }

        // Verificar derrota por calor
        if (CheckWinLoseAfterHeat()) return;

        // 🆕 SISTEMA A: Después de cada movimiento, los enemigos responden
        Debug.Log("🔄 Iniciando respuesta de enemigos...");
        StartCoroutine(EnemyResponseAfterPlayerMove());
    }

    // 🆕 NUEVO MÉTODO: Enemigos responden después de cada movimiento
    private IEnumerator EnemyResponseAfterPlayerMove()
    {
        // Bloquear input temporalmente
        TurnState previousState = state;
        state = TurnState.Busy;
        RaiseTurnStateChanged();
        if (inputManager) inputManager.enabled = false;

        // Pequeña pausa visual
        yield return new WaitForSeconds(0.15f);

        Debug.Log("┌─────────────────────────────────────┐");
        Debug.Log("│  🔴 RESPUESTA DE ENEMIGOS           │");
        Debug.Log("└─────────────────────────────────────┘");

        // Cada enemigo vivo juega una vez
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            
            count++;
            Debug.Log($"▶ Enemigo {count}: {enemy.name}");
            yield return enemy.TakeTurnCoroutine();
            yield return new WaitForSeconds(0.25f);
        }

        Debug.Log($"✅ {count} enemigos completaron su respuesta");
        Debug.Log("└─────────────────────────────────────┘");

        // Verificar si el turno debe terminar (sin acciones)
        if (actionsLeft <= 0)
        {
            Debug.Log("🔴 SIN ACCIONES RESTANTES - Reiniciando turno del jugador");
            StartPlayerTurn(); // Reinicia con 3 acciones nuevas
        }
        else
        {
            Debug.Log($"🟢 Turno continúa - {actionsLeft} acciones disponibles");
            state = TurnState.PlayerTurn;
            RaiseTurnStateChanged();
            if (inputManager) inputManager.enabled = true;
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

        Debug.Log($"⚔️ Atacando enemigo en tile {targetTile.gridPos}");

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

        // Gastar acción y aplicar costo de calor
        actionsLeft--;
        heat = Mathf.Clamp(heat + attackHeatCost, 0, maxHeat);
        
        RaiseActionsChanged();
        RaiseHeatChanged();
        
        Debug.Log($"⚡ Ataque realizado | Restantes: {actionsLeft}/{actionsPerTurn} | Heat: {heat}/{maxHeat}");

        if (CheckWinLoseAfterHeat()) yield break;

        // 🆕 SISTEMA A: Después de atacar, los enemigos también responden
        Debug.Log("🔄 Iniciando respuesta de enemigos tras ataque...");
        yield return EnemyResponseAfterPlayerMove();
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
        
        Debug.Log($"🎉 VICTORIA: {msg}");
    }

    private void Lose(string msg)
    {
        player.GetComponent<PlayerAnimationController>()?.PlayDeath();
        
        state = TurnState.Lost;

        RaiseTurnStateChanged();
        GameLost?.Invoke(msg);
        
        if (inputManager) inputManager.enabled = false;
        
        Debug.Log($"💀 DERROTA: {msg}");
    }
}

[System.Serializable]
public class EnemyInitialData
{
    public EnemyUnit enemy;
    public Tile initialTile;
    public int initialHealth;
}