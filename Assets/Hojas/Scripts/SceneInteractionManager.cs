using System.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using Unity.XR.CoreUtils;


/// <summary>
/// Clase SINGLETON que gestiona la interacción del usuario con el entorno de realidad mixta.
/// 
/// CAMBIO: Ya NO se activa en Start. Espera a que MainMenuController llame a WaitForStartSignal()
/// para comenzar a detectar clicks en los planos.
/// Notifica a SessionMetricsTracker al iniciar/completar cada nivel.
/// Notifica a EndSessionController cuando se completan todos los niveles.
/// </summary>
public class SceneInteractionManager : MonoBehaviour
{
  public static SceneInteractionManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private List<GameObject> prefabsToSpawn = new List<GameObject>();
    [SerializeField] private Transform spawnParent;
    [SerializeField] private int currentLevelIndex = 0;

    // ── NUEVO: referencia al spawner de planos ────────────────────────────
    [Header("Planos MR")]
    [Tooltip("El PlaneConfigurationSpawner de la escena (Spawn On Start debe estar en None)")]
    [SerializeField] private PlaneConfigurationSpawner planeSpawner;

    [Header("Animation Settings")]
    [SerializeField] private float scaleDuration = 0.5f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

 private bool hasBeenActivated = false;

    // ?? NUEVO: el manager solo acepta clicks una vez que el menú lo autoriza ??
    private bool _isReady = false;

    private GameObject currentLeavesContainer = null;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }


    // ?? NUEVO: llamado por MainMenuController al pulsar "Iniciar pruebas" ????
    /// <summary>
    /// Habilita el SceneInteractionManager para recibir clicks en los planos.
    /// Llamar esto SOLO desde MainMenuController.OnStartClicked().
    /// </summary>
    public void WaitForStartSignal()
    {
        _isReady = true;
        Debug.Log("[SceneManager] Listo para recibir selección de plano.");

        // Disparar el spawn de los planos azules ahora que el menú lo autorizó
        if (planeSpawner != null)
        {
            MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
            if (room != null)
                planeSpawner.SpawnForRoom(room);
            else
                Debug.LogWarning("[SceneManager] No se encontró room para spawnear planos.");
        }
        else
            Debug.LogWarning("[SceneManager] planeSpawner no asignado.");

        // Registrar inicio del primer nivel en métricas
        if (SessionMetricsTracker.Instance != null)
            SessionMetricsTracker.Instance.StartLevel(currentLevelIndex);
    }

    // ?? Click en botón del plano ?????????????????????????????????????????
    public void OnAnchorButtonClicked(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
    {
        // Ignorar si el menú aún no autorizó o si ya se activó
        if (!_isReady)        return;
        if (hasBeenActivated) return;

    this.targetPosition = targetPosition;
        this.targetRotation = targetRotation;
        this.targetScale    = targetScale;

        hasBeenActivated = true;
        StartCoroutine(DisableAllPlanePrefabsWithAnimation());
    }

    // ?? Corrutinas de animación de planos ????????????????????????????????
    private IEnumerator DisableAllPlanePrefabsWithAnimation()
    {
     MRUKRoom room = MRUK.Instance?.GetCurrentRoom();

        if (room == null)
        {
       Debug.LogWarning("No room found!");
            yield break;
        }

   List<GameObject> planePrefabs = new List<GameObject>();
        foreach (var anchor in room.Anchors)
 {
     if (anchor != null && anchor.gameObject.activeInHierarchy)
     {
    Transform planePrefab = FindPlanePrefab(anchor.transform);
       if (planePrefab != null) planePrefabs.Add(planePrefab.gameObject);
     }
        }

     List<Coroutine> animations = new List<Coroutine>();
        foreach (var planePrefab in planePrefabs)
        {
            if (planePrefab != null && planePrefab.activeInHierarchy)
        animations.Add(StartCoroutine(AnimatePlanePrefabScaleDown(planePrefab)));
        }

     // Espera a que todas las animaciones terminen
     yield return new WaitForSeconds(scaleDuration);

        foreach (var planePrefab in planePrefabs)
       if (planePrefab != null) planePrefab.SetActive(false);

        SpawnNewObject();
    }

    private Transform FindPlanePrefab(Transform parent)
    {
        foreach (Transform child in parent)
            if (child.name.Contains("PlanePrefab")) return child;
        return null;
    }

    private IEnumerator AnimatePlanePrefabScaleDown(GameObject planePrefab)
    {
        float elapsed       = 0f;
     Vector3 originalScale = planePrefab.transform.localScale;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
 float easedT = EaseOutCubic(elapsed / scaleDuration);
  planePrefab.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);
        yield return null;
        }

      planePrefab.transform.localScale = Vector3.zero;
    }

    // ?? Spawn del nivel actual ????????????????????????????????????????????
    private void SpawnNewObject()
    {
        if (prefabsToSpawn.Count == 0)           { Debug.LogWarning("No hay prefabs en la lista!"); return; }
     if (currentLevelIndex >= prefabsToSpawn.Count) { Debug.Log("¡Todos los niveles completados!"); return; }

   GameObject currentPrefab = prefabsToSpawn[currentLevelIndex];
        if (currentPrefab == null)     { Debug.LogWarning($"Prefab en índice {currentLevelIndex} es null!"); return; }

        ConfigureLeafSpawn(currentPrefab);
    }

    private void ConfigureLeafSpawn(GameObject prefabToSpawn)
    {
Transform parent = spawnParent != null ? spawnParent : transform;
        parent.rotation  = targetRotation;

        GameObject instance = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity, parent);
    instance.transform.position = targetPosition - Vector3.up * 0.03f;
        instance.transform.rotation = targetRotation;

        currentLeavesContainer = parent.gameObject;

        // Mostrar y posicionar la UI de diagnóstico al lado del plano elegido
        if (DiseaseSelectionSystem.Instance != null)
        {
            Vector3 planeRight  = targetRotation * Vector3.right;
            Vector3 planeNormal = targetRotation * Vector3.up;
            DiseaseSelectionSystem.Instance.PlaceNextTo(targetPosition, planeRight, planeNormal);
            DiseaseSelectionSystem.Instance.ShowAnimated();
        }

        LeavesSpawner leafSpawner = instance.GetComponent<LeavesSpawner>();
        if (leafSpawner != null)
        {
            leafSpawner.SpawnAreaSize = new Vector2(targetScale.x, targetScale.y);
       leafSpawner.grassParent   = parent;

      if (leafSpawner.grassCount == -1)
   leafSpawner.grassCount = Mathf.Max((int)(targetScale.x * targetScale.y * 0.7f), 20);

  leafSpawner.Activate();
        }
        else
        {
 Debug.LogWarning($"El prefab {prefabToSpawn.name} no tiene componente LeavesSpawner");
        }
    }

    // ?? Avance de nivel ???????????????????????????????????????????????????
    public void AdvanceToNextLevel()
    {
        Debug.Log($"Avanzando del nivel {currentLevelIndex} al nivel {currentLevelIndex + 1}");

        DestroyCurrentLeaves();
        currentLevelIndex++;

     if (currentLevelIndex >= prefabsToSpawn.Count)
      {
     Debug.Log("¡Felicitaciones! Has completado todos los niveles.");
  OnAllLevelsCompleted();
      return;
      }

        // Notificar métricas: inicio del nuevo nivel
        if (SessionMetricsTracker.Instance != null)
       SessionMetricsTracker.Instance.StartLevel(currentLevelIndex);

        SpawnNewObject();
    }

    private void DestroyCurrentLeaves()
    {
        if (currentLeavesContainer == null) return;

        foreach (Transform leaf in currentLeavesContainer.transform)
       if (leaf != null) Destroy(leaf.gameObject);
    }

    public void ResetToFirstLevel()
    {
        DestroyCurrentLeaves();
        currentLevelIndex = 0;

        if (SessionMetricsTracker.Instance != null)
            SessionMetricsTracker.Instance.StartLevel(currentLevelIndex);

        SpawnNewObject();
    }

    private void OnAllLevelsCompleted()
    {
        Debug.Log("Todos los niveles completados.");

        // Ocultar la UI de diagnóstico
        if (DiseaseSelectionSystem.Instance != null)
            DiseaseSelectionSystem.Instance.HideAnimated();

        if (EndSessionController.Instance != null)
        {
            Vector3 planeNormal = targetRotation * Vector3.up;
            EndSessionController.Instance.PlaceAt(targetPosition, planeNormal);
            EndSessionController.Instance.ShowAnimated();
        }
        else
            Debug.LogWarning("[SceneManager] EndSessionController no encontrado en escena.");
    }

    // ?? Helpers ???????????????????????????????????????????????????????????
  private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    public int    GetCurrentLevelIndex() => currentLevelIndex;
    public int    GetTotalLevels()       => prefabsToSpawn.Count;
  public string GetLevelProgress()     => $"Nivel {currentLevelIndex + 1} de {prefabsToSpawn.Count}";
}