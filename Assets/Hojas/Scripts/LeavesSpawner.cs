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
/// - Incluye visualizacion del area de spawn en el editor mediante Gizmos
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

        int spawnCount = spawnPositions.Count;
        if (grassCount > 0)
            spawnCount = Mathf.Min(grassCount, spawnPositions.Count);

        if (spawnCount == 0) yield break;
        if (grassPrefabs == null || grassPrefabs.Length == 0) yield break;

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
            GameObject grass = Instantiate(prefab, spawnPositions[i], Quaternion.identity, grassParent);

            var leaf = grass.GetComponentInChildren<Leaf>();
            if (leaf != null) leaf.ableToShowMarkers = isGrassAbleToShowMarker;

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

            grass.transform
                .DOScale(finalScale, instanceGrowDuration)
                .SetDelay(spawnDelay)
                .SetEase(growCurve)
                .OnComplete(() => Debug.Log("Hoja creci� completamente."));
        }

        yield return null;
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

            // Se calcula el �rea efectiva restando el margen a cada lado
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
        foreach (var t in activeGrass)
        {
            if (t != null)
            {
                t.DOKill();
                Destroy(t.gameObject);
            }
        }

        activeGrass.Clear();
        StartCoroutine(InitializeGrass());
    }

    private void OnDestroy()
    {
        foreach (var t in activeGrass)
        {
            if (t != null) t.DOKill();
        }
    }
}