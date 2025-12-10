using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Clase que gestiona la generación y animación de hojas en un área definida.
/// 
/// Funcionalidades principales:
/// 1. Genera múltiples instancias de prefabs de hojas dentro de un área rectangular personalizable
/// 2. Implementa un sistema de spawn progresivo con animaciones de crecimiento 
/// 3. Aplica variaciones aleatorias en posición, rotación, escala y tiempos de animación
/// 4. Proporciona controles de optimización mediante el uso de un contenedor padre organizado
/// 5. Incluye capacidad de respawn y regeneración de toda la vegetación
/// 
/// Flujo de trabajo:
/// - Al activarse, genera posiciones aleatorias dentro del área de spawn definida por SpawnAreaSize
/// - Crea instancias de los prefabs de vegetación pero las inicializa con escala cero (invisibles)
/// - Programa un sistema de delay escalonado para el inicio de las animaciones
/// - Ejecuta animaciones de crecimiento suaves utilizando AnimationCurve para el control de easing
/// - Mantiene una lista interna de todas las instancias para permitir su gestión centralizada
/// 
/// Notas importantes:
/// - El área de spawn se rota según la rotación del GameObject padre
/// - Las animaciones usan variaciones aleatorias para evitar patrones repetitivos
/// - El sistema es eficiente al crear todas las instancias al inicio y luego solo animarlas
/// - Incluye visualización del área de spawn en el editor mediante Gizmos
/// </summary>
public class LeavesSpawner : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private GameObject[] grassPrefabs;
    [SerializeField] public int grassCount = -1;
    public bool isGrassAbleToShowMarker = true;

    [SerializeField] public Vector2 SpawnAreaSize { get; set; }

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

    private class GrassInstance
    {
        public Transform transform;
        public Vector3 originalScale;
        public Vector3 targetPosition;
        public float spawnDelay;
        public float growDuration;
        public bool isGrowing;
        public float growProgress;
    }

    private List<GrassInstance> activeGrass = new List<GrassInstance>();

    private void Start()
    {
        if (grassParent == null)
        {
            GameObject parent = new GameObject("Grass_Parent");
            grassParent = parent.transform;
            grassParent.SetParent(transform);
        }
        Activate();
    }

    public void Activate()
    {
        StartCoroutine(InitializeGrass());
    }

    private IEnumerator InitializeGrass()
    {
        List<Vector3> spawnPositions = GenerateSpawnPositions();

        // Decide how many to spawn (handle inspector values <= 0 gracefully)
        int spawnCount = spawnPositions.Count;
        if (grassCount > 0)
        {
            spawnCount = Mathf.Min(grassCount, spawnPositions.Count);
        }

        if (spawnCount == 0) yield break;

        // Crear todas las hojas de inmediato pero invisibles
        for (int i = 0; i < spawnCount; i++)
        {
            if (grassPrefabs == null || grassPrefabs.Length == 0) yield break;

            GameObject prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
            GameObject grass = Instantiate(prefab, spawnPositions[i], Quaternion.identity, grassParent);
            var leaf = grass.GetComponentInChildren<Leaf>();
            if (leaf != null) leaf.ableToShowMarkers = isGrassAbleToShowMarker;


            // Rotación aleatoria + rotación del padre
            float randomYRotation = Random.Range(-rotationVariation, rotationVariation);
            float parentYRotation = transform.eulerAngles.y;
            grass.transform.rotation = Quaternion.Euler(0, parentYRotation + randomYRotation, 0);

            // Guardar escala original y aplicar variación
            Vector3 baseScale = grass.transform.localScale;
            float scaleMultiplier = 1f + Random.Range(-scaleVariation, scaleVariation);
            Vector3 finalScale = baseScale * scaleMultiplier;

            // Empezar con escala 0
            grass.transform.localScale = Vector3.zero;

            // Calcular delay de spawn
            float normalizedIndex = i / (float)Mathf.Max(1, spawnCount); // use actual spawnCount to avoid divide-by-zero
            float spawnDelay = (normalizedIndex * totalSpawnDuration) + Random.Range(-spawnTimeVariation, spawnTimeVariation);
            spawnDelay = Mathf.Max(0f, spawnDelay);

            // Ensure grow duration is never zero or negative
            float instanceGrowDuration = growDuration + Random.Range(-growTimeVariation, growTimeVariation);
            instanceGrowDuration = Mathf.Max(0.001f, instanceGrowDuration);

            GrassInstance instance = new GrassInstance
            {
                transform = grass.transform,
                originalScale = finalScale,
                targetPosition = spawnPositions[i],
                spawnDelay = spawnDelay,
                growDuration = instanceGrowDuration,
                isGrowing = false,
                growProgress = 0f
            };

            activeGrass.Add(instance);
        }

        // Iniciar animación
        StartCoroutine(AnimateAllGrass());

        yield return null;
    }

    private IEnumerator AnimateAllGrass()
    {
        float elapsedTime = 0f;
        bool allComplete = false;

        while (!allComplete)
        {
            allComplete = true;
            elapsedTime += Time.deltaTime;

            foreach (var grass in activeGrass)
            {
                if (grass.transform == null) continue;

                // Verificar si debe empezar a crecer
                if (!grass.isGrowing && elapsedTime >= grass.spawnDelay)
                {
                    grass.isGrowing = true;
                }

                // If this grass is not yet fully grown, we are not complete
                if (grass.growProgress < 1f)
                {
                    allComplete = false;

                    // Only advance growth when the spawn delay has passed (isGrowing)
                    if (grass.isGrowing)
                    {
                        grass.growProgress += Time.deltaTime / grass.growDuration;
                        grass.growProgress = Mathf.Clamp01(grass.growProgress);

                        float curveValue = growCurve.Evaluate(grass.growProgress);

                        // Aplicar escala con crecimiento vertical
                        Vector3 currentScale = Vector3.Lerp(Vector3.zero, grass.originalScale, curveValue);
                        grass.transform.localScale = currentScale;
                    }
                    // if not yet isGrowing, leave scale at zero until spawnDelay reached
                }
                else if (grass.growProgress >= 1f)
                {
                    // Asegurar valores finales
                    grass.transform.localScale = grass.originalScale;
                    float finalHeightOffset = grass.originalScale.y * 0.5f;
                    //grass.transform.position = grass.targetPosition + new Vector3(0, finalHeightOffset, 0);
                }
            }

            yield return null;
        }

        Debug.Log("¡Todas las hojas han crecido completamente!");
    }

    private List<Vector3> GenerateSpawnPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        int attempts = 0;
        int maxAttempts = Mathf.Max(1, (grassCount > 0 ? grassCount : 1) * 3);

        int targetCount = (grassCount > 0) ? grassCount : 1;
        while (positions.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            float randomX = Random.Range(-SpawnAreaSize.x / 2, SpawnAreaSize.x / 2);
            float randomZ = Random.Range(-SpawnAreaSize.y / 2, SpawnAreaSize.y / 2);

            // Posición local rotada según el transform del padre
            Vector3 localPos = new Vector3(randomX, 0, randomZ);
            Vector3 rotatedPos = transform.TransformPoint(localPos);

            positions.Add(rotatedPos);
        }

        return positions;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        // Dibujar área rotada
        Vector3 c1 = transform.TransformPoint(new Vector3(-SpawnAreaSize.x / 2, 0, -SpawnAreaSize.y / 2));
        Vector3 c2 = transform.TransformPoint(new Vector3(SpawnAreaSize.x / 2, 0, -SpawnAreaSize.y / 2));
        Vector3 c3 = transform.TransformPoint(new Vector3(SpawnAreaSize.x / 2, 0, SpawnAreaSize.y / 2));
        Vector3 c4 = transform.TransformPoint(new Vector3(-SpawnAreaSize.x / 2, 0, SpawnAreaSize.y / 2));

        Gizmos.DrawLine(c1, c2);
        Gizmos.DrawLine(c2, c3);
        Gizmos.DrawLine(c3, c4);
        Gizmos.DrawLine(c4, c1);
    }

    public void RespawnGrass()
    {
        StopAllCoroutines();

        foreach (var instance in activeGrass)
        {
            if (instance.transform != null)
            {
                Destroy(instance.transform.gameObject);
            }
        }

        activeGrass.Clear();
        StartCoroutine(InitializeGrass());
    }
}