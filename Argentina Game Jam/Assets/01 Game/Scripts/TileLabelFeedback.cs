using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Highlight")]
    public Color validColor = Color.green;
    public Color invalidColor = Color.red;
    [Range(0f, 1f)] public float blend = 0.65f;

    [Header("Tooltip Anchor")]
    public Vector3 worldOffset = new Vector3(0f, 0.2f, 0f); // lift slightly above tile

    private Tile _hoveredTile;
    private Renderer _hoveredRenderer;
    private MaterialPropertyBlock _mpb;
    private Color _originalColor;

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
            return;
        }

        // Only show during player's turn
        if (GameManager.Instance.state != TurnState.PlayerTurn)
        {
            ClearHover();
            return;
        }

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

        ApplyHighlight(isValid ? validColor : invalidColor);

        if (tileLabel != null)
        {
            // Anchor tooltip near the tile
            Vector3 anchor = tile.transform.position + worldOffset;
            tileLabel.ShowAtWorld(anchor, msg);
        }
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

        bool canMove = gm.CanMoveToTile(tile); // IMPORTANTE: usa tu check completo (adyacencia/acciones/turno + CanEnterTile)
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
            _hoveredRenderer.SetPropertyBlock(null);

        _hoveredTile = null;
        _hoveredRenderer = null;

        if (tileLabel != null)
            tileLabel.Hide();
    }
}

