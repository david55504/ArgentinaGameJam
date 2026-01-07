using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Refs")]
    public UIHud hud;
    public UILosePanel losePanel;

    //[Header("Optional Panels (later)")]
    // public UILosePanel losePanel;
    // public UIWinPanel winPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate UIManager detected. Destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // Subscribe when active
        TrySubscribe();
    }

    private void Start()
    {
        // In case GameManager wasn't ready in OnEnable (script execution order),
        // we try again at Start.
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (GameManager.Instance == null) return;

        // Avoid double subscription
        Unsubscribe();

        GameManager.Instance.TurnStateChanged += OnTurnStateChanged;
        GameManager.Instance.HeatChanged += OnHeatChanged;
        GameManager.Instance.ActionsChanged += OnActionsChanged;
        GameManager.Instance.GameLost += OnGameLost;
        GameManager.Instance.GameWon += OnGameWon;
        GameManager.Instance.GameReset += OnGameReset;

        // Force initial sync
        ForceRefreshFromGame();
    }

    private void Unsubscribe()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.TurnStateChanged -= OnTurnStateChanged;
        GameManager.Instance.HeatChanged -= OnHeatChanged;
        GameManager.Instance.ActionsChanged -= OnActionsChanged;
        GameManager.Instance.GameLost -= OnGameLost;
        GameManager.Instance.GameWon -= OnGameWon;
        GameManager.Instance.GameReset -= OnGameReset;
    }

    private void ForceRefreshFromGame()
    {
        if (hud == null || GameManager.Instance == null) return;

        var gm = GameManager.Instance;
        hud.RefreshAll(
            isPlayerTurn: gm.state == TurnState.PlayerTurn,
            heat: gm.heat,
            maxHeat: gm.maxHeat,
            actionsLeft: gm.actionsLeft,
            actionsPerTurn: gm.actionsPerTurn
        );
    }

    // ---------------- Event Handlers ----------------

    private void OnTurnStateChanged(TurnState newState)
    {
        if (hud == null) return;
        bool isPlayer = newState == TurnState.PlayerTurn;
        hud.RefreshTurn(isPlayer);
    }

    private void OnHeatChanged(int heat, int maxHeat)
    {
        if (hud == null) return;
        hud.RefreshHeat(heat, maxHeat);
    }

    private void OnActionsChanged(int actionsLeft, int actionsPerTurn)
    {
        if (hud == null) return;
        hud.RefreshActions(actionsLeft, actionsPerTurn);
    }

    private void OnGameReset()
    {
        Debug.Log("UI received GameReset.");
        ForceRefreshFromGame();
        losePanel?.Hide();
        // winPanel?.Hide();
    }

    private void OnGameLost(string msg)
    {
        Debug.Log($"UI received GameLost: {msg}");
        losePanel?.Show(msg);
    }

    private void OnGameWon(string msg)
    {
        Debug.Log($"UI received GameWon: {msg}");
        // winPanel?.Show(msg);
    }
}

