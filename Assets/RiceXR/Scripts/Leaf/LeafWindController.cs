using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el viento visual de todos los meshes de una hoja.
/// El movimiento ocurre en el shader; este componente solo gestiona los
/// parametros por instancia y lo suprime durante la interaccion.
/// </summary>
public class LeafWindController : MonoBehaviour
{
    [Header("Renderers de la hoja")]
    [Tooltip("Si se deja vacio, se detectan automaticamente los MeshRenderer activos fuera de Marker.")]
    [SerializeField] private Renderer[] windRenderers;

    [Header("Interaccion")]
    [Range(0f, 1f)]
    [SerializeField] private float windWeightWhileSelected = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float windWeightWhileMarkersVisible = 0f;
    [Min(0f)]
    [SerializeField] private float restoreDuration = 0.15f;

    private static readonly int WindWeightId = Shader.PropertyToID("_WindWeight");
    private static readonly int WindPhaseId = Shader.PropertyToID("_WindPhase");

    private Leaf _leaf;
    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _restoreRoutine;
    private bool _isSelected;
    private bool _markersVisible;
    private float _currentWeight = 1f;
    private float _phase;

    private void Awake()
    {
        _leaf = GetComponentInChildren<Leaf>(true);
        _propertyBlock = new MaterialPropertyBlock();
        _phase = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * 0.01731f, Mathf.PI * 2f);

        if (windRenderers == null || windRenderers.Length == 0)
            windRenderers = FindWindRenderers();

        ApplyWeight(1f);
    }

    private void OnEnable()
    {
        GrabbableLeafListener.SelectionUpdated += HandleSelectionUpdated;
        GrabbableLeafListener.SelectionCleared += HandleSelectionCleared;

        if (GrabbableLeafListener.Instance != null
            && GrabbableLeafListener.Instance.ActualLeafGrabbed == _leaf)
        {
            SetSelected(true);
        }
    }

    private void OnDisable()
    {
        GrabbableLeafListener.SelectionUpdated -= HandleSelectionUpdated;
        GrabbableLeafListener.SelectionCleared -= HandleSelectionCleared;

        if (_restoreRoutine != null)
        {
            StopCoroutine(_restoreRoutine);
            _restoreRoutine = null;
        }
    }

    /// <summary>
    /// Permite que Leaf mantenga estable la hoja cuando un marcador se muestra
    /// aunque la hoja no este seleccionada, como puede ocurrir en el tutorial.
    /// </summary>
    public void SetMarkersVisible(bool visible)
    {
        _markersVisible = visible;
        UpdateWindWeight();
    }

    private void HandleSelectionUpdated(Leaf selectedLeaf, GrabbableLeafListener.SelectionHand hand, Transform anchor)
    {
        SetSelected(selectedLeaf == _leaf);
    }

    private void HandleSelectionCleared(Leaf releasedLeaf)
    {
        if (releasedLeaf == _leaf)
            SetSelected(false);
    }

    private void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateWindWeight();
    }

    private void UpdateWindWeight()
    {
        float targetWeight = 1f;

        if (_isSelected)
            targetWeight = windWeightWhileSelected;
        else if (_markersVisible)
            targetWeight = windWeightWhileMarkersVisible;

        bool restoring = targetWeight > _currentWeight;
        if (!restoring || restoreDuration <= 0f)
        {
            ApplyWeight(targetWeight);
            return;
        }

        if (_restoreRoutine != null)
            StopCoroutine(_restoreRoutine);

        _restoreRoutine = StartCoroutine(RestoreWindRoutine(targetWeight));
    }

    private IEnumerator RestoreWindRoutine(float targetWeight)
    {
        float startWeight = _currentWeight;
        float elapsed = 0f;

        while (elapsed < restoreDuration)
        {
            elapsed += Time.deltaTime;
            ApplyWeight(Mathf.Lerp(startWeight, targetWeight, elapsed / restoreDuration));
            yield return null;
        }

        ApplyWeight(targetWeight);
        _restoreRoutine = null;
    }

    private void ApplyWeight(float weight)
    {
        _currentWeight = Mathf.Clamp01(weight);

        if (windRenderers == null)
            return;

        foreach (Renderer renderer in windRenderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(WindWeightId, _currentWeight);
            _propertyBlock.SetFloat(WindPhaseId, _phase);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private Renderer[] FindWindRenderers()
    {
        Renderer[] candidates = GetComponentsInChildren<Renderer>(false);
        var result = new List<Renderer>();

        foreach (Renderer candidate in candidates)
        {
            if (!(candidate is MeshRenderer) && !(candidate is SkinnedMeshRenderer))
                continue;

            if (IsInsideMarker(candidate.transform))
                continue;

            result.Add(candidate);
        }

        if (result.Count == 0)
            Debug.LogWarning($"[LeafWind] No se encontraron renderers visuales en {name}.");

        return result.ToArray();
    }

    private bool IsInsideMarker(Transform candidate)
    {
        Transform current = candidate;

        while (current != null && current != transform)
        {
            if (current.name == "Marker")
                return true;

            current = current.parent;
        }

        return false;
    }
}
