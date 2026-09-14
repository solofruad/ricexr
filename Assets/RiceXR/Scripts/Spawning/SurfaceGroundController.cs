using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using RiceXR.Core;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Terreno flotante ("isla de pasto") que se despliega sobre el plano seleccionado para que las
/// hojas no nazcan sobre la mesa o el piso desnudos. Es solo presentación: sin colliders ni input.
///
/// Flujo:
/// - FlowState DisablingPlanes (el jugador acaba de tocar un plano) → Deploy()
/// - "Iniciar y omitir selección de planos" → Deploy() con el plano cacheado
/// - Volver al menú → Retract()
/// El terreno se queda durante todos los niveles; solo cambian las hojas.
///
/// Notas:
/// - La pose sale de SceneInteractionManager (SelectedSurfacePosition/Size): es el mismo origen
///   donde nacen las hojas, así que la tapa (a topLift) entierra un poco su base.
/// - La malla la genera GroundMeshBuilder (RiceXR.Core) con una semilla nueva en cada despliegue.
/// - La decoración normaliza cada modelo por sus bounds: la escala y la posición del modelo se
///   ignoran y solo cuenta la altura objetivo en metros.
/// - La decoración va en un contenedor hermano de la isla para que la escala no uniforme de la
///   animación no la deforme.
/// - Los tiempos van en corrutinas: DOVirtual.DelayedCall falla en Android/IL2CPP.
/// - El prefab debe quedar en la raíz de la escena con transform identidad.
/// </summary>
public class SurfaceGroundController : MonoBehaviour
{
    [System.Serializable]
    public class GroundDecoration
    {
        public string label;

        [Tooltip("Modelos (FBX o prefabs). Se usa su primer MeshFilter; su transform se ignora.")]
        public GameObject[] models;

        [Tooltip("Si se deja vacío, se usan los materiales del modelo.")]
        public Material materialOverride;

        [Tooltip("Grupos por metro cuadrado de terreno.")]
        [Min(0f)] public float densityPerSquareMeter = 10f;

        [Tooltip("Piezas por grupo (mín, máx). 1-1 = piezas sueltas.")]
        public Vector2Int clusterSize = new Vector2Int(1, 1);

        [Tooltip("Separación máxima de las piezas respecto al centro del grupo (m).")]
        [Min(0f)] public float clusterRadius = 0.02f;

        [Tooltip("Altura final de cada pieza en metros (mín, máx).")]
        public Vector2 heightRange = new Vector2(0.05f, 0.1f);

        [Tooltip("Inclinación hacia fuera del grupo, en grados (mín, máx).")]
        public Vector2 tiltRange = new Vector2(0f, 10f);

        [Tooltip("Distancia máxima al centro como fracción del contorno (evita el labio del borde).")]
        [Range(0f, 1f)] public float maxRadius = 0.8f;

        [Tooltip("Cuánto se entierra la base de cada pieza (m).")]
        [Min(0f)] public float sinkDepth = 0.004f;
    }

    private struct ModelInfo
    {
        public Mesh Mesh;
        public Material[] Materials;
        public Quaternion Rotation;
        public Vector3 BottomCenter;
        public float Height;
    }

    private struct Piece
    {
        public Transform Transform;
        public float Delay;
    }

    public static SurfaceGroundController Instance { get; private set; }

    [Header("Materiales")]
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Material rootMaterial;

    [Header("Forma (metros)")]
    [Tooltip("2 = elipse; más alto = rectángulo con esquinas más marcadas.")]
    [SerializeField, Range(2f, 12f)] private float sharpness = 5f;
    [SerializeField] private float rimInset = 0.03f;
    [SerializeField] private float topLift = 0.008f;
    [SerializeField] private float lipDrop = 0.015f;
    [Tooltip("Profundidad de la raíz como fracción del lado corto del plano; se limita a rootDepthRange.")]
    [SerializeField, Range(0.05f, 1f)] private float rootDepthRatio = 0.35f;
    [SerializeField] private Vector2 rootDepthRange = new Vector2(0.12f, 0.25f);
    [SerializeField, Range(16, 128)] private int segments = 56;
    [SerializeField] private float surfaceJitter = 0.003f;
    [SerializeField] private float rootJitter = 0.02f;

    [Header("Decoración")]
    [SerializeField] private List<GroundDecoration> decorations = new List<GroundDecoration>();
    [Tooltip("Tope total de piezas (Quest).")]
    [SerializeField, Min(0)] private int maxDecorations = 180;

    [Header("Animación (segundos)")]
    [SerializeField] private float spreadDuration = 0.7f;
    [SerializeField] private float rootDelay = 0.15f;
    [SerializeField] private float rootDuration = 0.6f;
    [SerializeField] private float decorationDelay = 0.35f;
    [SerializeField] private float decorationWaveDuration = 1f;
    [SerializeField] private float decorationPopDuration = 0.35f;
    [SerializeField] private float retractDuration = 0.35f;

    private const float CollapsedScale = 0.001f;

    private readonly Dictionary<GameObject, ModelInfo> _modelCache = new Dictionary<GameObject, ModelInfo>();
    private readonly List<Piece> _pieces = new List<Piece>();

    private Transform _groundRoot;
    private Transform _island;
    private Transform _decorationRoot;
    private Mesh _mesh;
    private Coroutine _routine;

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        GameEventBus.OnFlowStateChanged += HandleFlowStateChanged;
        GameEventBus.OnSessionStartRequested += HandleSessionStartRequested;
        GameEventBus.OnReturnToMenuRequested += HandleReturnToMenuRequested;
    }

    private void OnDisable()
    {
        GameEventBus.OnFlowStateChanged -= HandleFlowStateChanged;
        GameEventBus.OnSessionStartRequested -= HandleSessionStartRequested;
        GameEventBus.OnReturnToMenuRequested -= HandleReturnToMenuRequested;
        StopRoutine();
    }

    private void OnDestroy()
    {
        KillTweens();
        if (_mesh != null) Destroy(_mesh);
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────
    // Handlers del bus
    // ─────────────────────────────────────────────

    private void HandleFlowStateChanged(FlowState previous, FlowState next, string reason)
    {
        // GameFlowController solo entra a DisablingPlanes tras una selección válida, y ya le pasó
        // el plano a SceneInteractionManager antes de la transición.
        if (next == FlowState.DisablingPlanes)
            Deploy();
    }

    private void HandleSessionStartRequested(bool skipPlaneSelection)
    {
        SceneInteractionManager manager = SceneInteractionManager.Instance;
        if (skipPlaneSelection && manager != null && manager.HasSelectedPlane)
            Deploy();
        else
            Retract();
    }

    private void HandleReturnToMenuRequested() => Retract();

    // ─────────────────────────────────────────────
    // API pública
    // ─────────────────────────────────────────────

    /// <summary>Genera un terreno nuevo sobre el plano seleccionado y lo anima. Reemplaza al anterior.</summary>
    public void Deploy()
    {
        SceneInteractionManager manager = SceneInteractionManager.Instance;
        if (manager == null || !manager.HasSelectedPlane)
        {
            Debug.LogWarning("[SurfaceGround] No hay plano seleccionado; no se despliega el terreno.", this);
            return;
        }

        ClearImmediate();

        Vector2 size = manager.SelectedSurfaceSize;
        if (size.x < 0.05f || size.y < 0.05f)
        {
            Debug.LogWarning($"[SurfaceGround] Plano demasiado pequeño para el terreno: {size}.", this);
            return;
        }

        GroundShape shape = BuildShape(size);
        BuildGround(shape, manager.SelectedSurfacePosition, manager.SelectedPlaneRotation);
        _routine = StartCoroutine(DeployRoutine());
    }

    /// <summary>Recoge el terreno con animación y lo destruye al terminar.</summary>
    public void Retract()
    {
        if (_groundRoot == null) return;
        StopRoutine();
        _routine = StartCoroutine(RetractRoutine());
    }

    // ─────────────────────────────────────────────
    // Construcción
    // ─────────────────────────────────────────────

    private GroundShape BuildShape(Vector2 size)
    {
        float shortSide = Mathf.Min(size.x, size.y);
        return new GroundShape
        {
            Width = size.x,
            Depth = size.y,
            Sharpness = sharpness,
            RimInset = Mathf.Min(rimInset, shortSide * 0.1f),
            TopLift = topLift,
            LipDrop = lipDrop,
            RootDepth = Mathf.Clamp(shortSide * rootDepthRatio, rootDepthRange.x, rootDepthRange.y),
            Segments = segments,
            SurfaceJitter = surfaceJitter,
            RootJitter = rootJitter,
            Seed = Random.Range(0, 100000)
        };
    }

    private void BuildGround(in GroundShape shape, Vector3 position, Quaternion rotation)
    {
        _groundRoot = new GameObject("SurfaceGroundInstance").transform;
        _groundRoot.SetParent(transform, false);
        _groundRoot.SetPositionAndRotation(position, rotation);

        _mesh = CreateMesh(GroundMeshBuilder.Build(shape));

        var island = new GameObject("Island", typeof(MeshFilter), typeof(MeshRenderer));
        _island = island.transform;
        _island.SetParent(_groundRoot, false);
        island.GetComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = island.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { grassMaterial, rootMaterial };
        ConfigureRenderer(renderer);

        _decorationRoot = new GameObject("Decoration").transform;
        _decorationRoot.SetParent(_groundRoot, false);
        SpawnDecorations(shape);
    }

    private static Mesh CreateMesh(GroundMeshData data)
    {
        int count = data.VertexCount;
        var vertices = new Vector3[count];
        var uvs = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
            uvs[i] = new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]);
        }

        var mesh = new Mesh { name = "SurfaceGround" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(data.TopTriangles, 0);
        mesh.SetTriangles(data.RootTriangles, 1);
        mesh.RecalculateNormals();

        // Los vértices duplicados de la costura de la raíz reciben normales distintas: se promedian
        // para que no se vea una línea vertical en la roca.
        Vector3[] normals = mesh.normals;
        for (int i = 0; i < data.SeamPairs.Length; i += 2)
        {
            int a = data.SeamPairs[i];
            int b = data.SeamPairs[i + 1];
            Vector3 shared = (normals[a] + normals[b]).normalized;
            normals[a] = shared;
            normals[b] = shared;
        }
        mesh.normals = normals;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void SpawnDecorations(in GroundShape shape)
    {
        float usableArea = shape.Width * shape.Depth * 0.8f;

        int requested = 0;
        foreach (GroundDecoration entry in decorations)
        {
            if (!IsUsable(entry)) continue;
            float averageSize = (Mathf.Max(1, entry.clusterSize.x) + Mathf.Max(1, entry.clusterSize.y)) * 0.5f;
            requested += Mathf.RoundToInt(ClusterCount(entry, usableArea) * averageSize);
        }
        float budget = requested > maxDecorations ? maxDecorations / (float)requested : 1f;

        foreach (GroundDecoration entry in decorations)
        {
            if (!IsUsable(entry)) continue;

            int clusters = Mathf.FloorToInt(ClusterCount(entry, usableArea) * budget);
            for (int c = 0; c < clusters; c++)
            {
                GroundMeshBuilder.SamplePoint(shape, Random.value, Random.value, entry.maxRadius,
                    out float centerX, out float centerZ);

                float normalizedRadius = Mathf.Clamp01(Mathf.Sqrt(
                    Square(centerX / (shape.Width * 0.5f)) + Square(centerZ / (shape.Depth * 0.5f))));

                int minSize = Mathf.Max(1, entry.clusterSize.x);
                int size = Random.Range(minSize, Mathf.Max(minSize, entry.clusterSize.y) + 1);
                float startAngle = Random.Range(0f, 360f);

                for (int k = 0; k < size; k++)
                {
                    GameObject model = entry.models[Random.Range(0, entry.models.Length)];
                    if (!TryGetModel(model, out ModelInfo info)) continue;

                    // Cada pieza del grupo mira hacia fuera y se inclina en esa dirección: un
                    // puñado de briznas se abre como una mata.
                    float yaw = startAngle + k * 360f / size + Random.Range(-20f, 20f);
                    Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
                    float distance = size > 1 ? entry.clusterRadius * Random.Range(0.3f, 1f) : 0f;

                    Vector3 position = new Vector3(centerX, shape.TopLift - entry.sinkDepth, centerZ)
                        + yawRotation * Vector3.forward * distance;
                    Quaternion rotation = yawRotation
                        * Quaternion.Euler(Random.Range(entry.tiltRange.x, entry.tiltRange.y), 0f, 0f);
                    float height = Random.Range(entry.heightRange.x, entry.heightRange.y);

                    _pieces.Add(new Piece
                    {
                        Transform = CreatePiece(info, entry.materialOverride, position, rotation, height),
                        Delay = normalizedRadius * decorationWaveDuration + Random.Range(0f, 0.08f)
                    });
                }
            }
        }

        _pieces.Sort((a, b) => a.Delay.CompareTo(b.Delay));
    }

    private Transform CreatePiece(in ModelInfo info, Material materialOverride, Vector3 localPosition,
        Quaternion localRotation, float height)
    {
        // Pivote en la base: la animación de escala hace brotar la pieza desde el suelo.
        var pivot = new GameObject($"Decoration_{info.Mesh.name}").transform;
        pivot.SetParent(_decorationRoot, false);
        pivot.localPosition = localPosition;
        pivot.localRotation = localRotation;
        pivot.localScale = Vector3.zero;

        float scale = height / info.Height;
        var visual = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(pivot, false);
        visual.transform.localRotation = info.Rotation;
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = -info.BottomCenter * scale;

        visual.GetComponent<MeshFilter>().sharedMesh = info.Mesh;
        var renderer = visual.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = materialOverride != null
            ? Repeat(materialOverride, info.Mesh.subMeshCount)
            : info.Materials;
        ConfigureRenderer(renderer);

        return pivot;
    }

    private bool TryGetModel(GameObject model, out ModelInfo info)
    {
        info = default;
        if (model == null) return false;
        if (_modelCache.TryGetValue(model, out info)) return info.Mesh != null;

        MeshFilter filter = model.GetComponentInChildren<MeshFilter>(true);
        if (filter != null && filter.sharedMesh != null)
        {
            // La rotación del asset incluye la conversión de ejes de Blender; la escala y la
            // posición se descartan y se sustituyen por la altura objetivo.
            Quaternion rotation = filter.transform.rotation;
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;

            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = rotation * (bounds.center + Vector3.Scale(bounds.extents, sign));
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            float height = max.y - min.y;
            if (height > 0.0001f)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                info = new ModelInfo
                {
                    Mesh = filter.sharedMesh,
                    Materials = renderer != null ? renderer.sharedMaterials : null,
                    Rotation = rotation,
                    BottomCenter = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f),
                    Height = height
                };
            }
        }

        if (info.Mesh == null)
            Debug.LogWarning($"[SurfaceGround] {model.name} no tiene un MeshFilter con malla válida.", this);

        _modelCache[model] = info;
        return info.Mesh != null;
    }

    // ─────────────────────────────────────────────
    // Animación
    // ─────────────────────────────────────────────

    private IEnumerator DeployRoutine()
    {
        Transform island = _island;
        island.localScale = Vector3.one * CollapsedScale;

        // 1) La tapa se abre desde el centro.
        island.DOScaleX(1f, spreadDuration).SetEase(Ease.OutBack).SetLink(island.gameObject, LinkBehaviour.KillOnDestroy);
        island.DOScaleZ(1f, spreadDuration).SetEase(Ease.OutBack).SetLink(island.gameObject, LinkBehaviour.KillOnDestroy);

        // 2) La raíz "cae" hacia abajo un poco después.
        yield return new WaitForSeconds(rootDelay);
        island.DOScaleY(1f, rootDuration).SetEase(Ease.OutBack).SetLink(island.gameObject, LinkBehaviour.KillOnDestroy);

        // 3) La decoración brota en ola, del centro al borde.
        yield return new WaitForSeconds(Mathf.Max(0f, decorationDelay - rootDelay));

        float start = Time.time;
        int next = 0;
        while (next < _pieces.Count)
        {
            float elapsed = Time.time - start;
            while (next < _pieces.Count && _pieces[next].Delay <= elapsed)
            {
                Transform piece = _pieces[next].Transform;
                if (piece != null)
                {
                    piece.DOKill();
                    piece.DOScale(1f, decorationPopDuration)
                        .SetEase(Ease.OutBack)
                        .SetLink(piece.gameObject, LinkBehaviour.KillOnDestroy);
                }
                next++;
            }
            yield return null;
        }

        _routine = null;
    }

    private IEnumerator RetractRoutine()
    {
        float piecesDuration = retractDuration * 0.6f;
        foreach (Piece piece in _pieces)
        {
            if (piece.Transform == null) continue;
            piece.Transform.DOKill();
            piece.Transform.DOScale(0f, piecesDuration)
                .SetEase(Ease.InBack)
                .SetLink(piece.Transform.gameObject, LinkBehaviour.KillOnDestroy);
        }

        Transform island = _island;
        island.DOKill();
        island.DOScaleY(CollapsedScale, retractDuration).SetEase(Ease.InBack).SetLink(island.gameObject, LinkBehaviour.KillOnDestroy);

        yield return new WaitForSeconds(retractDuration * 0.5f);
        island.DOScaleX(CollapsedScale, retractDuration).SetEase(Ease.InBack).SetLink(island.gameObject, LinkBehaviour.KillOnDestroy);
        island.DOScaleZ(CollapsedScale, retractDuration).SetEase(Ease.InBack).SetLink(island.gameObject, LinkBehaviour.KillOnDestroy);

        yield return new WaitForSeconds(retractDuration);
        _routine = null;
        ClearImmediate();
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    private void ClearImmediate()
    {
        StopRoutine();
        KillTweens();

        if (_groundRoot != null) Destroy(_groundRoot.gameObject);
        if (_mesh != null) Destroy(_mesh);

        _groundRoot = null;
        _island = null;
        _decorationRoot = null;
        _mesh = null;
        _pieces.Clear();
    }

    private void KillTweens()
    {
        if (_island != null) _island.DOKill();
        foreach (Piece piece in _pieces)
            if (piece.Transform != null) piece.Transform.DOKill();
    }

    private void StopRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }

    private static void ConfigureRenderer(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static bool IsUsable(GroundDecoration entry)
        => entry != null && entry.models != null && entry.models.Length > 0 && entry.densityPerSquareMeter > 0f;

    private static float ClusterCount(GroundDecoration entry, float area)
        => area * entry.maxRadius * entry.maxRadius * entry.densityPerSquareMeter;

    private static float Square(float value) => value * value;

    private static Material[] Repeat(Material material, int count)
    {
        var materials = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        return materials;
    }
}
