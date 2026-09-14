using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CatalogoEnfermedades", menuName = "Rice XR/Catálogo de enfermedades")]
public class DiseaseCatalog : ScriptableObject
{
    public List<DiseaseDefinition> diseases = new List<DiseaseDefinition>();

    public bool Contains(DiseaseDefinition disease) => disease != null && diseases != null && diseases.Contains(disease);

    public bool IsValid(out string error)
    {
        error = null;
        var ids = new HashSet<string>();
        if (diseases == null || diseases.Count == 0) error = "El catálogo está vacío.";
        else foreach (var disease in diseases)
        {
            if (disease == null) { error = "Hay una enfermedad sin asignar."; break; }
            if (!disease.IsValid(out error)) break;
            if (!ids.Add(disease.id)) { error = $"Identificador duplicado: {disease.id}"; break; }
        }
        return error == null;
    }
}
