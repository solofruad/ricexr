using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MENÚ PRINCIPAL
/// 
/// Controla la pantalla de inicio. El leaderboard siempre está visible.
/// Solo tiene un botón: "Iniciar Pruebas".
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Botones")]
    [SerializeField] private Button startButton;

    [Header("Referencias")]
    [SerializeField] private SceneInteractionManager sceneInteractionManager;
    [SerializeField] private LeaderboardController leaderboardController;

    [Header("Animación")]
    [SerializeField] private float animDuration = 0.3f;

    void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // Poblar el leaderboard al inicio (siempre visible)
        if (leaderboardController != null)
            leaderboardController.Populate();

        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnStartClicked()
    {
        if (SessionMetricsTracker.Instance != null)
            SessionMetricsTracker.Instance.StartSession();

        if (sceneInteractionManager != null)
            sceneInteractionManager.WaitForStartSignal();
        else
            Debug.LogWarning("[MainMenu] No se encontró SceneInteractionManager.");

        // Ocultar el menú con animación
        if (mainMenuPanel != null)
            StartCoroutine(HidePanel());
    }

    private IEnumerator HidePanel()
    {
        float elapsed = 0f;
        Vector3 originalScale = mainMenuPanel.transform.localScale;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            mainMenuPanel.transform.localScale = Vector3.LerpUnclamped(originalScale, Vector3.zero, easedT);
            yield return null;
        }
        mainMenuPanel.transform.localScale = Vector3.zero;
        mainMenuPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
    }
}
