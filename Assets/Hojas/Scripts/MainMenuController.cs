using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// MENU PRINCIPAL
///
/// Controla la pantalla de inicio. El leaderboard siempre esta visible.
/// Solo tiene un boton: "Iniciar Pruebas".
/// Flujo al presionar iniciar:
///   1. WaitForStartSignal() en SceneInteractionManager (spawnea planos MR)
///   2. Oculta el panel del menu principal
///   3. El tutorial visual inicia al seleccionar el plano y completar onboarding
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public static event Action StartFlowRequested;

    [Header("Paneles")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Botones")]
    [SerializeField] private Button startButton;

    [Header("Referencias")]
    [SerializeField] private SceneInteractionManager sceneInteractionManager;
    [SerializeField] private LeaderboardController leaderboardController;

    [Header("Animacion")]
    [SerializeField] private float animDuration = 0.3f;

    private bool _startFlowTriggered;

    void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (leaderboardController != null)
            leaderboardController.Populate();
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnEnable()
    {
        _startFlowTriggered = false;
        if (startButton != null) startButton.interactable = true;
    }

    private void OnStartClicked()
    {
        if (_startFlowTriggered) return;
        _startFlowTriggered = true;

        if (startButton != null)
            startButton.interactable = false;

        if (sceneInteractionManager != null)
        {
            sceneInteractionManager.PrepareForNextSession();
            sceneInteractionManager.WaitForStartSignal();
        }
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

        StartFlowRequested?.Invoke();
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        mainMenuPanel?.transform.DOKill();
    }
}