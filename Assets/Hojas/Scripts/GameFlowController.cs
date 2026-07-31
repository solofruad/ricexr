using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using DG.Tweening;

/// <summary>
/// CONTROLADOR CENTRAL DEL FLUJO DE JUEGO
///
/// Única fuente de verdad para la máquina de estados del juego.
/// Orquesta las transiciones:
/// Menu → PlaneSelection → LevelIntro → TutorialDemonstration → Tutorial → Niveles → Fin.
///
/// No contiene lógica de presentación (eso es responsabilidad de UIGameListener,
/// UIMessagesController, paneles, etc.).
/// No contiene lógica de spawn de hojas (eso lo hace SceneInteractionManager).
///
/// Escucha eventos del GameEventBus (clase estática) y decide qué transición ocurre a continuación.
/// Los controladores de UI publican eventos al bus; este controlador los consume.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    [Header("Referencias de escena")]
    [SerializeField] private SceneInteractionManager sceneInteractionManager;
    [SerializeField] private LevelIntroController levelIntroController;
    [SerializeField] private GuidedTutorialController guidedTutorialController;
    // Retained for deserializing legacy scenes; this flow no longer invokes the demo.
    [SerializeField] private TutorialDemonstrationController tutorialDemonstrationController;
    [SerializeField] private TutorialPanelController tutorialPanelController;
    [SerializeField] private PlaneConfigurationSpawner planeSpawner;
    [SerializeField] private MainMenuController mainMenuController;

    [Header("Configuración de flujo")]
    [SerializeField] private bool runLevelIntro = true;
    [SerializeField] private bool runTutorialDemonstration = false;
    [SerializeField] private int tutorialLevelIndex = 0;

    [Header("Tiempos")]
    [SerializeField] private float levelTransitionDelay = 1.1f;
    [SerializeField] private float scaleDuration = 0.5f;

    public FlowState CurrentState { get; private set; } = FlowState.None;
    public int TutorialLevelIndex => tutorialLevelIndex;

    // ── Estado interno ───────────────────────────────────────────────────────
    private Vector3 _selectedPlanePosition;
    private Quaternion _selectedPlaneRotation;
    private Vector3 _selectedPlaneScale;
    private bool _planeSelected;
    private bool _sessionMetricsStarted;
    private bool _waitingForAllPlants;
    private bool _waitingAnalysisToSpawnLevel;
    private bool _tutorialCompletionHandled;

    private Coroutine _levelTransitionCoroutine;

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        AutoResolveDependencies();
        TransitionTo(FlowState.Idle, "Awake");
    }

    private void OnEnable()
    {
        // Sesión / Flujo
        GameEventBus.OnSessionStartRequested += HandleSessionStartRequested;
        GameEventBus.OnPlaneSelected += HandlePlaneSelected;
        GameEventBus.OnPlanesHidden += HandlePlanesHidden;
        GameEventBus.OnLevelIntroCompleted += HandleLevelIntroCompleted;
        GameEventBus.OnTutorialCompleted += HandleTutorialCompleted;

        // Gameplay
        GameEventBus.OnAllPlantsSelected += HandleAllPlantsSelected;
        GameEventBus.OnDiseaseAnalysisCompleted += HandleDiseaseAnalysisCompleted;

        // End Session
        GameEventBus.OnReturnToMenuRequested += HandleReturnToMenuRequested;
    }

    private void OnDisable()
    {
        GameEventBus.OnSessionStartRequested -= HandleSessionStartRequested;
        GameEventBus.OnPlaneSelected -= HandlePlaneSelected;
        GameEventBus.OnPlanesHidden -= HandlePlanesHidden;
        GameEventBus.OnLevelIntroCompleted -= HandleLevelIntroCompleted;
        GameEventBus.OnTutorialCompleted -= HandleTutorialCompleted;

        GameEventBus.OnAllPlantsSelected -= HandleAllPlantsSelected;
        GameEventBus.OnDiseaseAnalysisCompleted -= HandleDiseaseAnalysisCompleted;

        GameEventBus.OnReturnToMenuRequested -= HandleReturnToMenuRequested;

        CancelLevelTransition();
        guidedTutorialController?.ResetTutorial();
    }

    // ─────────────────────────────────────────────
    // Handlers de eventos del bus
    // ─────────────────────────────────────────────

    /// <summary>
    /// El usuario presionó "Iniciar" en el menú principal.
    /// Prepara la sesión y spawnea los planos de MR.
    /// </summary>
    private void HandleSessionStartRequested()
    {
        if (CurrentState != FlowState.Idle && CurrentState != FlowState.None) return;

        // Preparar para nueva sesión
        sceneInteractionManager?.PrepareForNextSession();
        ResetFlowState();

        TransitionTo(FlowState.WaitingForPlaneSelection, "SessionStartRequested");
        Debug.Log("[GameFlow] Listo — esperando selección de plano.");

        // Spawnear los planos en el MixedReality
        if (planeSpawner != null)
        {
            MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
            if (room != null) planeSpawner.SpawnForRoom(room);
            else Debug.LogWarning("[GameFlow] No se encontró room.");
        }
        else Debug.LogWarning("[GameFlow] planeSpawner no asignado.");
    }

    /// <summary>
    /// El usuario seleccionó un plano MR.
    /// Guarda el transform seleccionado y comienza a ocultar los planos.
    /// </summary>
    private void HandlePlaneSelected(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (CurrentState != FlowState.WaitingForPlaneSelection) return;

        _selectedPlanePosition = position;
        _selectedPlaneRotation = rotation;
        _selectedPlaneScale = scale;
        _planeSelected = true;

        // Notificar a SceneInteractionManager del plano seleccionado
        sceneInteractionManager?.OnAnchorButtonClicked(position, rotation, scale);

        TransitionTo(FlowState.DisablingPlanes, "PlaneSelected");
        BeginDisablingPlanes();
    }

    /// <summary>
    /// Los planos de MR terminaron de ocultarse.
    /// Avanza al onboarding o al tutorial según configuración.
    /// </summary>
    private void HandlePlanesHidden()
    {
        if (CurrentState != FlowState.DisablingPlanes) return;
        BeginPostPlaneFlow();
    }

    /// <summary>
    /// La intro del nivel se completó.
    /// Para el nivel tutorial: arranca el panel de tutorial.
    /// Para el resto: ya lo maneja AfterLevelIntroCompleted internamente.
    /// </summary>
    private void HandleLevelIntroCompleted()
    {
        // El flujo post-intro se maneja directamente en el callback de ShowIntro.
        // Este handler existe para que otros sistemas puedan escuchar el evento.
    }

    /// <summary>
    /// La demostración terminó de forma natural o fue omitida.
    /// En ambos casos se continúa una sola vez con el tutorial interactivo.
    /// </summary>
    private void HandleTutorialDemonstrationCompleted(bool skipped)
    {
        return;

        Debug.Log($"[GameFlow] Demostración tutorial completada. skipped={skipped}");
        BeginInteractiveTutorial();
    }

    /// <summary>
    /// El tutorial comenzó (panel visible y animado).
    /// Arranca el nivel tutorial para que las hojas aparezcan
    /// mientras el jugador lee las instrucciones del panel.
    /// </summary>
    private void HandleTutorialStarted()
    {
        if (CurrentState != FlowState.Tutorial) return;
        StartCurrentLevelGameplay();
    }

    /// <summary>
    /// El tutorial se completó (el jugador identificó todas las hojas).
    /// NO arranca un nivel nuevo aquí — el avance al siguiente nivel
    /// ya lo gestiona el flujo AllPlantsSelected → DiseaseAnalysisCompleted.
    /// </summary>
    private void HandleTutorialCompleted()
    {
        if (_tutorialCompletionHandled ||
            (sceneInteractionManager?.GetCurrentLevelIndex() ?? -1) != tutorialLevelIndex)
            return;

        _tutorialCompletionHandled = true;
        _waitingForAllPlants = false;
        TransitionTo(FlowState.LevelTransition, "GuidedTutorialCompleted");
        CancelLevelTransition();
        _levelTransitionCoroutine = StartCoroutine(DelayedCompleteLevelAndAdvance(tutorialLevelIndex));
        // Nada que hacer: CompleteLevelAndAdvance ya se encargará del avance.
    }

    /// <summary>
    /// Todas las plantas del nivel actual fueron seleccionadas correctamente.
    /// </summary>
    private void HandleAllPlantsSelected()
    {
        if ((sceneInteractionManager?.GetCurrentLevelIndex() ?? -1) == tutorialLevelIndex &&
            (guidedTutorialController == null || guidedTutorialController.IsRunning || _tutorialCompletionHandled))
            return;

        _waitingForAllPlants = true;
        TransitionTo(FlowState.WaitingForLevelCompletion, "AllPlantsSelected");
    }

    /// <summary>
    /// El análisis de enfermedad se completó (barra de progreso terminó, o nivel final listo).
    /// Según el contexto: spawnea el nivel actual, o avanza al siguiente nivel.
    /// </summary>
    private void HandleDiseaseAnalysisCompleted()
    {
        if (_waitingAnalysisToSpawnLevel)
        {
            _waitingAnalysisToSpawnLevel = false;
            sceneInteractionManager?.SpawnCurrentLevel();
            TransitionTo(FlowState.LevelPlaying, "AnalysisCompleteSpawn");
            GameEventBus.PublishLevelSpawned(sceneInteractionManager?.GetCurrentLevelIndex() ?? 0);
            return;
        }

        if (_waitingForAllPlants)
        {
            int idx = sceneInteractionManager?.GetCurrentLevelIndex() ?? 0;
            _waitingForAllPlants = false;
            TransitionTo(FlowState.LevelTransition, "LevelTransition");
            CancelLevelTransition();
            _levelTransitionCoroutine = StartCoroutine(DelayedCompleteLevelAndAdvance(idx));
        }
    }

    /// <summary>
    /// El usuario quiere volver al menú principal.
    /// </summary>
    private void HandleReturnToMenuRequested()
    {
        sceneInteractionManager?.PrepareForNextSession();
        ResetFlowState();
        TransitionTo(FlowState.Idle, "ReturnToMenu");
    }

    // ─────────────────────────────────────────────
    // Lógica de flujo interna
    // ─────────────────────────────────────────────

    private void BeginDisablingPlanes()
    {
        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("[GameFlow] No se encontró room.");
            GameEventBus.PublishPlanesHidden();
            return;
        }

        List<GameObject> planePrefabs = CollectActivePlanePrefabs(room);

        if (planePrefabs.Count == 0)
        {
            GameEventBus.PublishPlanesHidden();
            return;
        }

        DisableAllPlanePrefabsWithAnimation(planePrefabs, () =>
        {
            GameEventBus.PublishPlanesHidden();
        });
    }

    private void BeginPostPlaneFlow()
    {
        levelIntroController?.ResetState();
        tutorialPanelController?.Hide();

        StartCurrentLevelFlow();
    }

    /// <summary>
    /// Muestra la intro del nivel actual y, al completarse, continúa con el gameplay.
    /// Si no hay LevelIntroController configurado, avanza directamente.
    /// </summary>
    private void StartCurrentLevelFlow()
    {
        int levelIndex  = sceneInteractionManager?.GetCurrentLevelIndex() ?? 0;
        int totalLevels = sceneInteractionManager?.GetTotalLevels() ?? 0;

        if (!runLevelIntro || levelIntroController == null)
        {
            AfterLevelIntroCompleted(levelIndex, totalLevels);
            return;
        }

        TransitionTo(FlowState.LevelIntro, "ShowLevelIntro");
        GameEventBus.PublishLevelIntroStarted();

        levelIntroController.ShowIntro(levelIndex, totalLevels, () =>
        {
            GameEventBus.PublishLevelIntroCompleted();
            AfterLevelIntroCompleted(levelIndex, totalLevels);
        });
    }

    /// <summary>
    /// Lógica de arranque del nivel una vez que la intro terminó.
    /// Separa tutorial de niveles normales.
    /// </summary>
    private void AfterLevelIntroCompleted(int levelIndex, int totalLevels)
    {
        int plantsRequired = sceneInteractionManager?.GetCurrentPlantsRequired() ?? 1;

        if (levelIndex == tutorialLevelIndex)
        {
            BeginGuidedTutorial();
            return;
        }

        // Nivel normal: publicar LevelStarted y continuar
        StartSessionMetricsIfNeeded();
        TransitionTo(FlowState.LevelStarting, "StartCurrentLevelGameplay");
        GameEventBus.PublishLevelStarted(levelIndex, totalLevels, plantsRequired);

        if (RequiresDiseaseAnalysisBeforeSpawn(levelIndex))
        {
            _waitingAnalysisToSpawnLevel = true;
            DiseaseSelectionSystem.Instance?.HideAnimated();
            TransitionTo(FlowState.WaitingForDiseaseAnalysis, "AwaitDiseaseAnalysis");
            return;
        }

        sceneInteractionManager?.SpawnCurrentLevel();
        TransitionTo(FlowState.LevelPlaying, "SpawnedLevel");
        GameEventBus.PublishLevelSpawned(levelIndex);
    }

    private void BeginTutorialDemonstrationOrContinue()
    {
        if (runTutorialDemonstration)
        {
            if (tutorialDemonstrationController == null)
            {
                Debug.LogWarning(
                    "[GameFlow] TutorialDemonstrationController no asignado. " +
                    "Se continúa con el tutorial interactivo.");
            }
            else if (!tutorialDemonstrationController.CanPlay)
            {
                Debug.LogWarning(
                    "[GameFlow] La demostración no tiene una Timeline completa. " +
                    "Se continúa con el tutorial interactivo.");
            }
            else
            {
                return;

                if (tutorialDemonstrationController.Play())
                {
                    GameEventBus.PublishTutorialDemonstrationStarted();
                    return;
                }

                Debug.LogWarning(
                    "[GameFlow] La demostración no pudo iniciarse. " +
                    "Se continúa con el tutorial interactivo.");
            }
        }

        BeginInteractiveTutorial();
    }

    private void BeginGuidedTutorial()
    {
        StartSessionMetricsIfNeeded();
        TransitionTo(FlowState.Tutorial, "StartGuidedTutorial");
        StartCurrentLevelGameplay();

        if (guidedTutorialController == null)
        {
            Debug.LogError("[GameFlow] GuidedTutorialController no disponible.");
            return;
        }

        guidedTutorialController.StartTutorial();
    }

    private void BeginInteractiveTutorial()
    {
        StartSessionMetricsIfNeeded();

        if (tutorialPanelController == null)
        {
            Debug.LogWarning(
                "[GameFlow] TutorialPanelController no disponible. Se inicia el nivel tutorial sin guía.");
            StartCurrentLevelGameplay();
            return;
        }

        TransitionTo(FlowState.Tutorial, "StartTutorial");
        tutorialPanelController.ShowAndStart();
    }

    /// <summary>
    /// Spawnea el nivel actual (llamado desde HandleTutorialStarted para el nivel tutorial).
    /// </summary>
    private void StartCurrentLevelGameplay()
    {
        int levelIndex  = sceneInteractionManager?.GetCurrentLevelIndex() ?? 0;
        int totalLevels = sceneInteractionManager?.GetTotalLevels() ?? 0;
        int plantsRequired = sceneInteractionManager?.GetCurrentPlantsRequired() ?? 1;

        TransitionTo(FlowState.LevelStarting, "StartCurrentLevelGameplay");
        GameEventBus.PublishLevelStarted(levelIndex, totalLevels, plantsRequired);

        sceneInteractionManager?.SpawnCurrentLevel();
        TransitionTo(FlowState.LevelPlaying, "SpawnedLevel");
        GameEventBus.PublishLevelSpawned(levelIndex);
    }

    private bool RequiresDiseaseAnalysisBeforeSpawn(int levelIndex)
    {
        if (levelIndex < 0) return false;
        return levelIndex != tutorialLevelIndex;
    }

    private void CompleteLevelAndAdvance(int completedIdx)
    {
        int currentIdx = sceneInteractionManager?.GetCurrentLevelIndex() ?? -1;
        if (completedIdx != currentIdx) return;

        GameEventBus.PublishLevelCompleted(completedIdx);
        sceneInteractionManager?.DestroyCurrentLevel();
        sceneInteractionManager?.AdvanceLevelIndex();

        int newIdx = sceneInteractionManager?.GetCurrentLevelIndex() ?? 0;
        int totalLevels = sceneInteractionManager?.GetTotalLevels() ?? 0;

        if (newIdx >= totalLevels)
        {
            GameEventBus.PublishAllLevelsCompleted();
            DiseaseSelectionSystem.Instance?.HideAnimated();
            TransitionTo(FlowState.AllLevelsCompleted, "AllLevelsCompleted");
            return;
        }

        StartCurrentLevelFlow();
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    private void StartSessionMetricsIfNeeded()
    {
        if (_sessionMetricsStarted) return;

        if (SessionMetricsTracker.Instance != null)
            SessionMetricsTracker.Instance.StartSession();
        else
            Debug.LogWarning("[GameFlow] SessionMetricsTracker no encontrado.");

        _sessionMetricsStarted = true;
    }

    private void ResetFlowState()
    {
        _planeSelected = false;
        _sessionMetricsStarted = false;
        _waitingForAllPlants = false;
        _waitingAnalysisToSpawnLevel = false;
        _tutorialCompletionHandled = false;
        CancelLevelTransition();
        tutorialDemonstrationController?.ResetState();
        guidedTutorialController?.ResetTutorial();
    }

    private IEnumerator DelayedCompleteLevelAndAdvance(int idx)
    {
        yield return new WaitForSeconds(levelTransitionDelay);
        CompleteLevelAndAdvance(idx);
    }

    private void CancelLevelTransition()
    {
        if (_levelTransitionCoroutine != null)
        {
            StopCoroutine(_levelTransitionCoroutine);
            _levelTransitionCoroutine = null;
        }
    }

    private void TransitionTo(FlowState nextState, string reason = null)
    {
        if (CurrentState == nextState) return;
        FlowState previous = CurrentState;
        CurrentState = nextState;
        string displayReason = string.IsNullOrEmpty(reason) ? "unspecified" : reason;
        Debug.Log($"[GameFlow] FlowState {previous} -> {nextState} ({displayReason})");
        GameEventBus.PublishFlowStateChanged(previous, nextState, displayReason);
    }

    private void AutoResolveDependencies()
    {
        if (sceneInteractionManager == null)
            sceneInteractionManager = FindObjectOfType<SceneInteractionManager>(true);

        if (levelIntroController == null)
            levelIntroController = FindObjectOfType<LevelIntroController>(true);

        if (tutorialDemonstrationController == null)
            tutorialDemonstrationController = FindObjectOfType<TutorialDemonstrationController>(true);

        if (tutorialPanelController == null)
            tutorialPanelController = FindObjectOfType<TutorialPanelController>(true);

        if (guidedTutorialController == null)
            guidedTutorialController = FindObjectOfType<GuidedTutorialController>(true);

        if (planeSpawner == null)
            planeSpawner = FindObjectOfType<PlaneConfigurationSpawner>(true);

        if (mainMenuController == null)
            mainMenuController = FindObjectOfType<MainMenuController>(true);
    }

    // ── Planos MR ────────────────────────────────────────────────────────────

    private List<GameObject> CollectActivePlanePrefabs(MRUKRoom room)
    {
        var planePrefabs = new List<GameObject>();
        foreach (var anchor in room.Anchors)
        {
            if (anchor == null || !anchor.gameObject.activeInHierarchy) continue;
            Transform p = FindPlanePrefab(anchor.transform);
            if (p != null) planePrefabs.Add(p.gameObject);
        }
        return planePrefabs;
    }

    private void DisableAllPlanePrefabsWithAnimation(List<GameObject> planePrefabs, System.Action onComplete)
    {
        int remaining = 0;
        foreach (var prefab in planePrefabs)
        {
            if (prefab == null || !prefab.activeInHierarchy) continue;
            remaining++;
            prefab.transform.DOKill();
            prefab.transform
                .DOScale(Vector3.zero, scaleDuration)
                .SetEase(Ease.InBack)
                .SetLink(prefab, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    if (prefab != null)
                    {
                        prefab.SetActive(false);
                    }
                    remaining--;
                    if (remaining <= 0)
                        onComplete?.Invoke();
                });
        }

        if (remaining == 0)
            onComplete?.Invoke();
    }

    private Transform FindPlanePrefab(Transform parent)
    {
        foreach (Transform child in parent)
            if (child.name.Contains("PlanePrefab")) return child;
        return null;
    }

    // ── Propiedades públicas ─────────────────────────────────────────────────

    public Vector3 SelectedPlanePosition => _selectedPlanePosition;
    public Quaternion SelectedPlaneRotation => _selectedPlaneRotation;
    public bool HasSelectedPlane => _planeSelected;
}
