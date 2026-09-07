using UnityEngine;

/// <summary>
/// Datos de una enfermedad de planta.
/// Se usa para poblar el PlantDiseasePanelController con la información
/// de cada nivel. Puedes llenarlo desde código o desde un PlantDiseaseDataAsset.
/// </summary>
[System.Serializable]
public class PanelDiseaseData
{
    public string      title;
    public string      scientificName;
    public string      category;
    public string      description;
    public Texture2D   severityEvolutionImage;
    public Texture2D[] images;

    [Tooltip("Textos de los 3 paneles que se muestran antes del nivel: portada, la " +
             "enfermedad y el daño. Si se deja vacio, el nivel muestra directamente " +
             "la ficha de consulta.")]
    public DiseaseIntroContent intro;
}

/// <summary>
/// ScriptableObject que envuelve PanelDiseaseData para poder configurarlo
/// desde el Inspector sin tocar código.
///
/// Cómo crear uno:
///   Clic derecho en Assets → Create → Plant Disease → Disease Data Asset
///
/// Luego arrástralo al array "Disease Data Per Level" del UIGameListener,
/// en el mismo orden que los levelConfigs del SceneInteractionManager.
/// </summary>

[CreateAssetMenu(fileName = "PanelDiseaseData", menuName = "Scriptable Objects/PanelDiseaseData")]
public class PanelDiseaseDataObject : ScriptableObject
{
    public PanelDiseaseData data;
}
