using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Oculus.Interaction;
using DG.Tweening;

[System.Serializable]
public class DiseaseSpot
{
    [Tooltip("Nombre de la enfermedad (ej: Pyricularia, Rhynchosporium)")]
    public string diseaseName = "Pyricularia oryzae";

    [Range(1, 5)]
    [Tooltip("Severidad de la infeccion (1-5)")]
    public int severity = 3;

    [Tooltip("Nombre cientifico mostrado en el panel de enfermedad")]
    public string scientificName;

    [Tooltip("GameObject del marcador UIDocument ya posicionado en la escena")]
    public GameObject markerObject;
}

/// <summary>
/// Componente que representa una hoja con capacidad de mostrar manchas de enfermedad.
///
/// Funcionalidades principales:
/// 1. Gestiona múltiples manchas de enfermedad, cada una vinculada a un GameObject UIDocument
///    que el diseñador posiciona manualmente en el editor.
/// 2. Rellena el contenido UXML de cada marcador con los datos del DiseaseSpot asociado.
/// 3. Activa/desactiva los marcadores según showMarkers.
/// 4. Implementa el efecto de quemado (BurnAndDisable) con DOTween.
/// 5. Integra con Oculus Interaction para detectar grab/release y notificar al listener global.
///
/// Uso típico:
/// - Añadir los GameObjects marcadores a la escena y posicionarlos manualmente.
/// - Asignar cada marcador al DiseaseSpot correspondiente en el inspector.
/// - Los marcadores se rellenan y muestran/ocultan automáticamente al inicio.
/// </summary>
public class Leaf : MonoBehaviour
{
    [Header("Manchas de Enfermedad")]
    [Tooltip("Lista de manchas. Cada una referencia su propio marcador UIDocument en la escena.")]
    public List<DiseaseSpot> diseaseSpots = new List<DiseaseSpot>();

    [Header("Visibilidad")]
    public bool showMarkers = true;
    public bool ableToShowMarkers = true;

    [Header("Efecto de Desaparicion (Burn/Dissolve)")]
    [Tooltip("Duracion del efecto en segundos")]
    public float burnDuration = 1.5f;
    [Tooltip("Tiempo de espera antes de empezar a quemarse")]
    public float delayBeforeBurn = 1.0f;

    [SerializeField] private PointableUnityEventWrapper pointableWrapper;

    private bool _isBurning = false;
    private static readonly int BurnProgressId = Shader.PropertyToID("_BurnProgress");

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        PopulateAndShowMarkers();

        if (pointableWrapper != null)
        {
            pointableWrapper.WhenSelect.AddListener(OnSelect);
            pointableWrapper.WhenUnselect.AddListener(OnUnselect);
        }
    }

    // -------------------------------------------------------------------------
    // Marcadores UIDocument
    // -------------------------------------------------------------------------

    /// <summary>
    /// Recorre todos los DiseaseSpots, rellena su UIDocument con los datos
    /// y aplica la visibilidad actual.
    /// </summary>
    public void PopulateAndShowMarkers()
    {
        foreach (DiseaseSpot spot in diseaseSpots)
        {
            if (spot.markerObject == null)
            {
                Debug.LogWarning($"[Leaf] El DiseaseSpot '{spot.diseaseName}' no tiene markerObject asignado.");
                continue;
            }

            FillMarkerUI(spot);
            spot.markerObject.SetActive(showMarkers && ableToShowMarkers);
        }
    }

    /// <summary>
    /// Rellena los elementos del UXML con los datos del spot.
    /// Requiere que el UIDocument tenga un elemento con name="disease-name"
    /// y otro con name="severity-value".
    /// </summary>
    private void FillMarkerUI(DiseaseSpot spot)
    {
        UIDocument doc = spot.markerObject.GetComponentInChildren<UIDocument>();
        if (doc == null)
        {
            Debug.LogWarning($"[Leaf] El markerObject '{spot.markerObject.name}' o sus hijos no tienen el componente UIDocument.");
            return;
        }

        VisualElement root = doc.rootVisualElement;
        if (root == null) return;

        // Nombre de la enfermedad
        Label nameLabel = root.Q<Label>("disease-name");
        if (nameLabel != null)
            nameLabel.text = string.IsNullOrEmpty(spot.scientificName)
                ? spot.diseaseName
                : $"{spot.diseaseName}\n<i>{spot.scientificName}</i>";

        // Valor de severidad
        Label severityLabel = root.Q<Label>("severity-value");
        if (severityLabel != null)
            severityLabel.text = spot.severity.ToString();

        // Barra de severidad: llenamos los segmentos activos con la clase USS
        for (int i = 1; i <= 5; i++)
        {
            VisualElement segment = root.Q<VisualElement>($"seg-{i}");
            if (segment == null) continue;

            segment.EnableInClassList("active", i <= spot.severity);
            segment.EnableInClassList("inactive", i > spot.severity);
        }
    }

    /// <summary>
    /// Activa o desactiva todos los marcadores respetando ableToShowMarkers.
    /// </summary>
    public void SetMarkersVisibility(bool visible)
    {
        if (!ableToShowMarkers) return;

        foreach (DiseaseSpot spot in diseaseSpots)
        {
            if (spot.markerObject != null)
                spot.markerObject.SetActive(visible);
        }
    }

    public void ToggleMarkers()
    {
        showMarkers = !showMarkers;
        SetMarkersVisibility(showMarkers);
    }

    // -------------------------------------------------------------------------
    // Oculus Interaction callbacks
    // -------------------------------------------------------------------------

    private void OnSelect(PointerEvent pointerEvent)
    {
        if (GrabbableLeafListener.Instance == null) return;

        object data = pointerEvent.Data;
        Debug.Log($"Leaf {gameObject.name} selected with data: {data}");

        GrabbableLeafListener.SelectionHand hand = ResolveSelectionHand(data);
        Transform anchor = ResolveSelectionAnchor(data);

        GrabbableLeafListener.Instance.SetActiveSelection(this, hand, anchor);
    }

    private void OnUnselect(PointerEvent pointerEvent)
    {
        if (GrabbableLeafListener.Instance == null) return;
        GrabbableLeafListener.Instance.ClearActiveSelection(this);
    }

    private static GrabbableLeafListener.SelectionHand ResolveSelectionHand(object data)
    {
        if (data == null) return GrabbableLeafListener.SelectionHand.Unknown;

        var handednessProp = data.GetType().GetProperty("Handedness");
        if (handednessProp == null) return GrabbableLeafListener.SelectionHand.Unknown;

        object handedness = handednessProp.GetValue(data);
        if (handedness == null) return GrabbableLeafListener.SelectionHand.Unknown;

        string handednessText = handedness.ToString();
        if (string.Equals(handednessText, "Left", System.StringComparison.OrdinalIgnoreCase))
            return GrabbableLeafListener.SelectionHand.Left;
        if (string.Equals(handednessText, "Right", System.StringComparison.OrdinalIgnoreCase))
            return GrabbableLeafListener.SelectionHand.Right;

        return GrabbableLeafListener.SelectionHand.Unknown;
    }

    private static Transform ResolveSelectionAnchor(object data)
    {
        if (data == null) return null;

        if (data is Component component)
        {
            Transform pinchArea = component.transform.Find("PinchArea");
            return pinchArea != null ? pinchArea : component.transform;
        }

        if (data is Transform t) return t;
        if (data is GameObject go) return go.transform;

        return null;
    }

    // -------------------------------------------------------------------------
    // Burn / Disable
    // -------------------------------------------------------------------------

    /// <summary>
    /// Activa el efecto de quemado animando la propiedad del shader y desactiva la hoja.
    /// </summary>
    public void BurnAndDisable()
    {
        if (_isBurning) return;
        _isBurning = true;

        if (pointableWrapper != null)
            pointableWrapper.enabled = false;

        SetMarkersVisibility(false);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            ReleaseAndDisable();
            return;
        }

        foreach (var renderer in renderers)
            renderer.material.SetFloat(BurnProgressId, 0f);

        DOVirtual.DelayedCall(delayBeforeBurn, () =>
        {
            float val = 0f;
            DOTween.To(() => val, x =>
            {
                val = x;
                foreach (var renderer in renderers)
                    renderer.material.SetFloat(BurnProgressId, x);
            }, 1f, burnDuration)
            .OnComplete(ReleaseAndDisable);
        });
    }

    private void ReleaseAndDisable()
    {
        var grabbable = GetComponent<Grabbable>();
        if (grabbable != null) grabbable.enabled = false;

        if (GrabbableLeafListener.Instance != null)
            GrabbableLeafListener.Instance.ClearActiveSelection(this);

        gameObject.SetActive(false);
    }
}