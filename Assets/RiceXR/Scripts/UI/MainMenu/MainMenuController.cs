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

    /// <summary>
    /// Pose de la raiz en la escena. Esta pensada para un jugador en el origen
    /// mirando hacia +Z, que es donde arranca el casco.
    /// </summary>
    private Vector3 _homePosition;
    private Quaternion _homeRotation;

    private void Awake()
    {
        if (mainMenuPanel != null)
            _mainMenuOriginalScale = mainMenuPanel.localScale;

        _homePosition = transform.position;
        _homeRotation = transform.rotation;
    }

    void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.gameObject.SetActive(true);
        optionsController?.Close();
        if (leaderboardController != null)
            leaderboardController.Populate();
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
        if (startAndOmitSelectPlanesButton != null)
            startAndOmitSelectPlanesButton.onClick.AddListener(OnStartAndOmitSelectPlanesClicked);
        if (OptionsButton != null)
            OptionsButton.onClick.AddListener(OnOptionsClicked);

        RefreshPlaneSkipButton();
    }

    private void OnEnable()
    {
        GameEventBus.OnReturnToMenuRequested += HandleReturnToMenuRequested;
        _startFlowTriggered = false;
        SetStartButtonsInteractable(true);
        RefreshPlaneSkipButton();
    }

    private void OnStartClicked()
    {
        BeginStartFlow(false);
    }

    private void OnStartAndOmitSelectPlanesClicked()
    {
        // El boton se mantiene oculto si no existe un plano valido. Si el estado
        // cambio entre frames, el inicio normal conserva el flujo seguro.
        BeginStartFlow(HasSelectedPlane());
    }

    private void BeginStartFlow(bool skipPlaneSelection)
    {
        if (_startFlowTriggered) return;
        var manager = SceneInteractionManager.Instance;
        var catalog = DiseaseSelectionSystem.Instance != null ? DiseaseSelectionSystem.Instance.catalog : null;
        string error = "Falta SceneInteractionManager.";
        if (manager == null || !manager.CanStartSession(catalog, out error))
        {
            Debug.LogError($"[MainMenu] No se puede iniciar la sesión: {error}", this);
            return;
        }
        _startFlowTriggered = true;
        SetStartButtonsInteractable(false);

        optionsController?.Close();

        // Publicar al bus — GameFlowController escucha esto y orquesta todo
        GameEventBus.PublishSessionStartRequested(skipPlaneSelection);

        if (mainMenuPanel != null)
        {
            mainMenuPanel.DOKill();
            mainMenuPanel
                .DOScale(Vector3.zero, animDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => mainMenuPanel.gameObject.SetActive(false));
        }
    }

    private void HandleReturnToMenuRequested()
    {
        _startFlowTriggered = false;
        SetStartButtonsInteractable(true);
        RefreshPlaneSkipButton();
    }

    private bool HasSelectedPlane()
    {
        return SceneInteractionManager.Instance != null
            && SceneInteractionManager.Instance.HasSelectedPlane;
    }

    private void RefreshPlaneSkipButton()
    {
        if (startAndOmitSelectPlanesButton != null)
            startAndOmitSelectPlanesButton.gameObject.SetActive(HasSelectedPlane());
    }

    private void SetStartButtonsInteractable(bool interactable)
    {
        if (startButton != null)
            startButton.interactable = interactable;
        if (startAndOmitSelectPlanesButton != null)
            startAndOmitSelectPlanesButton.interactable = interactable;
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
        PlaceInFrontOfPlayer();
        mainMenuPanel.DOKill();
        mainMenuPanel.gameObject.SetActive(true);
        mainMenuPanel.localScale = Vector3.zero;
        mainMenuPanel
            .DOScale(_mainMenuOriginalScale, animDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                SetStartButtonsInteractable(true);
                RefreshPlaneSkipButton();
                leaderboardController?.Populate();
            });
    }

    /// <summary>
    /// Lleva el menu completo (panel, rayo y panel de opciones, que cuelgan de esta
    /// raiz) frente al jugador. Aplica a la pose original el giro y desplazamiento
    /// que separan el origen de la posicion actual de la cabeza: conserva la
    /// distancia y la altura con las que se diseño el menu.
    /// </summary>
    private void PlaceInFrontOfPlayer()
    {
        if (Camera.main == null) return;

        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;

        Quaternion yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
        transform.SetPositionAndRotation(
            new Vector3(cam.position.x, 0f, cam.position.z) + yaw * _homePosition,
            yaw * _homeRotation);
    }

    private void OnDisable()
    {
        GameEventBus.OnReturnToMenuRequested -= HandleReturnToMenuRequested;
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (startAndOmitSelectPlanesButton != null)
            startAndOmitSelectPlanesButton.onClick.RemoveListener(OnStartAndOmitSelectPlanesClicked);
        if (OptionsButton != null) OptionsButton.onClick.RemoveListener(OnOptionsClicked);
        mainMenuPanel?.DOKill();
    }
}
