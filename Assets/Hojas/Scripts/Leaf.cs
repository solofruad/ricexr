using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Oculus.Interaction;
using DG.Tweening;

[System.Serializable]
public class DiseaseSpot
{
    public Vector3 localPosition;
    public float size = 0.05f;
    [Tooltip("Nombre de la enfermedad (ej: Pyricularia, Rhynchosporium)")]
    public string diseaseName = "Pyricularia oryzae";
    [Range(1, 10)]
    [Tooltip("Severidad de la infeccion (1-10)")]
    public int severity = 5;
    [Tooltip("Nombre cientifico mostrado en el panel de enfermedad")]
    public string scientificName;

}

/// <summary>
/// Componente que representa una hoja con capacidad de mostrar manchas de enfermedad.
/// 
/// Funcionalidades principales:
/// 1. Gestiona miltiples manchas de enfermedad con posicion, tamaño y severidad individuales
/// 2. Genera marcadores visuales para cada mancha (etiquetas con informacion)
/// 3. Implementa animaciones de pulso para destacar los marcadores
/// 4. Integra con Oculus Interaction para detectar cuando la hoja es agarrada/soltada
/// 5. Notifica al GrabbableObjectListener cuando es interactuada
/// 
/// Uso tipico:
/// - Configurar diseaseSpots en el inspector para definir las manchas
/// - Los marcadores se generan automaticamente al inicio
/// - Cuando el usuario agarra la hoja, se notifica al sistema global
/// - Los sistemas de diagnastico pueden acceder a la informaci�n de las manchas
/// 
/// Notas importantes:
/// - Los marcadores usan TextMeshPro para mostrar informacion de enfermedad
/// - Las posiciones de las manchas son locales al transform de la hoja
/// - La animacion de pulso se puede activar/desactivar segun necesidad
/// - Integra con el sistema de interaccion de Meta para VR
/// </summary>
public class Leaf : MonoBehaviour
{
    [Header("Manchas de Enfermedad")]
    [Tooltip("Lista de posiciones locales donde estan las manchas")]
    public List<DiseaseSpot> diseaseSpots = new List<DiseaseSpot>();

    [Header("Configuracion Visual")]
    [Tooltip("Prefab por defecto para los marcadores (si no se especifica uno por mancha)")]
    public GameObject markerPrefab;


    public bool showMarkers = true;

    [Header("Configuracion de Animacion")]

    public bool ableToShowMarkers = true;

    [Header("Efecto de Desaparicion (Burn/Dissolve)")]
    [Tooltip("Duracion del efecto en segundos")]
    public float burnDuration = 1.5f;
    [Tooltip("Tiempo de espera antes de empezar a quemarse")]
    public float delayBeforeBurn = 1.0f;

    private List<GameObject> markerObjects = new List<GameObject>();

    [SerializeField] private PointableUnityEventWrapper pointableWrapper;

    private bool _isBurning = false;
    private static readonly int BurnProgressId = Shader.PropertyToID("_BurnProgress");

    void Start()
    {
        GenerateMarkers();
    
        if (pointableWrapper != null)
        {
            // Suscribirse a los eventos
            pointableWrapper.WhenSelect.AddListener(OnSelect);
            pointableWrapper.WhenUnselect.AddListener(OnUnselect);
        }
    }

    private void OnSelect(PointerEvent pointerEvent)
    {
        if (GrabbableObjectListener.Instance == null)
            return;

        object data = pointerEvent.Data;
        GrabbableObjectListener.SelectionHand hand = ResolveSelectionHand(data);
        Transform anchor = ResolveSelectionAnchor(data);

        GrabbableObjectListener.Instance.SetActiveSelection(this, hand, anchor);
    }

    private void OnUnselect(PointerEvent pointerEvent)
    {
        if (GrabbableObjectListener.Instance == null)
            return;

        GrabbableObjectListener.Instance.ClearActiveSelection(this);
    }

    private static GrabbableObjectListener.SelectionHand ResolveSelectionHand(object data)
    {
        if (data == null)
            return GrabbableObjectListener.SelectionHand.Unknown;

        var handednessProp = data.GetType().GetProperty("Handedness");
        if (handednessProp == null)
            return GrabbableObjectListener.SelectionHand.Unknown;

        object handedness = handednessProp.GetValue(data);
        if (handedness == null)
            return GrabbableObjectListener.SelectionHand.Unknown;

        string handednessText = handedness.ToString();
        if (string.Equals(handednessText, "Left", System.StringComparison.OrdinalIgnoreCase))
            return GrabbableObjectListener.SelectionHand.Left;
        if (string.Equals(handednessText, "Right", System.StringComparison.OrdinalIgnoreCase))
            return GrabbableObjectListener.SelectionHand.Right;

        return GrabbableObjectListener.SelectionHand.Unknown;
    }

    private static Transform ResolveSelectionAnchor(object data)
    {
        if (data == null)
            return null;

        if (data is Component component)
            return component.transform;

        var transformProp = data.GetType().GetProperty("Transform");
        if (transformProp != null)
        {
            object transformValue = transformProp.GetValue(data);
            if (transformValue is Transform t)
                return t;
        }

        return null;
    }


    public void GenerateMarkers()
    {
        // Si ya existen marcadores, solo cambiar su visibilidad
        if (markerObjects.Count > 0)
        {
            SetMarkersVisibility(showMarkers);
            return;
        }

        // Crear marcadores por primera vez
        for (int i = 0; i < diseaseSpots.Count; i++)
        {
            DiseaseSpot spot = diseaseSpots[i];
            GameObject marker = CreateMarker(spot, i);

            if (marker != null)
            {
                TextMeshProUGUI[] texts = marker.GetComponentsInChildren<TextMeshProUGUI>(true);
                texts[0].text = spot.diseaseName;
                texts[1].text = $"Severidad: {spot.severity}/5";
                markerObjects.Add(marker);
                marker.SetActive(showMarkers);
            }
        }
    }

    public void SetMarkersVisibility(bool visible)
    {
        if (!ableToShowMarkers)
        {
            return;
        }

        foreach (GameObject marker in markerObjects)
        {
            if (marker != null)
            {
                marker.SetActive(visible);
            }
        }
    }

    GameObject CreateMarker(DiseaseSpot spot, int index)
    {
        // Determinar qu� prefab usar
        GameObject prefabToUse = markerPrefab;

        if (prefabToUse == null)
        {
            Debug.LogWarning($"No hay prefab asignado para el marcador {index} en {gameObject.name}");
            return null;
        }

        // Instanciar el prefab
        GameObject marker = Instantiate(prefabToUse);
        marker.name = $"Marker_{spot.diseaseName}_{index}";
        marker.transform.parent = transform;
        marker.transform.localPosition = spot.localPosition;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one * spot.size;


        return marker;
    }


    public void ToggleMarkers()
    {
        showMarkers = !showMarkers;
        GenerateMarkers();
    }


    /// <summary>
    /// Activa el efecto de quemado/desaparicion animando la propiedad del shader
    /// y finalmente desactiva la hoja.
    /// </summary>
    public void BurnAndDisable()
    {
        if (_isBurning) return;
        _isBurning = true;

        if (pointableWrapper != null)
        {
            pointableWrapper.enabled = false;
        }

        SetMarkersVisibility(false);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            ReleaseAndDisable();
            return;
        }

        foreach (var renderer in renderers)
        {
            renderer.material.SetFloat(BurnProgressId, 0f);
        }

        DG.Tweening.DOVirtual.DelayedCall(delayBeforeBurn, () =>
        {
            float val = 0f;
            DG.Tweening.DOTween.To(() => val, x =>
            {
                val = x;
                foreach (var renderer in renderers)
                {
                    renderer.material.SetFloat(BurnProgressId, x);
                }
            }, 1f, burnDuration)
            .OnComplete(() =>
            {
                ReleaseAndDisable();
            });
        });
    }

    private void ReleaseAndDisable()
    {
        var grabbable = GetComponent<Grabbable>();
        if (grabbable != null)
        {
            grabbable.enabled = false;
        }

        if (GrabbableObjectListener.Instance != null)
        {
            GrabbableObjectListener.Instance.ClearActiveSelection(this);
        }

        gameObject.SetActive(false);
    }

}
