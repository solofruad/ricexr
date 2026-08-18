using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// MENU PRINCIPAL
///
/// Controla la pantalla de inicio. El leaderboard siempre esta visible.
/// Solo tiene un boton: "Iniciar Pruebas".
///
/// Flujo al presionar iniciar:
///   1. Publica SessionStartRequested al GameEventBus
///   2. Oculta el panel del menu principal con animación
///   3. GameFlowController escucha el evento y orquesta el flujo
/// </summary>
public class MainMenuController : MonoBehaviour
{
    /// <summary>
    /// Evento legacy — mantenido para compatibilidad con GameNarrationController.
    /// Se emite junto con GameEventBus.PublishSessionStartRequested().
    /// </summary>
    public static event Action StartFlowRequested;

    [Header("Paneles")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Botones")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button startAndOmitSelectPlanesButton;
    [SerializeField] private Button OptionsButton;

    [Header("Referencias")]
    [SerializeField] private LeaderboardController leaderboardController;
    [SerializeField] private OptionsController optionsController;


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


        // Publicar al bus — GameFlowController escucha esto y orquesta todo
        GameEventBus.PublishSessionStartRequested();

        if (mainMenuPanel != null)
        {
            mainMenuPanel.transform.DOKill();
            mainMenuPanel.transform
                .DOScale(Vector3.zero, animDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => mainMenuPanel.SetActive(false));
        }

        // Legacy: mantener evento estático para GameNarrationController
        StartFlowRequested?.Invoke();
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        mainMenuPanel?.transform.DOKill();
    }
}