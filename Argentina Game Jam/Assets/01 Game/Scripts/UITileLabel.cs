using TMPro;
using UnityEngine;

public class UITileLabel : MonoBehaviour
{
    [Header("UI")]
    public RectTransform tooltipRect;
    public TMP_Text tooltipText;

    [Header("Positioning")]
    public Camera cam;
    public Vector2 screenOffset = new Vector2(24f, 24f);
    public bool clampToScreen = true;
    public Vector2 screenPadding = new Vector2(12f, 12f);

    private void Awake()
    {
        Hide();
    }

    public void ShowAtWorld(Vector3 worldPos, string msg)
    {
        if (tooltipRect == null || tooltipText == null || cam == null) return;

        tooltipText.text = msg;

        Vector3 screen = cam.WorldToScreenPoint(worldPos);
        Vector2 pos = (Vector2)screen + screenOffset;

        if (clampToScreen)
        {
            float w = tooltipRect.rect.width;
            float h = tooltipRect.rect.height;

            float minX = screenPadding.x;
            float maxX = Screen.width - screenPadding.x - w;

            float minY = screenPadding.y;
            float maxY = Screen.height - screenPadding.y - h;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }

        tooltipRect.position = pos;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
