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
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);

        if (SessionMetricsTracker.Instance != null)
            SessionMetricsTracker.Instance.StartSession();

        if (sceneInteractionManager != null)
            sceneInteractionManager.WaitForStartSignal();
        else
            Debug.LogWarning("[MainMenu] No se encontró SceneInteractionManager.");
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
    }
}
