using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TileLabelFeedback : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference pointAction; // Mouse position

    [Header("Raycast")]
    public Camera cam;
    public LayerMask tileLayer;
    public float rayDistance = 300f;

    [Header("Refs")]
    public PlayerController player;
    public UITileLabel tileLabel;

    [Header("Highlight Colors")]
    public Color validColor = Color.green;
    public Color invalidColor = Color.red;
    public Color moveRangeColor = new Color(0.3f, 0.6f, 1f, 0.4f); // Azul con transparencia
    [Range(0f, 1f)] public float blend = 0.65f;

    [Header("Tooltip Anchor")]
    public Vector3 worldOffset = new Vector3(0f, 0.2f, 0f); // lift slightly above tile

    private Tile _hoveredTile;
    private Renderer _hoveredRenderer;
    private MaterialPropertyBlock _mpb;
    private Color _originalColor;

    // Sistema de rango
    private List<Tile> _rangeHighlightedTiles = new List<Tile>();
    private Dictionary<Tile, Renderer> _rangeRenderers = new Dictionary<Tile, Renderer>();
    private Dictionary<Tile, Color> _rangeOriginalColors = new Dictionary<Tile, Color>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Standard

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (pointAction != null) pointAction.action.Enable();
    }

    private void Update()
    {
        if (cam == null || player == null || GameManager.Instance == null)
        {
            ClearHover();
            ClearRangeHighlights();
            return;
        }

        // Only show during player's turn
        if (GameManager.Instance.state != TurnState.PlayerTurn)
        {
            ClearHover();
            ClearRangeHighlights();
            return;
        }

        // ✨ NUEVA LÓGICA: Actualizar rango de movimiento
        UpdateMovementRange();

        // Raycast para hover
        Vector2 screenPos = pointAction.action.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, tileLayer))
        {
            ClearHover();
            return;
        }

        if (!hit.collider.TryGetComponent<Tile>(out var tile))
        {
            ClearHover();
            return;
        }

        if (tile != _hoveredTile)
        {
            ClearHover();
            SetHovered(tile);
        }

        var (isValid, msg) = BuildHoverMessage(tile);

        // ✨ PRIORIDAD: El hover siempre tapa el color de rango
        ApplyHighlight(isValid ? validColor : invalidColor);

        if (tileLabel != null)
        {
            // Anchor tooltip near the tile
            Vector3 anchor = tile.transform.position + worldOffset;
            tileLabel.ShowAtWorld(anchor, msg);
        }
    }

    private void UpdateMovementRange()
    {
        // Limpiar resaltados anteriores
        ClearRangeHighlights();

        if (player == null || player.currentTile == null) return;
        if (BoardManager.Instance == null) return;
        if (GameManager.Instance == null) return;

        Vector2Int playerPos = player.currentTile.gridPos;

        // Direcciones en cruz (4D)
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Arriba
            new Vector2Int(0, -1),  // Abajo
            new Vector2Int(1, 0),   // Derecha
            new Vector2Int(-1, 0)   // Izquierda
        };

        foreach (var dir in directions)
        {
            Vector2Int adjacentPos = playerPos + dir;
            Tile adjacentTile = BoardManager.Instance.GetTile(adjacentPos);

            if (adjacentTile == null) continue;

            // Solo resaltar casillas adyacentes válidas
            if (!BoardManager.Instance.AreAdjacent4D(playerPos, adjacentPos)) continue;

            // ✨ CRÍTICO: Solo resaltar si el jugador PUEDE entrar a esa casilla
            if (!GameManager.Instance.CanEnterTile(adjacentTile)) continue;

            // Guardar para aplicar color de rango
            Renderer tileRenderer = adjacentTile.GetComponentInChildren<Renderer>();
            if (tileRenderer == null) continue;

            _rangeHighlightedTiles.Add(adjacentTile);
            _rangeRenderers[adjacentTile] = tileRenderer;

            // Guardar color original
            var mat = tileRenderer.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId))
                    _rangeOriginalColors[adjacentTile] = mat.GetColor(BaseColorId);
                else if (mat.HasProperty(ColorId))
                    _rangeOriginalColors[adjacentTile] = mat.GetColor(ColorId);
                else
                    _rangeOriginalColors[adjacentTile] = Color.white;
            }
            else
            {
                _rangeOriginalColors[adjacentTile] = Color.white;
            }

            // Aplicar color de rango SOLO si no es la casilla hovereada
            if (adjacentTile != _hoveredTile)
            {
                ApplyRangeHighlight(adjacentTile, tileRenderer, _rangeOriginalColors[adjacentTile]);
            }
        }
    }

    private void ApplyRangeHighlight(Tile tile, Renderer renderer, Color originalColor)
    {
        if (renderer == null) return;

        Color final = Color.Lerp(originalColor, moveRangeColor, blend);

        renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, final);
        _mpb.SetColor(ColorId, final);
        renderer.SetPropertyBlock(_mpb);
    }

    private void ClearRangeHighlights()
    {
        foreach (var tile in _rangeHighlightedTiles)
        {
            if (_rangeRenderers.TryGetValue(tile, out var renderer) && renderer != null)
            {
                // No limpiar si es la casilla hovereada (tiene prioridad)
                if (tile == _hoveredTile) continue;

                renderer.SetPropertyBlock(null);
            }
        }

        _rangeHighlightedTiles.Clear();
        _rangeRenderers.Clear();
        _rangeOriginalColors.Clear();
    }

    private void SetHovered(Tile tile)
    {
        _hoveredTile = tile;
        _hoveredRenderer = tile.GetComponentInChildren<Renderer>();

        if (_hoveredRenderer == null) return;

        var mat = _hoveredRenderer.sharedMaterial;
        if (mat != null)
        {
            if (mat.HasProperty(BaseColorId)) _originalColor = mat.GetColor(BaseColorId);
            else if (mat.HasProperty(ColorId)) _originalColor = mat.GetColor(ColorId);
            else _originalColor = Color.white;
        }
        else _originalColor = Color.white;
    }

    private (bool isValid, string message) BuildHoverMessage(Tile tile)
    {
        var gm = GameManager.Instance;

        // 1) Blocked tile: always show "Blocked"
        if (!tile.IsWalkable)
            return (false, "Blocked");

        // 2) Enemy on tile => intent is ATTACK (always show heat cost)
        EnemyUnit enemy = gm.GetEnemyOnTile(tile);
        if (enemy != null)
        {
            bool canAttack = gm.CanAttackEnemyOnTile(tile);
            int heatCost = gm.attackHeatCost;

            // Message always shows the effect, even if not possible right now
            return (canAttack, $"Attack: +{heatCost} Heat");
        }

        // 3) Free walkable tile => intent is MOVE (always show enter heat delta)
        int delta = tile.heatDeltaOnEnter;
        string signed = delta >= 0 ? $"+{delta}" : delta.ToString();

        bool canMove = gm.CanMoveToTile(tile);
        return (canMove, $"Move: {signed} Heat");
    }

    private void ApplyHighlight(Color targetColor)
    {
        if (_hoveredRenderer == null) return;

        Color final = Color.Lerp(_originalColor, targetColor, blend);

        _hoveredRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, final);
        _mpb.SetColor(ColorId, final);
        _hoveredRenderer.SetPropertyBlock(_mpb);
    }

    private void ClearHover()
    {
        if (_hoveredRenderer != null)
        {
            // Si la casilla hovereada estaba en el rango, restaurar color de rango
            if (_hoveredTile != null && _rangeHighlightedTiles.Contains(_hoveredTile))
            {
                if (_rangeOriginalColors.TryGetValue(_hoveredTile, out var originalColor))
                {
                    ApplyRangeHighlight(_hoveredTile, _hoveredRenderer, originalColor);
                }
            }
            else
            {
                // Si no estaba en rango, limpiar completamente
                _hoveredRenderer.SetPropertyBlock(null);
            }
        }

        _hoveredTile = null;
        _hoveredRenderer = null;

        if (tileLabel != null)
            tileLabel.Hide();
    }
}