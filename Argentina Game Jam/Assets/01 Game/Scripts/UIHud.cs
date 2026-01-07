using TMPro;
using UnityEngine;

public class UIHud : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text turnText;
    public TMP_Text heatText;
    public TMP_Text actionsText;

    [Header("Labels")]
    public string playerTurnLabel = "YOUR TURN";
    public string enemyTurnLabel = "ENEMY TURN";

    public void RefreshTurn(bool isPlayerTurn)
    {
        if (turnText == null) return;
        turnText.text = isPlayerTurn ? playerTurnLabel : enemyTurnLabel;
    }

    public void RefreshHeat(int heat, int maxHeat)
    {
        if (heatText == null) return;
        heatText.text = $"Heat: {heat}/{maxHeat}";
    }

    public void RefreshActions(int actionsLeft, int actionsPerTurn)
    {
        if (actionsText == null) return;
        actionsText.text = $"Actions: {actionsLeft}/{actionsPerTurn}";
    }

    public void RefreshAll(bool isPlayerTurn, int heat, int maxHeat, int actionsLeft, int actionsPerTurn)
    {
        RefreshTurn(isPlayerTurn);
        RefreshHeat(heat, maxHeat);
        RefreshActions(actionsLeft, actionsPerTurn);
    }
}
