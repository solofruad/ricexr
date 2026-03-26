using UnityEngine;

/// <summary>
/// Datos de una enfermedad de planta.
/// Se usa para poblar el PlantDiseasePanelController con la información
/// de cada nivel. Puedes llenarlo desde código o desde un PlantDiseaseDataAsset.
/// </summary>
[System.Serializable]
public class PlantDiseaseData
{
    public string      title;
    public string      scientificName;
    public string      category;
    public string      description;
    public Texture2D[] images;
}

/// <summary>
/// ScriptableObject que envuelve PlantDiseaseData para poder configurarlo
/// desde el Inspector sin tocar código.
///
/// Cómo crear uno:
///   Clic derecho en Assets → Create → Plant Disease → Disease Data Asset
///
/// Luego arrástralo al array "Disease Data Per Level" del UIGameListener,
/// en el mismo orden que los levelConfigs del SceneInteractionManager.
/// </summary>
[CreateAssetMenu(fileName = "NewDiseaseData", menuName = "Plant Disease/Disease Data Asset")]
public class PlantDiseaseDataAsset : ScriptableObject
{
    public PlantDiseaseData data;
}
