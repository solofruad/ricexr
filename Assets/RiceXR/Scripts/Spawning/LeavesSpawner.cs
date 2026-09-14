using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RiceXR.Core;

/// <summary>
/// Clase que gestiona la generacion y animacion de hojas en un area definida.
///
/// Funcionalidades principales:
/// 1. Genera multiples instancias de prefabs de hojas dentro de un area rectangular personalizable
/// 2. Implementa un sistema de spawn progresivo con animaciones de crecimiento
/// 3. Aplica variaciones aleatorias en posicion, rotacion, escala y tiempos de animacion
/// 4. Proporciona controles de optimizacion mediante el uso de un contenedor padre organizado
/// 5. Incluye capacidad de respawn y regeneracion de toda la vegetacion
///
/// Flujo de trabajo:
/// - SceneInteractionManager llama a ConfigureArea() con el tamano del plano y luego a Activate()
/// - Al activarse, genera posiciones aleatorias dentro del area de spawn definida por SpawnAreaSize
/// - Crea instancias de los prefabs de vegetacion pero las inicializa con escala cero (invisibles)
/// - Programa un sistema de delay escalonado para el inicio de las animaciones
/// - Ejecuta animaciones de crecimiento suaves usando DOTween con AnimationCurve como ease
/// - Mantiene una lista interna de todas las instancias para permitir su gestion centralizada
///
/// Notas importantes:
/// - El area de spawn se rota segun la rotacion del GameObject padre
/// - SpawnAreaSize se expresa en metros del mundo y se convierte a coordenadas
///   locales para que la escala de la jerarquia no reduzca el area cubierta
/// - Las animaciones usan variaciones aleatorias para evitar patrones repetitivos
/// - El sistema es eficiente al crear todas las instancias al inicio y luego solo animarlas
/// - Con isGrassAbleToShowMarker en false las hojas nacen sin marcadores, para que el
///   tutorial guiado los revele cuando corresponda (ver TutorialPanelController)
/// </summary>
public class LeavesSpawner : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private GameObject[] grassPrefabs;
    [SerializeField] private DiseaseCatalog diseaseCatalog;
    private int minimumDiagnosable = 1;

    public void ConfigureDiagnosis(int required) => minimumDiagnosable = Mathf.Max(1, required);

    public bool CanDiagnoseWith(DiseaseCatalog catalog, out string error)
    {
        error = "El spawner debe usar el mismo catálogo válido que el selector.";
        if (catalog == null || diseaseCatalog != catalog || !catalog.IsValid(out _)) return false;
        if (grassPrefabs != null) foreach (var prefab in grassPrefabs)
        {
            var leaf = prefab != null ? prefab.GetComponentInChildren<Leaf>(true) : null;
            if (leaf != null && leaf.IsDiagnosable(catalog)) { error = null; return true; }
        }
        error = "No hay hojas diagnosticables con las severidades habilitadas.";
        return false;
    }
    [SerializeField] public int grassCount = -1;
    public bool isGrassAbleToShowMarker = true;
    [SerializeField] public Vector2 SpawnAreaSize;


    [Tooltip("Margen de seguridad para evitar que las hojas se salgan del borde (en metros).")]
    [SerializeField] private float safeMargin = 0.15f;

    [Header("Animation Settings")]
    [SerializeField] private float totalSpawnDuration = 10f;
    [SerializeField] private float growDuration = 4f;
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Variation")]
    [SerializeField] private float spawnTimeVariation = 0.3f;
    [SerializeField] private float growTimeVariation = 0.5f;
    [SerializeField] private float scaleVariation = 0.2f;
    [SerializeField] private float rotationVariation = 15f;

    [Header("Optimization")]
    [SerializeField] public Transform grassParent;

    private List<Transform> activeGrass = new List<Transform>();
    private Coroutine initializationRoutine;
    private bool activationRequested;

    private void Start()
    {
        // El manager configura el area y llama a Activate() justo despues del
        // Instantiate, es decir antes de que corra este Start. Sin estas guardas
        // el spawner se activaria dos veces y generaria el doble de hojas.
        if (!activationRequested && SpawnAreaSize.sqrMagnitude > 0.0001f)
            Activate();
    }

    /// <summary>
    /// Permite al SceneInteractionManager aplicar el tamano mundial del plano antes del spawn.
    /// La misma llamada sirve despues para un ancla virtual.
    /// </summary>
    public void ConfigureArea(Vector2 areaSize, Transform parent)
    {
        SpawnAreaSize = areaSize;
        grassParent = parent;
    }

    public void Activate()
    {
        if (initializationRoutine != null)
            StopCoroutine(initializationRoutine);

        activationRequested = true;
        initializationRoutine = StartCoroutine(InitializeGrass());
    }

    private IEnumerator InitializeGrass()
    {
        if (grassParent == null)
        {
            GameObject parent = new GameObject("Grass_Parent");
            grassParent = parent.transform;
            grassParent.SetParent(transform);
        }

        if (diseaseCatalog == null || !diseaseCatalog.IsValid(out _))
        {
            Debug.LogError("[LeavesSpawner] Asigna un catálogo de enfermedades válido.", this);
            yield break;
        }
        var eligible = new List<GameObject>();
        var diagnosable = new List<bool>();
        if (grassPrefabs != null) foreach (var prefab in grassPrefabs)
        {
            if (prefab == null) continue;
            var candidate = prefab.GetComponentInChildren<Leaf>(true);
            if (candidate == null) continue;
            bool canDiagnose = candidate.IsDiagnosable(diseaseCatalog);
            if (!canDiagnose && !candidate.IsHealthy) continue;
            eligible.Add(prefab);
            diagnosable.Add(canDiagnose);
        }
        if (!diagnosable.Contains(true))
        {
            Debug.LogError("[LeavesSpawner] El nivel no tiene hojas diagnosticables con este catálogo.", this);
            yield break;
        }
        grassCount = Mathf.Max(minimumDiagnosable, grassCount);
        List<Vector3> spawnPositions = GenerateSpawnPositions();

        int spawnCount = spawnPositions.Count;
        if (grassCount > 0)
            spawnCount = Mathf.Min(grassCount, spawnPositions.Count);

        if (spawnCount == 0) yield break;
        int[] plan = LeafSpawnPlan.Create(diagnosable, spawnCount, minimumDiagnosable,
            new System.Random(Random.Range(0, int.MaxValue)));

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = eligible[plan[i]];
            float randomYRotation = Random.Range(-rotationVariation, rotationVariation);
            float parentYRotation = transform.eulerAngles.y;
            Quaternion spawnRotation = Quaternion.Euler(0, parentYRotation + randomYRotation, 0);
            GameObject grass = Instantiate(prefab, spawnPositions[i], spawnRotation, grassParent);

            // El true incluye hijos desactivados: algunos prefabs de hoja arrancan apagados.
            var leaf = grass.GetComponentInChildren<Leaf>(true);
            if (leaf != null)
            {
                leaf.ableToShowMarkers = isGrassAbleToShowMarker;

                // Los markers del prefab pueden venir activos: apagarlos aqui evita
                // el frame visible que habria antes de que corra Leaf.Start().
                if (!isGrassAbleToShowMarker || leaf.IsHealthy)
                    leaf.HideMarkersImmediate();
            }

            Vector3 baseScale = grass.transform.localScale;
            float scaleMultiplier = 1f + Random.Range(-scaleVariation, scaleVariation);
            Vector3 finalScale = baseScale * scaleMultiplier;

            grass.transform.localScale = Vector3.zero;

            float normalizedIndex = i / (float)Mathf.Max(1, spawnCount - 1);
            float spawnDelay = (normalizedIndex * totalSpawnDuration) + Random.Range(-spawnTimeVariation, spawnTimeVariation);
            spawnDelay = Mathf.Max(0f, spawnDelay);

            float instanceGrowDuration = growDuration + Random.Range(-growTimeVariation, growTimeVariation);
            instanceGrowDuration = Mathf.Max(0.001f, instanceGrowDuration);

            activeGrass.Add(grass.transform);

            StartCoroutine(SpawnGrassRoutine(grass.transform, finalScale, instanceGrowDuration, spawnDelay));
        }

        yield return null;
        initializationRoutine = null;
    }

    private IEnumerator SpawnGrassRoutine(Transform grassTransform, Vector3 finalScale, float duration, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (grassTransform != null)
        {
            grassTransform
                .DOScale(finalScale, duration)
                .SetEase(growCurve)
                .SetLink(grassTransform.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    private List<Vector3> GenerateSpawnPositions()
    {
        int targetCount = (grassCount > 0) ? grassCount : 1;
        List<Vector3> positions = new List<Vector3>(targetCount);

        float halfX = Mathf.Max(0f, SpawnAreaSize.x * 0.5f - safeMargin);
        float halfZ = Mathf.Max(0f, SpawnAreaSize.y * 0.5f - safeMargin);

        for (int i = 0; i < targetCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-halfX, halfX),
                0f,
                Random.Range(-halfZ, halfZ));

            positions.Add(transform.position + transform.rotation * offset);
        }

        return positions;
    }

    public void RespawnGrass()
    {
        StopAllCoroutines();
        initializationRoutine = null;

        foreach (var t in activeGrass)
        {
            if (t != null)
            {
                t.DOKill();
                Destroy(t.gameObject);
            }
        }

        activeGrass.Clear();
        Activate();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        initializationRoutine = null;
    }

    private void OnDestroy()
    {
        foreach (var t in activeGrass)
        {
            if (t != null) t.DOKill();
        }
    }
}
