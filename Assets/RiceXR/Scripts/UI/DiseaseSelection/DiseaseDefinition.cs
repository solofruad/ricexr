using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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

[CreateAssetMenu(fileName = "Enfermedad", menuName = "Rice XR/Enfermedad")]
public class DiseaseDefinition : ScriptableObject
{
    [Tooltip("Identificador estable. No cambia al editar los nombres visibles.")]
    public string id;
    public PanelDiseaseData data;
    [Tooltip("Valores elegidos manualmente. 0 corresponde a hojas sanas y no es una respuesta.")]
    public List<DiseaseSeverity> severities = new List<DiseaseSeverity>();

    public string DisplayName => data != null ? data.title : name;
    public IEnumerable<DiseaseSeverity> AvailableSeverities => (severities ?? Enumerable.Empty<DiseaseSeverity>())
        .Where(s => s != null && s.value >= 1 && s.value <= 9)
        .GroupBy(s => s.value).Select(g => g.First()).OrderBy(s => s.value);
    public bool AllowsSeverity(int value) => value > 0 && value <= 9
        && severities != null && severities.Exists(s => s != null && s.value == value);
    public string SeveritySummary => string.Join(", ", AvailableSeverities.Select(s => s.value));

    public bool IsValid(out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(id)) error = "Falta el identificador de la enfermedad.";
        else if (data == null || string.IsNullOrWhiteSpace(data.title)) error = "Falta el nombre visible.";
        else if (severities == null || severities.Count == 0) error = "Define al menos una severidad.";
        else if (severities.Exists(s => s == null || s.value < 1 || s.value > 9))
            error = "Las severidades diagnosticables deben estar entre 1 y 9.";
        else if (severities.Select(s => s.value).Distinct().Count() != severities.Count)
            error = "Hay severidades duplicadas.";
        return error == null;
    }

    private void OnValidate()
    {
        if (!IsValid(out string error)) Debug.LogWarning($"[Enfermedad] {name}: {error}", this);
    }
}

[System.Serializable]
public class DiseaseSeverity
{
    [Range(1, 9)] public int value = 1;
    public string label;
    public Texture2D referenceImage;
    public string DisplayLabel => string.IsNullOrWhiteSpace(label) ? value.ToString() : $"{value} · {label}";
}
