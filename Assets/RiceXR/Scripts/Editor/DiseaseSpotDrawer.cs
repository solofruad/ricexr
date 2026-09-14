using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DiseaseSpot))]
public class DiseaseSpotDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        var diseaseProperty = property.FindPropertyRelative("disease");
        EditorGUI.PropertyField(position, diseaseProperty, new GUIContent("Enfermedad"));
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        var severity = property.FindPropertyRelative("severity");
        var disease = diseaseProperty.objectReferenceValue as DiseaseDefinition;
        if (disease != null)
        {
            var values = new[] { -1 }.Concat(disease.AvailableSeverities.Select(s => s.value)).ToList();
            var labels = new[] { "Cualquier severidad" }.Concat(disease.AvailableSeverities.Select(s => s.DisplayLabel)).ToList();
            if (!values.Contains(severity.intValue))
            {
                values.Add(severity.intValue);
                labels.Add($"{severity.intValue} (no disponible)");
            }
            int selected = EditorGUI.Popup(position, "Severidad", values.IndexOf(severity.intValue), labels.ToArray());
            severity.intValue = values[selected];
        }
        else EditorGUI.PropertyField(position, severity, new GUIContent("Severidad"));
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("markerObject"), new GUIContent("Marcador"));
        EditorGUI.EndProperty();
    }
}
