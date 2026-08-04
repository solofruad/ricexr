using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Spawner exclusivo del nivel tutorial.
/// Todas las instancias usan el mismo prefab para que el jugador practique sobre
/// un diagnostico conocido y no sobre una mezcla de enfermedades/severidades.
/// </summary>
public class TutorialLeavesSpawner : MonoBehaviour
{
    [Header("Hoja fija del tutorial")]
    [SerializeField] private GameObject leafPrefab;
    [SerializeField, Min(1)] private int leafCount = 20;
    [SerializeField] private bool hideMarkersUntilGuided = true;

    [Header("Area")]
    public Vector2 SpawnAreaSize;
    [SerializeField] private float safeMargin = 0.1f;

    [Header("Animacion")]
    [SerializeField] private float totalSpawnDuration = 1f;
    [SerializeField] private float growDuration = 1.4f;
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float spawnTimeVariation = 0.15f;
    [SerializeField] private float growTimeVariation = 0.2f;
    [SerializeField] private float scaleVariation = 0.2f;
    [SerializeField] private float rotationVariation = 15f;

    [Header("Organizacion")]
    public Transform grassParent;

    private readonly List<Transform> _activeLeaves = new List<Transform>();
    private Coroutine _initializationRoutine;
    private bool _activationRequested;

    private void Start()
    {
        // El manager configura el area antes de activar el spawner. Evita crear
        // 20 hojas juntas si este prefab se instancia sin un ancla configurada.
        if (!_activationRequested && SpawnAreaSize.sqrMagnitude > 0.0001f)
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
        if (_initializationRoutine != null)
            StopCoroutine(_initializationRoutine);

        _activationRequested = true;
        _initializationRoutine = StartCoroutine(InitializeLeaves());
    }

    private IEnumerator InitializeLeaves()
    {
        if (leafPrefab == null)
        {
            Debug.LogWarning("[TutorialLeavesSpawner] No hay leafPrefab asignado.");
            yield break;
        }

        if (grassParent == null)
        {
            GameObject parent = new GameObject("TutorialLeaves");
            grassParent = parent.transform;
            grassParent.SetParent(transform, true);
        }

        List<Vector3> positions = GenerateSpawnPositions();
        int spawnCount = Mathf.Min(Mathf.Max(1, leafCount), positions.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject leafObject = Instantiate(leafPrefab, positions[i], Quaternion.identity, grassParent);
            Leaf leaf = leafObject.GetComponentInChildren<Leaf>(true);
            if (leaf != null)
            {
                leaf.showMarkers = false;
                leaf.ableToShowMarkers = !hideMarkersUntilGuided;
                if (hideMarkersUntilGuided)
                    leaf.HideMarkersImmediate();
            }

            float yRotation = transform.eulerAngles.y + Random.Range(-rotationVariation, rotationVariation);
            leafObject.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

            Vector3 baseScale = leafObject.transform.localScale;
            float scaleMultiplier = 1f + Random.Range(-scaleVariation, scaleVariation);
            Vector3 finalScale = baseScale * scaleMultiplier;
            leafObject.transform.localScale = Vector3.zero;
            _activeLeaves.Add(leafObject.transform);

            float normalizedIndex = i / (float)Mathf.Max(1, spawnCount - 1);
            float delay = Mathf.Max(0f,
                normalizedIndex * totalSpawnDuration + Random.Range(-spawnTimeVariation, spawnTimeVariation));
            float duration = Mathf.Max(0.01f, growDuration + Random.Range(-growTimeVariation, growTimeVariation));

            StartCoroutine(GrowLeaf(leafObject.transform, finalScale, duration, delay));
        }

        yield return null;
        _initializationRoutine = null;
    }

    private IEnumerator GrowLeaf(Transform leafTransform, Vector3 finalScale, float duration, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (leafTransform == null)
            yield break;

        leafTransform.DOScale(finalScale, duration)
            .SetEase(growCurve)
            .SetLink(leafTransform.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private List<Vector3> GenerateSpawnPositions()
    {
        int count = Mathf.Max(1, leafCount);
        float width = Mathf.Max(0f, SpawnAreaSize.x - safeMargin * 2f);
        float depth = Mathf.Max(0f, SpawnAreaSize.y - safeMargin * 2f);
        var positions = new List<Vector3>(count);

        for (int i = 0; i < count; i++)
        {
            Vector3 localPosition = new Vector3(
                Random.Range(-width * 0.5f, width * 0.5f),
                0f,
                Random.Range(-depth * 0.5f, depth * 0.5f));
            positions.Add(transform.TransformPoint(localPosition));
        }

        return positions;
    }

    private void OnDestroy()
    {
        if (_initializationRoutine != null)
            StopCoroutine(_initializationRoutine);

        foreach (Transform leaf in _activeLeaves)
            if (leaf != null) leaf.DOKill();
    }
}
