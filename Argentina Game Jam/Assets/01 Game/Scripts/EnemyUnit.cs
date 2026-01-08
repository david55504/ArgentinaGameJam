using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyUnit : MonoBehaviour
{
    [Header("Stats")]
    public int health = 2;
    
    [Header("Attack")]
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
    private EnemyAnimationController _animController;
    private bool _isExecutingTurn = false;
    private bool _hasCompletedAction = false;

    private static readonly Vector2Int[] Dir4 =
    {
        new Vector2Int(0, 1),   // North
        new Vector2Int(1, 0),   // East
        new Vector2Int(0, -1),  // South
        new Vector2Int(-1, 0),  // West
    };


    private void Awake()
    {
        if (transform.childCount > 0)
        {
            _visualMesh = transform.GetChild(0).gameObject;
        }
        else
        {
            Debug.LogWarning($"EnemyUnit '{name}' no tiene hijos. La maya debe ser un hijo del GameObject.");
        }
        
        _animController = GetComponent<EnemyAnimationController>();
        if (_animController == null)
        {
            Debug.LogWarning($"EnemyUnit '{name}' no tiene EnemyAnimationController.");
        }
    }

    private void Start()
    {
        if (currentTile == null)
            DebugLog("⚠️ currentTile not set on Start.");
        else
            SnapToTile(currentTile);
    }

    public void SnapToTile(Tile tile)
    {
        currentTile = tile;
        if (tile) transform.position = tile.transform.position;
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        DebugLog($"Enemy took damage: {amount}. HP: {health}");

        if (health <= 0)
        {
            DebugLog("Enemy defeated.");
            GameManager.Instance.RemoveEnemy(this);
            
            if (_visualMesh != null)
            {
                _visualMesh.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    public void ResetEnemy(int initialHealth)
    {
        health = initialHealth;
        _isExecutingTurn = false;
        _hasCompletedAction = false;
        
        if (_visualMesh != null)
        {
            _visualMesh.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }
        
        if (_animController != null)
        {
            _animController.ResetToIdle();
        }
        
        DebugLog($"Enemy '{name}' reset with {health} HP.");
    }

    public IEnumerator TakeTurnCoroutine()
    {
        // VALIDACIÓN 1: Prevenir ejecución múltiple
        if (_isExecutingTurn)
        {
            DebugLog("⚠️ YA ESTÁ EJECUTANDO TURNO - ABORTANDO");
            yield break;
        }

        // VALIDACIÓN 2: Verificar si está muerto
        if (IsDead)
        {
            DebugLog("💀 ESTÁ MUERTO - NO EJECUTA TURNO");
            yield break;
        }

        // VALIDACIÓN 3: Verificar referencias críticas
        if (currentTile == null)
        {
            DebugLog("❌ ERROR CRÍTICO: currentTile es NULL");
            yield break;
        }

        if (GameManager.Instance == null)
        {
            DebugLog("❌ ERROR CRÍTICO: GameManager.Instance es NULL");
            yield break;
        }

        if (GameManager.Instance.player == null)
        {
            DebugLog("❌ ERROR CRÍTICO: Player es NULL");
            yield break;
        }

        if (GameManager.Instance.player.currentTile == null)
        {
            DebugLog("❌ ERROR CRÍTICO: Player.currentTile es NULL");
            yield break;
        }

        // Marcar como ejecutando
        _isExecutingTurn = true;
        _hasCompletedAction = false;

        DebugLog("╔═══════════════════════════════════════╗");
        DebugLog($"║ 🎮 TURNO INICIADO: {name}");
        DebugLog("╚═══════════════════════════════════════╝");

        Vector2Int myPos = currentTile.gridPos;
        Vector2Int playerPos = GameManager.Instance.player.currentTile.gridPos;
        
        DebugLog($"📍 Posición enemigo: {myPos}");
        DebugLog($"🎯 Posición jugador: {playerPos}");

        // Calcular distancia Manhattan
        int manhattanDist = Mathf.Abs(myPos.x - playerPos.x) + Mathf.Abs(myPos.y - playerPos.y);
        DebugLog($"📏 Distancia Manhattan: {manhattanDist}");

        // DECISIÓN: ¿Puede atacar?
        bool isAdjacent = (manhattanDist == 1);
        DebugLog($"🔍 ¿Es adyacente (dist==1)? {isAdjacent}");

        if (isAdjacent)
        {
            // ACCIÓN: ATACAR
            DebugLog("⚔️ DECISIÓN: ATACAR AL JUGADOR");
            yield return ExecuteAttackCoroutine();
            _hasCompletedAction = true;
        }
        else
        {
            // ACCIÓN: MOVERSE
            DebugLog("🏃 DECISIÓN: MOVERSE HACIA JUGADOR");
            yield return ExecuteMoveCoroutine();
            _hasCompletedAction = true;
        }

        // Verificar que se completó una acción
        if (!_hasCompletedAction)
        {
            DebugLog("⚠️ ADVERTENCIA: No se completó ninguna acción");
        }
        else
        {
            DebugLog("✅ ACCIÓN COMPLETADA EXITOSAMENTE");
        }

        DebugLog("╚═══════════════════════════════════════╝");
        DebugLog($"  ✅ TURNO FINALIZADO: {name}");
        DebugLog("╚═══════════════════════════════════════╝");
        
        _isExecutingTurn = false;
    }

    private IEnumerator ExecuteAttackCoroutine()
    {
        var player = GameManager.Instance.player;
        if (player == null)
        {
            DebugLog("❌ ERROR: Player se volvió null durante ataque");
            yield break;
        }

        DebugLog(">>> INICIANDO SECUENCIA DE ATAQUE <<<");

        // PASO 1: ROTAR hacia el jugador
        DebugLog("  [1/5] Rotando hacia jugador...");
        Vector3 directionToPlayer = (player.transform.position - transform.position).normalized;
        
        if (directionToPlayer.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            float elapsedTime = 0f;
            float maxRotationTime = 0.5f;

            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f && elapsedTime < maxRotationTime)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            transform.rotation = targetRotation;
        }
        DebugLog("  ✓ Rotación completada");

        // PASO 2: ANIMACIÓN de ataque
        DebugLog("  [2/5] Reproduciendo animación...");
        if (_animController != null)
        {
            _animController.PlayAttack();
            DebugLog("  ✓ Animación iniciada");
        }
        else
        {
            DebugLog("  ⚠️ No hay AnimController");
        }

        // PASO 3: ESPERAR para ver la animación
        DebugLog("  [3/5] Esperando visualización (0.5s)...");
        yield return new WaitForSeconds(0.5f);

        // PASO 4: APLICAR DAÑO
        DebugLog($"  [4/5] Aplicando {attackHeatDamage} de daño de calor...");
        GameManager.Instance.ApplyEnemyAttackHeat(attackHeatDamage);
        DebugLog($"  ✓ Daño aplicado (Heat actual: {GameManager.Instance.heat})");

        // PASO 5: EFECTO VISUAL
        DebugLog("  [5/5] Creando efecto visual...");
        if (attackEffectPrefab != null && player != null)
        {
            GameObject effect = Instantiate(
                attackEffectPrefab, 
                player.transform.position, 
                Quaternion.identity
            );
            Destroy(effect, attackEffectDuration);
            DebugLog($"  ✓ Efecto creado en {player.transform.position}");
        }
        else
        {
            DebugLog("  ⚠️ No hay attackEffectPrefab o player es null");
        }

        // PASO FINAL: Pausa y volver a idle
        yield return new WaitForSeconds(0.3f);
        if (_animController != null)
        {
            _animController.SetMoving(false);
        }

        DebugLog(">>> ATAQUE COMPLETADO <<<");
    }

    private IEnumerator ExecuteMoveCoroutine()
    {
        var player = GameManager.Instance.player;
        if (player?.currentTile == null || currentTile == null)
        {
            DebugLog("❌ ERROR: Referencias null durante movimiento");
            yield break;
        }

        DebugLog(">>> INICIANDO SECUENCIA DE MOVIMIENTO <<<");

        // PASO 1: BUSCAR mejor casilla
        DebugLog("  [1/5] Buscando mejor casilla...");
        Tile bestTile = FindNextStepAStar();
        
        if (bestTile == null)
        {
            DebugLog("  ⚠️ NO HAY CASILLA VÁLIDA - permaneciendo quieto");
            yield return new WaitForSeconds(0.5f);
            DebugLog(">>> MOVIMIENTO CANCELADO (sin opciones) <<<");
            yield break;
        }

        DebugLog($"  ✓ Mejor casilla encontrada: {bestTile.gridPos}");

        // PASO 2: ROTAR hacia destino
        DebugLog("  [2/5] Rotando hacia destino...");
        Vector3 direction = (bestTile.transform.position - transform.position).normalized;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float elapsedTime = 0f;
            float maxRotationTime = 0.5f;

            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f && elapsedTime < maxRotationTime)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            transform.rotation = targetRotation;
        }
        DebugLog("  ✓ Rotación completada");

        // PASO 3: ACTIVAR animación de correr
        DebugLog("  [3/5] Activando animación de correr...");
        if (_animController != null)
        {
            _animController.SetMoving(true);
            DebugLog("  ✓ Animación activada");
        }

        // PASO 4: MOVERSE a la casilla
        DebugLog($"  [4/5] Moviéndose de {currentTile.gridPos} a {bestTile.gridPos}...");
        Vector3 startPos = transform.position;
        Vector3 targetPos = bestTile.transform.position;
        float moveProgress = 0f;

        if (moveSpeed <= 0.01f)
        {
            DebugLog("❌ moveSpeed too low. Teleporting to tile.");
            transform.position = targetPos;
        }
        else
        {
            float moveDuration = Vector3.Distance(startPos, targetPos) / moveSpeed;

            while (moveProgress < moveDuration)
            {
                moveProgress += Time.deltaTime;
                float t = Mathf.Clamp01(moveProgress / moveDuration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            transform.position = targetPos;
            currentTile = bestTile;
            DebugLog($"  ✓ Llegó a destino: {bestTile.gridPos}");

            // PASO 5: DESACTIVAR animación (volver a idle)
            DebugLog("  [5/5] Desactivando animación de correr...");
            if (_animController != null)
            {
                _animController.SetMoving(false);
                DebugLog("  ✓ Vuelto a Idle");
            }

            yield return new WaitForSeconds(0.2f);
            DebugLog(">>> MOVIMIENTO COMPLETADO <<<");
        }
    }

    private bool IsTileOccupiedByOtherEnemy(Tile tile)
    {
        if (GameManager.Instance == null || tile == null) return false;

        foreach (var enemy in GameManager.Instance.enemies)
        {
            if (enemy == null || enemy.IsDead || enemy == this) continue;
            if (enemy.currentTile == tile) return true;
        }

        return false;
    }

    private Tile FindNextStepAStar()
    {
        if (currentTile == null || GameManager.Instance?.player?.currentTile == null)
        {
            DebugLog("FindNextStepAStar: Null references. Aborting.");
            return null;
        }

        Vector2Int start = currentTile.gridPos;
        Vector2Int playerPos = GameManager.Instance.player.currentTile.gridPos;

        // 1) Build goal set: any walkable, unoccupied tile adjacent to player (not the player's tile)
        List<Vector2Int> goals = new List<Vector2Int>(4);
        for (int i = 0; i < 4; i++)
            goals.Add(playerPos + Dir4[i]);

        // Filter goals (must exist, walkable, not occupied by other enemy, not player's tile)
        goals.RemoveAll(g =>
        {
            Tile t = BoardManager.Instance.GetTile(g);
            if (t == null) return true;
            if (!t.IsWalkable) return true;
            if (t == GameManager.Instance.player.currentTile) return true;
            if (IsTileOccupiedByOtherEnemy(t)) return true;
            return false;
        });

        if (goals.Count == 0)
        {
            DebugLog("FindNextStepAStar: No valid goal tiles adjacent to player.");
            return null;
        }

        // 2) Run A* to the closest goal (multi-goal A*)
        List<Vector2Int> path = AStarPathToAnyGoal(start, goals, playerPos);

        if (path == null || path.Count < 2)
        {
            DebugLog("FindNextStepAStar: No path found (or already at goal).");
            return null;
        }

        // path[0] = start, path[1] = next step
        Vector2Int nextPos = path[1];
        Tile nextTile = BoardManager.Instance.GetTile(nextPos);

        if (nextTile == null)
        {
            DebugLog("FindNextStepAStar: Next tile resolved to null. Unexpected.");
            return null;
        }

        DebugLog($"FindNextStepAStar: Next step is {nextTile.gridPos} (path length {path.Count}).");
        return nextTile;
    }

    // ---------------- A* Implementation ----------------

    private class AStarNode
    {
        public Vector2Int pos;
        public int g;
        public int h;
        public int f => g + h;
        public AStarNode parent;

        public AStarNode(Vector2Int pos, int g, int h, AStarNode parent)
        {
            this.pos = pos;
            this.g = g;
            this.h = h;
            this.parent = parent;
        }
    }

    private List<Vector2Int> AStarPathToAnyGoal(Vector2Int start, List<Vector2Int> goals, Vector2Int playerPos)
    {
        // Open set as list (fine for jam-sized grids). For bigger grids you’d use a priority queue.
        List<AStarNode> open = new List<AStarNode>();
        Dictionary<Vector2Int, AStarNode> openMap = new Dictionary<Vector2Int, AStarNode>();
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();

        // For faster "is goal" checks
        HashSet<Vector2Int> goalSet = new HashSet<Vector2Int>(goals);

        AStarNode startNode = new AStarNode(start, 0, HeuristicToClosestGoal(start, goals), null);
        open.Add(startNode);
        openMap[start] = startNode;

        while (open.Count > 0)
        {
            // Pick node with lowest f (tie-breaker: lowest h)
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].f < open[bestIndex].f ||
                    (open[i].f == open[bestIndex].f && open[i].h < open[bestIndex].h))
                {
                    bestIndex = i;
                }
            }

            AStarNode current = open[bestIndex];
            open.RemoveAt(bestIndex);
            openMap.Remove(current.pos);

            if (goalSet.Contains(current.pos))
            {
                // Found a goal, reconstruct full path
                return ReconstructPath(current);
            }

            closed.Add(current.pos);

            // Explore neighbors (N/E/S/W)
            for (int i = 0; i < 4; i++)
            {
                Vector2Int npos = current.pos + Dir4[i];

                if (closed.Contains(npos)) continue;
                if (npos == playerPos) continue;

                Tile tile = BoardManager.Instance.GetTile(npos);
                if (tile == null) continue;
                if (!tile.IsWalkable) continue;
                if (IsTileOccupiedByOtherEnemy(tile)) continue;

                int tentativeG = current.g + 1;

                if (openMap.TryGetValue(npos, out AStarNode existing))
                {
                    if (tentativeG >= existing.g) continue;

                    existing.g = tentativeG;
                    existing.parent = current;
                    existing.h = HeuristicToClosestGoal(npos, goals);
                    continue;
                }

                AStarNode node = new AStarNode(npos, tentativeG, HeuristicToClosestGoal(npos, goals), current);
                open.Add(node);
                openMap[npos] = node;
            }

        }

        // No path found
        return null;
    }

    private int HeuristicToClosestGoal(Vector2Int p, List<Vector2Int> goals)
    {
        int best = int.MaxValue;
        for (int i = 0; i < goals.Count; i++)
        {
            int d = Mathf.Abs(p.x - goals[i].x) + Mathf.Abs(p.y - goals[i].y);
            if (d < best) best = d;
        }
        return best;
    }

    private List<Vector2Int> ReconstructPath(AStarNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        AStarNode cur = endNode;
        while (cur != null)
        {
            path.Add(cur.pos);
            cur = cur.parent;
        }
        path.Reverse();
        return path;
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[{name}] {message}");
        }
    }
}