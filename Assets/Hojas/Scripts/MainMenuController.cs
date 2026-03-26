using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// MENU PRINCIPAL
///
/// Controla la pantalla de inicio. El leaderboard siempre esta visible.
/// Solo tiene un boton: "Iniciar Pruebas".
/// Flujo al presionar iniciar:
///   1. StartSession() en SessionMetricsTracker
///   2. WaitForStartSignal() en SceneInteractionManager (spawnea planos MR)
///   3. Oculta el panel del menu principal
///   4. El tutorial visual inicia al seleccionar el plano
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

    [Header("Animacion")]
    [SerializeField] private float animDuration = 0.3f;

    void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (leaderboardController != null)
            leaderboardController.Populate();
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnStartClicked()
    {
        if (SessionMetricsTracker.Instance != null)
            SessionMetricsTracker.Instance.StartSession();

        if (sceneInteractionManager != null)
            sceneInteractionManager.WaitForStartSignal();
        else
            Debug.LogWarning("[MainMenu] No se encontro SceneInteractionManager.");

        if (mainMenuPanel != null)
        {
            mainMenuPanel.transform.DOKill();
            mainMenuPanel.transform
                .DOScale(Vector3.zero, animDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => mainMenuPanel.SetActive(false));
        }
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        mainMenuPanel?.transform.DOKill();
    }
}