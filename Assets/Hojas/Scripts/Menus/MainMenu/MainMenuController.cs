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
    [SerializeField] private Transform mainMenuPanel;

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
    private Vector3 _mainMenuOriginalScale = Vector3.one;

    private void Awake()
    {
        if (mainMenuPanel != null)
            _mainMenuOriginalScale = mainMenuPanel.localScale;
    }

    void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.gameObject.SetActive(true);
        optionsController?.Close();
        if (leaderboardController != null)
            leaderboardController.Populate();
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
        if (OptionsButton != null)
            OptionsButton.onClick.AddListener(OnOptionsClicked);
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

        optionsController?.Close();

        // Publicar al bus — GameFlowController escucha esto y orquesta todo
        GameEventBus.PublishSessionStartRequested();

        if (mainMenuPanel != null)
        {
            mainMenuPanel.DOKill();
            mainMenuPanel
                .DOScale(Vector3.zero, animDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => mainMenuPanel.gameObject.SetActive(false));
        }

        // Legacy: mantener evento estático para GameNarrationController
        StartFlowRequested?.Invoke();
    }

    private void OnOptionsClicked()
    {
        if (_startFlowTriggered) return;
        optionsController?.ToggleFromMainMenu();
    }

    /// <summary>
    /// Vuelve a mostrar el contenido del menú principal y restablece el botón de inicio.
    /// </summary>
    public void ShowMainMenu()
    {
        if (mainMenuPanel == null) return;

        _startFlowTriggered = false;
        mainMenuPanel.DOKill();
        mainMenuPanel.gameObject.SetActive(true);
        mainMenuPanel.localScale = Vector3.zero;
        mainMenuPanel
            .DOScale(_mainMenuOriginalScale, animDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                if (startButton != null)
                    startButton.interactable = true;
                leaderboardController?.Populate();
            });
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (OptionsButton != null) OptionsButton.onClick.RemoveListener(OnOptionsClicked);
        mainMenuPanel?.DOKill();
    }
}
