using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

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
/// - Las animaciones usan variaciones aleatorias para evitar patrones repetitivos
/// - El sistema es eficiente al crear todas las instancias al inicio y luego solo animarlas
/// - Con isGrassAbleToShowMarker en false las hojas nacen sin marcadores, para que el
///   tutorial guiado los revele cuando corresponda (ver TutorialPanelController)
/// </summary>
public class LeavesSpawner : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private GameObject[] grassPrefabs;
    [SerializeField] public int grassCount = -1;
    public bool isGrassAbleToShowMarker = true;
    [SerializeField] public Vector2 SpawnAreaSize;


    [Tooltip("Margen de seguridad para evitar que las hojas se salgan del borde (en metros).")]
    [SerializeField] private float safeMargin = 0.1f;

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
    /// Permite al SceneInteractionManager aplicar el tamano del plano antes del spawn.
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

        List<Vector3> spawnPositions = GenerateSpawnPositions();

        int spawnCount = spawnPositions.Count;
        if (grassCount > 0)
            spawnCount = Mathf.Min(grassCount, spawnPositions.Count);

        if (spawnCount == 0) yield break;
        if (grassPrefabs == null || grassPrefabs.Length == 0) yield break;

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
            GameObject grass = Instantiate(prefab, spawnPositions[i], Quaternion.identity, grassParent);

            // El true incluye hijos desactivados: algunos prefabs de hoja arrancan apagados.
            var leaf = grass.GetComponentInChildren<Leaf>(true);
            if (leaf != null)
            {
                leaf.ableToShowMarkers = isGrassAbleToShowMarker;

                // Los markers del prefab pueden venir activos: apagarlos aqui evita
                // el frame visible que habria antes de que corra Leaf.Start().
                if (!isGrassAbleToShowMarker)
                    leaf.HideMarkersImmediate();
            }

            float randomYRotation = Random.Range(-rotationVariation, rotationVariation);
            float parentYRotation = transform.eulerAngles.y;
            grass.transform.rotation = Quaternion.Euler(0, parentYRotation + randomYRotation, 0);

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
        List<Vector3> positions = new List<Vector3>();
        int attempts = 0;
        int targetCount = (grassCount > 0) ? grassCount : 1;
        int maxAttempts = Mathf.Max(1, targetCount * 3);

        while (positions.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            // Se calcula el area efectiva restando el margen a cada lado
            float effectiveWidthX = Mathf.Max(0, SpawnAreaSize.x - (safeMargin * 2));
            float effectiveWidthZ = Mathf.Max(0, SpawnAreaSize.y - (safeMargin * 2));

            float randomX = Random.Range(-effectiveWidthX / 2f, effectiveWidthX / 2f);
            float randomZ = Random.Range(-effectiveWidthZ / 2f, effectiveWidthZ / 2f);

            Vector3 localPos = new Vector3(randomX, 0, randomZ);
            Vector3 rotatedPos = transform.TransformPoint(localPos);

            positions.Add(rotatedPos);
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
