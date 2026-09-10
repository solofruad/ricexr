using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Oculus.Interaction;

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
/// 1. Gestiona multiples manchas de enfermedad, cada una vinculada a un GameObject UIDocument
///    que el disenador posiciona manualmente en el editor.
/// 2. Rellena el contenido UXML de cada marcador con los datos del DiseaseSpot asociado.
///    IMPORTANTE: UI Toolkit no garantiza que rootVisualElement exista en Start(), por eso
///    FillMarkerUI lanza una corrutina que espera hasta que el panel este listo.
/// 3. Activa/desactiva los marcadores segun showMarkers.
/// 4. Delega el efecto de quemado a BurnLeafRenderer.
/// 5. Integra con Oculus Interaction para detectar grab/release y notificar al listener global.
/// </summary>
public class Leaf : MonoBehaviour
{
    [Header("Manchas de Enfermedad")]
    [Tooltip("Lista de manchas. Cada una referencia su propio marcador UIDocument en la escena.")]
    public List<DiseaseSpot> diseaseSpots = new List<DiseaseSpot>();

    [Header("Visibilidad")]
    public bool showMarkers = true;
    public bool ableToShowMarkers = true;

    [Header("Burn")]
    [Tooltip("Referencia al BurnLeafRenderer que vive en el GameObject del mesh de la hoja")]
    [SerializeField] private BurnLeafRenderer burnLeafRenderer;

    [Tooltip("Controlador visual opcional del viento de la hoja")]
    [SerializeField] private LeafWindController leafWindController;

    [SerializeField] private PointableUnityEventWrapper pointableWrapper;

    private bool _isBurning = false;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        if (leafWindController == null)
            leafWindController = GetComponentInParent<LeafWindController>();

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

    // Spots pendientes de poblar: se intentan cada frame hasta que el panel exista
    private readonly List<DiseaseSpot> _pendingSpots = new List<DiseaseSpot>();

    /// <summary>
    /// Activa los marcadores y los encola para poblar su UI en Update,
    /// ya que el panel de UI Toolkit puede no estar listo en Start().
    /// </summary>
    public void PopulateAndShowMarkers()
    {
        foreach (DiseaseSpot spot in diseaseSpots)
        {
            if (spot.markerObject == null)
            {
                Debug.LogWarning($"[Leaf] DiseaseSpot '{spot.diseaseName}' no tiene markerObject asignado.");
                continue;
            }

            spot.markerObject.SetActive(showMarkers && ableToShowMarkers);
            _pendingSpots.Add(spot);
        }

        leafWindController?.SetMarkersVisible(showMarkers && ableToShowMarkers);
    }

    /// <summary>
    /// Intenta poblar los spots pendientes cada frame.
    /// En cuanto un spot queda poblado se saca de la lista.
    /// Cuando la lista esta vacia, Update no hace nada.
    /// </summary>
    void Update()
    {
        if (_pendingSpots.Count == 0) return;

        for (int i = _pendingSpots.Count - 1; i >= 0; i--)
        {
            DiseaseSpot spot = _pendingSpots[i];

            if (spot.markerObject == null) { _pendingSpots.RemoveAt(i); continue; }

            UIDocument doc = spot.markerObject.GetComponentInChildren<UIDocument>(true);
            if (doc == null)
            {
                Debug.LogError($"[Leaf] '{spot.markerObject.name}' no tiene UIDocument en si mismo ni en sus hijos.");
                _pendingSpots.RemoveAt(i);
                continue;
            }

            VisualElement root = doc.rootVisualElement;
            if (root == null) continue;  // panel todavia no listo, reintentar el proximo frame

            Debug.Log($"[Leaf] Poblando '{spot.markerObject.name}' | doc={doc.name} | diseaseName={spot.diseaseName} | severity={spot.severity}");
            ApplySpotToRoot(spot, root);
            _pendingSpots.RemoveAt(i);
        }
    }

    /// <summary>
    /// Escribe los datos del spot en el arbol visual ya inicializado.
    /// </summary>
    private static void ApplySpotToRoot(DiseaseSpot spot, VisualElement root)
    {
        Label nameLabel = root.Q<Label>("disease-name");
        Debug.Log($"[Leaf] disease-name label encontrado: {nameLabel != null}");
        if (nameLabel != null)
            nameLabel.text = string.IsNullOrEmpty(spot.scientificName)
                ? spot.diseaseName
                : $"{spot.diseaseName}\n<i>{spot.scientificName}</i>";

        Label severityLabel = root.Q<Label>("severity-value");
        Debug.Log($"[Leaf] severity-value label encontrado: {severityLabel != null}");
        if (severityLabel != null)
            severityLabel.text = spot.severity.ToString();

        for (int i = 1; i <= 5; i++)
        {
            VisualElement segment = root.Q<VisualElement>($"seg-{i}");
            if (segment == null) { Debug.LogWarning($"[Leaf] seg-{i} no encontrado"); continue; }
            segment.EnableInClassList("active",   i <= spot.severity);
            segment.EnableInClassList("inactive", i >  spot.severity);
        }
    }

    /// <summary>
    /// Activa o desactiva todos los marcadores respetando ableToShowMarkers.
    /// </summary>
    public void SetMarkersVisibility(bool visible)
    {
        bool actualVisibility = visible && ableToShowMarkers;
        leafWindController?.SetMarkersVisible(actualVisibility);

        if (!ableToShowMarkers) return;

        foreach (DiseaseSpot spot in diseaseSpots)
        {
            if (spot.markerObject != null)
                spot.markerObject.SetActive(actualVisibility);
        }
    }

    /// <summary>
    /// Oculta los marcadores sin consultar ableToShowMarkers. Se usa al preparar
    /// una hoja tutorial, antes de que el acto de pista los habilite.
    /// </summary>
    public void HideMarkersImmediate()
    {
        leafWindController?.SetMarkersVisible(false);

        foreach (DiseaseSpot spot in diseaseSpots)
        {
            if (spot.markerObject != null)
                spot.markerObject.SetActive(false);
        }
    }

    /// <summary>
    /// Cambia si los callbacks de interaccion pueden mostrar marcadores.
    /// Al deshabilitarlos tambien los oculta para evitar que una hoja soltada
    /// deje una pista visible en el cultivo.
    /// </summary>
    public void SetMarkersAvailability(bool available)
    {
        ableToShowMarkers = available;
        if (!available)
            HideMarkersImmediate();
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
    /// Activa el efecto de quemado delegando la animacion del shader a BurnLeafRenderer.
    /// </summary>
    public void BurnAndDisable()
    {
        if (_isBurning) return;
        _isBurning = true;

        if (pointableWrapper != null)
            pointableWrapper.enabled = false;

        SetMarkersVisibility(false);

        if (burnLeafRenderer == null)
        {
            Debug.LogWarning($"[Leaf] No hay BurnLeafRenderer asignado en {gameObject.name}. Se desactiva sin animacion.");
            ReleaseAndDisable();
            return;
        }

        burnLeafRenderer.Burn(onComplete: ReleaseAndDisable);
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
