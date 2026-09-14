using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public static class DiseaseSeverityReferences
{
    private static readonly int[] ScaleValues = { 1, 3, 5, 7, 9 };

    public static void Apply(VisualElement root, IEnumerable<DiseaseSeverity> severities)
    {
        var container = root.Q<VisualElement>("severity-references");
        if (container == null) return;
        container.Clear();
        var available = severities?
            .Where(s => s.referenceImage != null && ScaleValues.Contains(s.value))
            .GroupBy(s => s.value)
            .ToDictionary(group => group.Key, group => group.First());
        container.style.display = available != null && available.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (available == null) return;
        for (int i = 0; i < ScaleValues.Length; i++)
            if (available.TryGetValue(ScaleValues[i], out DiseaseSeverity severity)) Add(container, severity, i + 1);
    }

    private static void Add(VisualElement parent, DiseaseSeverity severity, int step)
    {
        var column = new VisualElement();
        column.AddToClassList("quest-severity-reference");
        var image = new Image { image = severity.referenceImage, scaleMode = ScaleMode.ScaleToFit };
        image.AddToClassList("quest-severity-reference-image");
        column.Add(image);
        var color = new VisualElement();
        color.AddToClassList("quest-severity-reference-color");
        color.AddToClassList($"quest-severity-reference-color--{step}");
        column.Add(color);
        var label = new Label(severity.value.ToString());
        label.AddToClassList("quest-severity-reference-label");
        column.Add(label);
        parent.Add(column);
    }
}
