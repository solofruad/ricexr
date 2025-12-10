using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using Unity.XR.CoreUtils;


/// <summary>
/// Clase que gestiona la interacción del usuario con el entorno de realidad mixta.
/// 
/// Funcionalidades principales:
/// 1. Gestiona el ciclo de vida de los objetos interactivos mediante un patrón Singleton
/// 2. Detecta y anima la desaparición de los planos existentes en el entorno MR
/// 3. Instancia y configura nuevos objetos prefabricados en posiciones específicas
/// 4. Implementa un sistema de niveles progresivos con transiciones animadas
/// 5. Proporciona métodos para navegar entre niveles y reiniciar la experiencia
/// 
/// Flujo de trabajo:
/// - El usuario selecciona un plano en el entorno de realidad mixta
/// - Se activa OnAnchorSelected() que captura la posición, rotación y escala del plano
/// - Todos los planos existentes se animan y desaparecen
/// - Se instancia un nuevo objeto prefabricado en la ubicación seleccionada
/// - El sistema mantiene un índice de nivel actual para cargar diferentes prefabs
/// </summary>
public class SceneInteractionManager : MonoBehaviour
{
    public static SceneInteractionManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private List<GameObject> prefabsToSpawn = new List<GameObject>();
    [SerializeField] private Transform spawnParent;
    [SerializeField] private int currentLevelIndex = 0;

    [Header("Animation Settings")]
    [SerializeField] private float scaleDuration = 0.5f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    private bool hasBeenActivated = false;
    private GameObject currentLeavesContainer = null;

    void Awake()
    {
        // Singleton 
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Este método se llamará desde el botón UI
    public void OnAnchorButtonClicked(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
    {
        if (hasBeenActivated) return; // Evita clicks múltiples

        this.targetPosition = targetPosition;
        this.targetRotation = targetRotation;
        this.targetScale = targetScale;

        hasBeenActivated = true;
        StartCoroutine(DisableAllPlanePrefabsWithAnimation());
    }

    private IEnumerator DisableAllPlanePrefabsWithAnimation()
    {
        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();

        if (room == null)
        {
            Debug.LogWarning("No room found!");
            yield break;
        }

        // Obtén todos los PlanePrefab de cada anchor
        List<GameObject> planePrefabs = new List<GameObject>();

        foreach (var anchor in room.Anchors)
        {
            if (anchor != null && anchor.gameObject.activeInHierarchy)
            {
                // Busca el PlanePrefab dentro del anchor
                Transform planePrefab = FindPlanePrefab(anchor.transform);
                if (planePrefab != null)
                {
                    planePrefabs.Add(planePrefab.gameObject);
                }
            }
        }

        // Anima todos los PlanePrefabs
        List<Coroutine> animations = new List<Coroutine>();

        foreach (var planePrefab in planePrefabs)
        {
            if (planePrefab != null && planePrefab.activeInHierarchy)
            {
                Coroutine anim = StartCoroutine(AnimatePlanePrefabScaleDown(planePrefab));
                animations.Add(anim);
            }
        }

        // Espera a que todas las animaciones terminen
        yield return new WaitForSeconds(scaleDuration);

        // Desactiva solo los PlanePrefabs
        foreach (var planePrefab in planePrefabs)
        {
            if (planePrefab != null)
            {
                planePrefab.SetActive(false);
            }
        }

        SpawnNewObject();
    }

    // Busca el PlanePrefab en la jerarquía del anchor
    private Transform FindPlanePrefab(Transform parent)
    {
        // Busca primero en los hijos directos
        foreach (Transform child in parent)
        {
            if (child.name.Contains("PlanePrefab"))
            {
                return child;
            }
        }


        return null;
    }

    private IEnumerator AnimatePlanePrefabScaleDown(GameObject planePrefab)
    {
        float elapsed = 0f;
        Vector3 originalScale = planePrefab.transform.localScale;

        // Animación de escala
        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleDuration;
            float easedT = EaseOutCubic(t);

            planePrefab.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);

            yield return null;
        }

        // Asegura que termine en escala cero
        planePrefab.transform.localScale = Vector3.zero;
    }

    private void SpawnNewObject()
    {
        if (prefabsToSpawn.Count == 0)
        {
            Debug.LogWarning("No hay prefabs en la lista!");
            return;
        }

        if (currentLevelIndex >= prefabsToSpawn.Count)
        {
            Debug.Log("¡Todos los niveles completados!");
            return;
        }

        GameObject currentPrefab = prefabsToSpawn[currentLevelIndex];

        if (currentPrefab == null)
        {
            Debug.LogWarning($"Prefab en índice {currentLevelIndex} es null!");
            return;
        }

        ConfigureLeafSpawn(currentPrefab);
    }

    private void ConfigureLeafSpawn(GameObject prefabToSpawn)
    {
        Transform parent = spawnParent != null ? spawnParent : transform;

        parent.rotation = targetRotation;
        GameObject instance = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity, parent);
        instance.transform.position = targetPosition - Vector3.up * 0.03f;
        instance.transform.rotation = targetRotation;

        // Guardar referencia al contenedor actual de hojas
        currentLeavesContainer = parent.gameObject;

        LeavesSpawner leafSpawner = instance.GetComponent<LeavesSpawner>();

        if (leafSpawner != null)
        {
            leafSpawner.SpawnAreaSize = new Vector2(targetScale.x, targetScale.y);
            Debug.Log($"Nivel {currentLevelIndex}: SpawnAreaSize = {leafSpawner.SpawnAreaSize}");

            leafSpawner.grassParent = parent;
            if (leafSpawner.grassCount == -1) { 
                leafSpawner.grassCount = Mathf.Max((int)(targetScale.x * targetScale.y * 0.7f), 20);
            }

            leafSpawner.Activate();
        }
        else
        {
            Debug.LogWarning($"El prefab {prefabToSpawn.name} no tiene componente SpawnHojas");
        }
    }

    public void AdvanceToNextLevel()
    {
        Debug.Log($"Avanzando del nivel {currentLevelIndex} al nivel {currentLevelIndex + 1}");

        // Destruir todas las hojas actuales
        DestroyCurrentLeaves();

        // Avanzar al siguiente nivel
        currentLevelIndex++;

        // Verificar si hay más niveles
        if (currentLevelIndex >= prefabsToSpawn.Count)
        {
            Debug.Log("¡Felicitaciones! Has completado todos los niveles.");
            OnAllLevelsCompleted();
            return;
        }

        // Spawnear el siguiente nivel
        SpawnNewObject();
    }

    private void DestroyCurrentLeaves()
    {
        if (currentLeavesContainer != null)
        {

            // Destruir cada hoja individual
            foreach (Transform leaf in currentLeavesContainer.transform)
            {
                if (leaf != null)
                {
                    Destroy(leaf.gameObject);
                }
            }

            // Destruir el contenedor completo
            //Destroy(currentLeavesContainer);
            //currentLeavesContainer = null;
        }
    }

    public void ResetToFirstLevel()
    {
        DestroyCurrentLeaves();
        currentLevelIndex = 0;
        SpawnNewObject();
    }

    private void OnAllLevelsCompleted()
    {
        // Aquí puedes poner lo que quieras que pase cuando se completen todos los niveles
        // Por ejemplo: mostrar un menú de felicitaciones, estadísticas, etc.
        Debug.Log("Todos los niveles completados. Puedes agregar aquí tu lógica de finalización.");
    }

    // Easing function para animación más suave
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    // Método público para obtener información del nivel actual
    public int GetCurrentLevelIndex()
    {
        return currentLevelIndex;
    }

    public int GetTotalLevels()
    {
        return prefabsToSpawn.Count;
    }

    public string GetLevelProgress()
    {
        return $"Nivel {currentLevelIndex + 1} de {prefabsToSpawn.Count}";
    }
}