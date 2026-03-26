using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using DG.Tweening;

public class SceneInteractionManager : MonoBehaviour
{
    [System.Serializable]
    public class LevelConfig
    {
        public GameObject levelPrefab;
        /// <summary>Cuántas hojas correctas debe identificar el jugador para pasar al siguiente nivel.</summary>
        [Min(1)] public int plantsRequired = 1;
    }

    public static SceneInteractionManager Instance { get; private set; }

    [Header("Niveles")]
    [Tooltip("Lista de niveles. Cada elemento define el prefab y cuántas plantas correctas se requieren.")]
    [SerializeField] private List<LevelConfig> levelConfigs = new List<LevelConfig>();
    [SerializeField] private Transform spawnParent;
    [SerializeField] private int currentLevelIndex = 0;

    [Header("Compatibilidad Legacy")]
    [Tooltip("Solo respaldo. Si levelConfigs está vacío se usan estos prefabs con 1 planta requerida.")]
    [SerializeField] private List<GameObject> prefabsToSpawn = new List<GameObject>();

    [Header("Planos MR")]
    [Tooltip("El PlaneConfigurationSpawner de la escena (Spawn On Start debe estar en None)")]
    [SerializeField] private PlaneConfigurationSpawner planeSpawner;

    [Header("Tutorial")]
    [SerializeField] private TutorialPanelController tutorialPanelController;

    [Header("Animation Settings")]
    [SerializeField] private float scaleDuration = 0.5f;

    [Header("Flujo de nivel")]
    [SerializeField] private float levelTransitionDelay = 1.1f;
    [SerializeField] private float analysisFallbackTimeout = 6f;

    [Header("Índices especiales")]
    [SerializeField] private int tutorialLevelIndex = 0;
    [SerializeField] private int levelFinalIndex = 3;

    private readonly List<LevelConfig> _runtimeLevels = new List<LevelConfig>();

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    private bool hasBeenActivated = false;
    private bool _isReady = false;
    private bool _firstLevelReady = false; // true cuando el tutorial terminó y está pendiente arrancar nivel 0
    private bool _firstLevelStarted = false;
    private bool _tutorialStarted = false;

    private bool _waitingForAllPlants = false;
    private bool _waitingAnalysisToSpawnLevel = false;

    // Raíz del nivel actual. Todas las hojas son hijas de este objeto.
    private GameObject currentLevelInstance = null;

    private Tween _analysisFallbackTween;

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (tutorialPanelController == null)
            tutorialPanelController = FindObjectOfType<TutorialPanelController>(true);

        BuildRuntimeLevels();
    }

    private void OnEnable()
    {
        GameEventBus.OnAllPlantsSelected += HandleAllPlantsSelected;
        GameEventBus.OnDiseaseAnalysisCompleted += HandleDiseaseAnalysisCompleted;
    }

    private void OnDisable()
    {
        GameEventBus.OnAllPlantsSelected -= HandleAllPlantsSelected;
        GameEventBus.OnDiseaseAnalysisCompleted -= HandleDiseaseAnalysisCompleted;
        _analysisFallbackTween?.Kill();
    }

    // ─────────────────────────────────────────────
    // API pública
    // ─────────────────────────────────────────────

    /// <summary>
    /// Habilita el manager y spawnea los planos MR.
    /// Llamar desde MainMenuController al presionar Iniciar.
    /// El primer nivel NO arranca aquí — arranca cuando el tutorial termina.
    /// </summary>
    public void WaitForStartSignal()
    {
        _isReady = true;
        Debug.Log("[SceneManager] Listo — esperando tutorial y selección de plano.");

        if (hasBeenActivated)
        {
            StartTutorialAfterPlaneSelection();
            return;
        }

        if (planeSpawner != null)
        {
            MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
            if (room != null) planeSpawner.SpawnForRoom(room);
            else Debug.LogWarning("[SceneManager] No se encontró room.");
        }
        else Debug.LogWarning("[SceneManager] planeSpawner no asignado.");
    }

    public void PrepareForNextSession()
    {
        DestroyCurrentLeaves();

        currentLevelIndex = 0;
        _waitingForAllPlants = false;
        _waitingAnalysisToSpawnLevel = false;
        _firstLevelReady = false;
        _firstLevelStarted = false;
        _tutorialStarted = false;

        _analysisFallbackTween?.Kill();
        DiseaseSelectionSystem.Instance?.HideAnimated();
    }

    /// <summary>
    /// Inicia el primer nivel. Llamar desde UIGameListener.OnTutorialCompleted().
    /// El nivel 0 se publica y spawnea solo cuando ya existe un plano seleccionado.
    /// </summary>
    public void StartFirstLevel()
    {
        _firstLevelReady = true;
        TryStartFirstLevelAfterPlaneSelection();
    }

    // ── Click en botón del plano ─────────────────────────────────────────────
    public void OnAnchorButtonClicked(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
    {
        if (!_isReady || hasBeenActivated) return;

        this.targetPosition = targetPosition;
        this.targetRotation = targetRotation;
        this.targetScale = targetScale;

        hasBeenActivated = true;
        StartTutorialAfterPlaneSelection();
        DisableAllPlanePrefabsWithAnimation();
    }

    // ── Animación de planos ──────────────────────────────────────────────────
    private void DisableAllPlanePrefabsWithAnimation()
    {
        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
        if (room == null) { Debug.LogWarning("No room found!"); return; }

        var planePrefabs = new List<GameObject>();
        foreach (var anchor in room.Anchors)
        {
            if (anchor != null && anchor.gameObject.activeInHierarchy)
            {
                Transform p = FindPlanePrefab(anchor.transform);
                if (p != null) planePrefabs.Add(p.gameObject);
            }
        }

        if (planePrefabs.Count == 0)
        {
            TryStartFirstLevelAfterPlaneSelection();
            return;
        }

        int remaining = planePrefabs.Count;
        foreach (var prefab in planePrefabs)
        {
            if (prefab == null || !prefab.activeInHierarchy) { remaining--; continue; }
            prefab.transform.DOKill();
            prefab.transform
                .DOScale(Vector3.zero, scaleDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    prefab.SetActive(false);
                    remaining--;
                    if (remaining <= 0)
                        TryStartFirstLevelAfterPlaneSelection();
                });
        }
    }

    private Transform FindPlanePrefab(Transform parent)
    {
        foreach (Transform child in parent)
            if (child.name.Contains("PlanePrefab")) return child;
        return null;
    }

    // ── Spawn del nivel actual ───────────────────────────────────────────────
    private void SpawnCurrentLevelObject()
    {
        if (_runtimeLevels.Count == 0) { Debug.LogWarning("No hay configuraciones de niveles!"); return; }
        if (currentLevelIndex >= _runtimeLevels.Count) { Debug.Log("¡Todos los niveles completados!"); return; }

        LevelConfig level = _runtimeLevels[currentLevelIndex];
        if (level?.levelPrefab == null) { Debug.LogWarning($"Prefab {currentLevelIndex} es null!"); return; }

        ConfigureLeafSpawn(level.levelPrefab);
    }

    private void ConfigureLeafSpawn(GameObject prefabToSpawn)
    {
        Transform parent = spawnParent != null ? spawnParent : transform;
        parent.rotation = targetRotation;

        // currentLevelInstance es el contenedor raíz. Las hojas van dentro de él.
        currentLevelInstance = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity, parent);
        currentLevelInstance.transform.position = targetPosition - Vector3.up * 0.03f;
        currentLevelInstance.transform.rotation = targetRotation;

        DiseaseSelectionSystem.Instance?.Hide();

        LeavesSpawner leafSpawner = currentLevelInstance.GetComponent<LeavesSpawner>();
        if (leafSpawner != null)
        {
            leafSpawner.SpawnAreaSize = new Vector2(targetScale.x, targetScale.y);

            // FIX: hojas hijas del nivel actual → se destruyen con él
            leafSpawner.grassParent = currentLevelInstance.transform;

            if (leafSpawner.grassCount == -1)
                leafSpawner.grassCount = Mathf.Max((int)(targetScale.x * targetScale.y * 0.7f), 20);

            leafSpawner.Activate();
        }
        else
            Debug.LogWarning($"{prefabToSpawn.name} no tiene LeavesSpawner");
    }

    // ── Avance de nivel guiado por eventos ───────────────────────────────────
    private void HandleAllPlantsSelected()
    {
        _waitingForAllPlants = true;
    }

    private void HandleDiseaseAnalysisCompleted()
    {
        if (_waitingAnalysisToSpawnLevel)
        {
            _waitingAnalysisToSpawnLevel = false;
            SpawnCurrentLevelObject();
            return;
        }

        if (_waitingForAllPlants)
        {
            int idx = currentLevelIndex;
            _waitingForAllPlants = false;
            DOVirtual.DelayedCall(levelTransitionDelay, () => CompleteLevelAndAdvance(idx));
        }
    }

    private void CompleteLevelAndAdvance(int completedIdx)
    {
        if (completedIdx != currentLevelIndex) return;

        GameEventBus.PublishLevelCompleted(completedIdx);
        DestroyCurrentLeaves();
        currentLevelIndex++;

        if (currentLevelIndex >= _runtimeLevels.Count)
        {
            GameEventBus.PublishAllLevelsCompleted();
            DiseaseSelectionSystem.Instance?.HideAnimated();
            return;
        }

        StartCurrentLevelFlow();
    }

    /// <summary>
    /// Destruye el nivel actual y todas las hojas hijas.
    /// Mata tweens DOTween pendientes para evitar callbacks sobre objetos destruidos.
    /// </summary>
    private void DestroyCurrentLeaves()
    {
        if (currentLevelInstance != null)
        {
            foreach (Transform child in currentLevelInstance.transform)
                child?.DOKill();

            Destroy(currentLevelInstance);
            currentLevelInstance = null;
            return;
        }

        if (spawnParent == null) return;

        foreach (Transform child in spawnParent)
            if (child != null) Destroy(child.gameObject);
    }

    public void ResetToFirstLevel()
    {
        DestroyCurrentLeaves();
        currentLevelIndex = 0;
        _waitingForAllPlants = false;
        _waitingAnalysisToSpawnLevel = false;
        _firstLevelStarted = false;
        _firstLevelReady = true;
        TryStartFirstLevelAfterPlaneSelection();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    public int GetCurrentLevelIndex() => currentLevelIndex;
    public int GetTotalLevels() => _runtimeLevels.Count;
    public string GetLevelProgress() => $"Nivel {currentLevelIndex + 1} de {_runtimeLevels.Count}";

    public int GetCurrentPlantsRequired()
    {
        if (currentLevelIndex < 0 || currentLevelIndex >= _runtimeLevels.Count) return 1;
        return Mathf.Max(1, _runtimeLevels[currentLevelIndex].plantsRequired);
    }

    private void StartTutorialAfterPlaneSelection()
    {
        if (_tutorialStarted) return;
        _tutorialStarted = true;

        if (tutorialPanelController != null)
        {
            tutorialPanelController.ShowAndStart();
            return;
        }

        Debug.LogWarning("[SceneManager] tutorialPanelController no asignado/encontrado. Se inicia nivel 0 sin panel tutorial.");
        StartFirstLevel();
    }

    private void TryStartFirstLevelAfterPlaneSelection()
    {
        if (!_firstLevelReady || _firstLevelStarted || !hasBeenActivated) return;
        if (currentLevelIndex != 0) return;

        _firstLevelStarted = true;
        _firstLevelReady = false;
        StartCurrentLevelFlow();
    }

    private void StartCurrentLevelFlow()
    {
        PublishCurrentLevelStarted();

        if (RequiresDiseaseAnalysisBeforeSpawn(currentLevelIndex))
        {
            _waitingAnalysisToSpawnLevel = true;
            DiseaseSelectionSystem.Instance?.HideAnimated();
            return;
        }

        SpawnCurrentLevelObject();
    }

    private bool RequiresDiseaseAnalysisBeforeSpawn(int levelIndex)
    {
        if (levelIndex < 0) return false;
        return levelIndex != tutorialLevelIndex && levelIndex != levelFinalIndex;
    }

    private void PublishCurrentLevelStarted()
    {
        GameEventBus.PublishLevelStarted(currentLevelIndex, _runtimeLevels.Count, GetCurrentPlantsRequired());
    }

    private void BuildRuntimeLevels()
    {
        _runtimeLevels.Clear();
        foreach (var config in levelConfigs)
        {
            if (config?.levelPrefab == null) continue;
            _runtimeLevels.Add(new LevelConfig
            {
                levelPrefab = config.levelPrefab,
                plantsRequired = Mathf.Max(1, config.plantsRequired)
            });
        }
        if (_runtimeLevels.Count > 0) return;
        foreach (var prefab in prefabsToSpawn)
        {
            if (prefab == null) continue;
            _runtimeLevels.Add(new LevelConfig { levelPrefab = prefab, plantsRequired = 1 });
        }
    }

    public Vector3 SelectedPlanePosition => targetPosition;
    public Quaternion SelectedPlaneRotation => targetRotation;
    public bool HasSelectedPlane => hasBeenActivated;
}