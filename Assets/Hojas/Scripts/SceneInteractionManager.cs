using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using DG.Tweening;

/// <summary>
/// SCENE INTERACTION MANAGER — SOLO SPAWN/DESPAWN DE NIVELES
///
/// Este componente:
///   - Construye y mantiene la lista de niveles (LevelConfig).
///   - Spawnea y destruye las instancias de nivel (prefabs + hojas).
///   - Expone helpers de consulta (índice actual, progreso, etc.).
///   - Cachea el transform del plano seleccionado.
///
/// Ya NO orquesta el flujo del juego. Esa responsabilidad es de GameFlowController.
/// Los métodos legacy (WaitForStartSignal, etc.) se mantienen como wrappers
/// que delegan al GameFlowController para no romper referencias existentes en Unity.
/// </summary>
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


    private readonly List<LevelConfig> _runtimeLevels = new List<LevelConfig>();

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    private bool hasBeenActivated = false;

    // Raíz del nivel actual. Todas las hojas son hijas de este objeto.
    private GameObject currentLevelInstance = null;

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        BuildRuntimeLevels();
    }

    // ─────────────────────────────────────────────
    // API pública — Usada por GameFlowController
    // ─────────────────────────────────────────────

    /// <summary>
    /// Recibe la posición, rotación y escala del plano seleccionado.
    /// Llamar desde GameFlowController al recibir PlaneSelected.
    /// </summary>
    public void OnAnchorButtonClicked(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
    {
        CacheSelectedPlaneTransform(targetPosition, targetRotation, targetScale);
        hasBeenActivated = true;
    }

    /// <summary>
    /// Spawnea el nivel actual usando el prefab correspondiente al currentLevelIndex.
    /// Llamar desde GameFlowController cuando el nivel debe aparecer.
    /// </summary>
    public void SpawnCurrentLevel()
    {
        SpawnCurrentLevelObject();
    }

    /// <summary>
    /// Destruye el nivel actual y todas las hojas hijas.
    /// Llamar desde GameFlowController al completar un nivel.
    /// </summary>
    public void DestroyCurrentLevel()
    {
        DestroyCurrentLeaves();
    }

    /// <summary>
    /// Avanza el índice de nivel al siguiente.
    /// </summary>
    public void AdvanceLevelIndex()
    {
        currentLevelIndex++;
    }

    /// <summary>
    /// Arranca el primer nivel (índice 0). Wrapper legacy.
    /// GameFlowController llama esto para iniciar el flujo de niveles.
    /// </summary>
    public void StartFirstLevel()
    {
        if (currentLevelIndex != 0) return;
        StartCurrentLevelFlow();
    }

    /// <summary>
    /// Prepara el manager para una nueva sesión: destruye nivel actual, resetea índices y flags.
    /// </summary>
    public void PrepareForNextSession()
    {
        DestroyCurrentLeaves();

        currentLevelIndex = 0;
        hasBeenActivated = false;

        DiseaseSelectionSystem.Instance?.HideAnimated();
    }

    public void ResetToFirstLevel()
    {
        DestroyCurrentLeaves();
        currentLevelIndex = 0;
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

    /// <summary>
    /// Wrapper legacy: inicia el flujo del nivel actual publicando el evento
    /// de LevelStarted y delegando al GameFlowController la decisión de spawn.
    /// Esto se mantiene para que GameFlowController.StartFirstLevel() siga funcionando.
    /// </summary>
    private void StartCurrentLevelFlow()
    {
        // Publicar nivel iniciado - GameFlowController y UIGameListener escuchan esto
        GameEventBus.PublishLevelStarted(currentLevelIndex, _runtimeLevels.Count, GetCurrentPlantsRequired());
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

    private void CacheSelectedPlaneTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        targetPosition = position;
        targetRotation = rotation;
        targetScale = scale;
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

    private void BuildRuntimeLevels()
    {
        _runtimeLevels.Clear();
        
        for (int i = 0; i < levelConfigs.Count; i++)
        {
            var config = levelConfigs[i];
            if (config?.levelPrefab == null) continue;
            _runtimeLevels.Add(new LevelConfig
            {
                levelPrefab = config.levelPrefab,
                plantsRequired = Mathf.Max(1, config.plantsRequired)
            });
        }
        
        if (_runtimeLevels.Count > 0)
        {
            Debug.Log($"[BuildRuntimeLevels] Total niveles cargados: {_runtimeLevels.Count}");
            return;
        }
    }

    public Vector3 SelectedPlanePosition => targetPosition;
    public Quaternion SelectedPlaneRotation => targetRotation;
    public bool HasSelectedPlane => hasBeenActivated;
}