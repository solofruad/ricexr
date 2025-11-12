using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Oculus.Interaction;

[System.Serializable]
public class DiseaseSpot
{
    public Vector3 localPosition;
    public float size = 0.05f;
    [Tooltip("Nombre de la enfermedad (ej: Pyricularia, Rhynchosporium)")]
    public string diseaseName = "Pyricularia oryzae";
    [Range(1, 10)]
    [Tooltip("Severidad de la infección (1-10)")]
    public int severity = 5;
    public GameObject customPrefab; // Prefab específico para esta mancha (opcional)

}

public class Leaf : MonoBehaviour
{
    [Header("Manchas de Enfermedad")]
    [Tooltip("Lista de posiciones locales donde están las manchas")]
    public List<DiseaseSpot> diseaseSpots = new List<DiseaseSpot>();

    [Header("Configuración Visual")]
    [Tooltip("Prefab por defecto para los marcadores (si no se especifica uno por mancha)")]
    public GameObject markerPrefab;


    public bool showMarkers = true;

    [Header("Configuración de Animación")]
    public bool animateMarkers = true;
    public float pulseSpeed = 2f;
    public float pulseScale = 1.2f;
    public bool ableToShowMarkers = true;

    private List<GameObject> markerObjects = new List<GameObject>();

    [SerializeField] private PointableUnityEventWrapper pointableWrapper;

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
        //Debug.Log($"-----------------------------------------------------------");

        GrabbableObjectListener.Instance.ActualLeafGrabbed = this;
    }

    private void OnUnselect(PointerEvent pointerEvent)
    {
        //Debug.Log($"Objeto deseleccionado: {pointerEvent.Identifier}");
        GrabbableObjectListener.Instance.ActualLeafGrabbed = null;
    }

   
    void Update()
    {
        if (animateMarkers && markerObjects.Count > 0)
        {
            AnimateMarkers();
        }
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
        // Determinar qué prefab usar
        GameObject prefabToUse = spot.customPrefab != null ? spot.customPrefab : markerPrefab;

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

    

    void AnimateMarkers()
    {
        float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f) * 0.5f;

        for (int i = 0; i < markerObjects.Count; i++)
        {
            if (markerObjects[i] != null && i < diseaseSpots.Count)
            {
                DiseaseSpot spot = diseaseSpots[i];
                markerObjects[i].transform.localScale = Vector3.one * spot.size * scale;
            }
        }
    }

    public void ClearMarkers()
    {
        foreach (GameObject marker in markerObjects)
        {
            if (marker != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(marker);
                }
                else
                {
                    DestroyImmediate(marker);
                }
            }
        }
        markerObjects.Clear();
    }

    public void AddDiseaseSpot(Vector3 localPosition, float size = 0.05f, GameObject customPrefab = null)
    {
        DiseaseSpot newSpot = new DiseaseSpot
        {
            localPosition = localPosition,
            size = size,
            customPrefab = customPrefab
        };
        diseaseSpots.Add(newSpot);

        if (Application.isPlaying)
        {
            GenerateMarkers();
        }
    }

    public void RemoveDiseaseSpot(int index)
    {
        if (index >= 0 && index < diseaseSpots.Count)
        {
            diseaseSpots.RemoveAt(index);

            if (Application.isPlaying)
            {
                GenerateMarkers();
            }
        }
    }

    public void ToggleMarkers()
    {
        showMarkers = !showMarkers;
        GenerateMarkers();
    }

    /*
    public string GetDiseaseInfo()
    {
        return $"Enfermedad: {diseaseName}\n" +
               $"Severidad: {severity}/10\n" +
               $"Manchas detectadas: {diseaseSpots.Count}";
    }
    */
    public void SetMarkerPrefab(GameObject newPrefab)
    {
        markerPrefab = newPrefab;
        GenerateMarkers();
    }

    void OnValidate()
    {
        // Regenerar marcadores cuando se cambian valores en el inspector
        if (Application.isPlaying && markerObjects.Count > 0)
        {
            GenerateMarkers();
        }
    }


    // Método de ayuda para añadir manchas en posiciones del mundo
    public void AddDiseaseSpotWorldPosition(Vector3 worldPosition, float size = 0.05f, GameObject customPrefab = null)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        AddDiseaseSpot(localPos, size, customPrefab);
    }
    void OnDestroy()
    {
        ClearMarkers();
        if (pointableWrapper != null)
        {
            pointableWrapper.WhenSelect.RemoveListener(OnSelect);
            pointableWrapper.WhenUnselect.RemoveListener(OnUnselect);
        }
    }

}
/*
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(Leaf))]
public class PlantDiseaseDetectorEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Leaf detector = (Leaf)target;

        GUILayout.Space(10);

        GUILayout.Space(10);
        GUILayout.Label(detector.GetDiseaseInfo(), UnityEditor.EditorStyles.helpBox);
    }
}
#endif*/