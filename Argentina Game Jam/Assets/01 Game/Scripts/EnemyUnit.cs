using System.Collections;
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
        Tile bestTile = FindBestAdjacentTile();
        
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

    private Tile FindBestAdjacentTile()
    {
        if (currentTile == null || GameManager.Instance?.player?.currentTile == null)
        {
            DebugLog("    ❌ FindBest: referencias null");
            return null;
        }

        Vector2Int playerPos = GameManager.Instance.player.currentTile.gridPos;
        Vector2Int myPos = currentTile.gridPos;

        DebugLog($"    🔍 Evaluando casillas desde {myPos} hacia {playerPos}");

        // Casillas adyacentes (sin diagonales)
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Norte
            new Vector2Int(1, 0),   // Este
            new Vector2Int(0, -1),  // Sur
            new Vector2Int(-1, 0)   // Oeste
        };

        Tile bestTile = null;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int checkPos = myPos + directions[i];
            
            // Verificar que existe la casilla
            Tile tile = BoardManager.Instance.GetTile(checkPos);
            if (tile == null)
            {
                DebugLog($"      [{i}] {checkPos}: No existe");
                continue;
            }

            // Verificar que es caminable
            if (!tile.IsWalkable)
            {
                DebugLog($"      [{i}] {checkPos}: Bloqueada");
                continue;
            }
            
            // Verificar que no está ocupada por otro enemigo
            if (IsTileOccupiedByOtherEnemy(tile))
            {
                DebugLog($"      [{i}] {checkPos}: Ocupada por enemigo");
                continue;
            }
            
            // Verificar que no es la casilla del jugador
            if (tile == GameManager.Instance.player.currentTile)
            {
                DebugLog($"      [{i}] {checkPos}: Es del jugador");
                continue;
            }

            // Calcular distancia Manhattan al jugador
            int distance = Mathf.Abs(checkPos.x - playerPos.x) + 
                          Mathf.Abs(checkPos.y - playerPos.y);

            DebugLog($"      [{i}] {checkPos}: VÁLIDA (dist={distance})");

            // Elegir la que más acerca al jugador
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTile = tile;
                DebugLog($"           ⭐ NUEVA MEJOR (dist={distance})");
            }
        }

        if (bestTile != null)
        {
            DebugLog($"    ✅ RESULTADO: {bestTile.gridPos} (dist={bestDistance})");
        }
        else
        {
            DebugLog($"    ❌ RESULTADO: Ninguna casilla válida");
        }

        return bestTile;
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

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[{name}] {message}");
        }
    }
}