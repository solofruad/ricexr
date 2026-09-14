using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Oculus.Interaction;

[System.Serializable]
public class DiseaseSpot
{
    [Tooltip("Modelo compartido de la enfermedad.")]
    public DiseaseDefinition disease;

    [Tooltip("Valor real del ejemplar (1–9); -1 acepta cualquier severidad disponible.")]
    public int severity = 3;

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

    public bool IsHealthy => diseaseSpots != null && diseaseSpots.Count == 0;
    public bool IsDiagnosable(DiseaseCatalog catalog) => diseaseSpots != null && diseaseSpots.Exists(
        spot => spot != null && catalog != null && catalog.Contains(spot.disease)
            && (spot.severity == -1 || spot.disease.AllowsSeverity(spot.severity)));

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

    /// <summary>
    /// True desde que la hoja fue diagnosticada correctamente y empezo a quemarse.
    /// </summary>
    public bool IsBurning => _isBurning;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        if (diseaseSpots == null)
            diseaseSpots = new List<DiseaseSpot>();

        if (IsHealthy)
        {
            showMarkers = false;
            _hideAllMarkerDocuments = true;
        }
    }

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
    private bool _hideAllMarkerDocuments;

    /// <summary>
    /// Activa los marcadores y los encola para poblar su UI en Update,
    /// ya que el panel de UI Toolkit puede no estar listo en Start().
    /// </summary>
    public void PopulateAndShowMarkers()
    {
        _pendingSpots.Clear();
        for (int i = 0; i < diseaseSpots.Count; i++)
        {
            DiseaseSpot spot = diseaseSpots[i];
            if (spot == null)
            {
                Debug.LogError($"[Leaf] '{gameObject.name}' tiene un DiseaseSpot nulo en la posición {i}.");
                continue;
            }
            if (spot.disease == null)
            {
                Debug.LogError($"[Leaf] '{gameObject.name}' tiene un DiseaseSpot sin enfermedad en la posición {i}.");
                SetMarkerVisible(spot, false);
                continue;
            }
            if (spot.markerObject == null)
            {
                Debug.LogWarning($"[Leaf] '{spot.disease.DisplayName}' no tiene marcador asignado.");
                continue;
            }

            QueuePendingSpot(spot);
            SetMarkerVisible(spot, showMarkers && ableToShowMarkers);
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
        if (_hideAllMarkerDocuments)
        {
            _hideAllMarkerDocuments = !HideAllMarkerDocuments();
        }

        if (IsHealthy) return;
        if (_pendingSpots.Count == 0) return;

        for (int i = _pendingSpots.Count - 1; i >= 0; i--)
        {
            DiseaseSpot spot = _pendingSpots[i];

            if (spot == null || spot.disease == null)
            {
                _pendingSpots.RemoveAt(i);
                continue;
            }

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

            ApplySpotToRoot(spot, root);
            SetMarkerRootVisible(root, showMarkers && ableToShowMarkers);
            _pendingSpots.RemoveAt(i);
        }
    }

    /// <summary>
    /// Escribe los datos del spot en el arbol visual ya inicializado.
    /// </summary>
    private static void ApplySpotToRoot(DiseaseSpot spot, VisualElement root)
    {
        if (spot?.disease == null) return;

        Label nameLabel = root.Q<Label>("disease-name");
        if (nameLabel != null)
        {
            string scientificName = spot.disease.data != null ? spot.disease.data.scientificName : string.Empty;
            nameLabel.text = $"{spot.disease.DisplayName}\n<i>{scientificName}</i>";
        }

        Label severityLabel = root.Q<Label>("severity-value");
        if (severityLabel != null)
            severityLabel.text = spot.severity == -1 ? "—" : spot.severity.ToString();

        VisualElement bar = root.Q<VisualElement>("severity-bar");
        if (bar == null) return;
        bar.Clear();
        foreach (var severity in spot.disease.AvailableSeverities)
        {
            var segment = new Label(severity.value.ToString());
            segment.AddToClassList("severity-tick");
            segment.EnableInClassList("active", severity.value == spot.severity);
            bar.Add(segment);
        }
    }

    private void SetMarkerVisible(DiseaseSpot spot, bool visible)
    {
        if (spot?.markerObject == null) return;

        UIDocument doc = spot.markerObject.GetComponentInChildren<UIDocument>(true);
        if (doc == null) return;

        VisualElement root = doc.rootVisualElement;
        if (root == null)
        {
            QueuePendingSpot(spot);
            return;
        }

        SetMarkerRootVisible(root, visible);
    }

    private void QueuePendingSpot(DiseaseSpot spot)
    {
        if (spot != null && !_pendingSpots.Contains(spot))
            _pendingSpots.Add(spot);
    }

    private static void SetMarkerRootVisible(VisualElement root, bool visible)
    {
        root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool HideAllMarkerDocuments()
    {
        bool allReady = true;
        foreach (UIDocument doc in GetComponentsInChildren<UIDocument>(true))
        {
            if (doc == null) continue;

            VisualElement root = doc.rootVisualElement;
            if (root == null)
            {
                allReady = false;
                continue;
            }

            SetMarkerRootVisible(root, false);
        }

        return allReady;
    }

    /// <summary>
    /// Activa o desactiva todos los marcadores respetando ableToShowMarkers.
    /// </summary>
    public void SetMarkersVisibility(bool visible)
    {
        bool actualVisibility = visible && ableToShowMarkers;
        showMarkers = visible;
        leafWindController?.SetMarkersVisible(actualVisibility);

        if (!ableToShowMarkers) return;

        foreach (DiseaseSpot spot in diseaseSpots)
        {
            SetMarkerVisible(spot, actualVisibility);
        }
    }

    /// <summary>
    /// Oculta los marcadores sin consultar ableToShowMarkers. Se usa al preparar
    /// una hoja tutorial, antes de que el acto de pista los habilite.
    /// </summary>
    public void HideMarkersImmediate()
    {
        showMarkers = false;
        _hideAllMarkerDocuments = true;
        leafWindController?.SetMarkersVisible(false);

        foreach (DiseaseSpot spot in diseaseSpots)
        {
            SetMarkerVisible(spot, false);
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
