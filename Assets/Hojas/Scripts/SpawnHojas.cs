using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnHojas : MonoBehaviour
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

        // Crear todas las hojas de inmediato pero invisibles
        for (int i = 0; i < Mathf.Min(grassCount, spawnPositions.Count); i++)
        {
            if (grassPrefabs == null || grassPrefabs.Length == 0) yield break;

            GameObject prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
            GameObject grass = Instantiate(prefab, spawnPositions[i], Quaternion.identity, grassParent);
            grass.GetComponentInChildren<Leaf>().ableToShowMarkers = isGrassAbleToShowMarker;


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
            float normalizedIndex = i / (float)grassCount;
            float spawnDelay = (normalizedIndex * totalSpawnDuration) + Random.Range(-spawnTimeVariation, spawnTimeVariation);
            spawnDelay = Mathf.Max(0f, spawnDelay);

            GrassInstance instance = new GrassInstance
            {
                transform = grass.transform,
                originalScale = finalScale,
                targetPosition = spawnPositions[i],
                spawnDelay = spawnDelay,
                growDuration = growDuration + Random.Range(-growTimeVariation, growTimeVariation),
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

                // Animar crecimiento
                if (grass.isGrowing && grass.growProgress < 1f)
                {
                    allComplete = false;

                    grass.growProgress += Time.deltaTime / grass.growDuration;
                    grass.growProgress = Mathf.Clamp01(grass.growProgress);

                    float curveValue = growCurve.Evaluate(grass.growProgress);

                    // Aplicar escala con crecimiento vertical
                    Vector3 currentScale = Vector3.Lerp(Vector3.zero, grass.originalScale, curveValue);
                    grass.transform.localScale = currentScale;

                    // Ajustar posición para que crezca desde abajo
                    //float heightOffset = grass.originalScale.y * curveValue * 0.5f;
                    //grass.transform.position = grass.targetPosition + new Vector3(0, heightOffset, 0);
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
        int maxAttempts = grassCount * 3;

        while (positions.Count < grassCount && attempts < maxAttempts)
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