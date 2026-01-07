using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UILosePanel : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text messageText;

    [Header("Retry")]
    public bool reloadSceneOnRetry = false;

    private void Awake()
    {
        Hide();
    }

    public void Show(string message)
    {
        if (titleText != null) titleText.text = "GAME OVER";
        if (messageText != null) messageText.text = string.IsNullOrWhiteSpace(message)
            ? "You lost."
            : message;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // Conecta este método al OnClick del botón Retry
    public void OnRetryPressed()
    {
        Debug.Log("Retry pressed.");

        Hide();

        if (reloadSceneOnRetry)
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetRun();
        }
        else
        {
            Debug.LogWarning("GameManager.Instance is null. Reloading scene as fallback.");
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
