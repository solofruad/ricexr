using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RiceXR.Core;
using Object = UnityEngine.Object;

public static class DiseaseModelMigration
{
    private const string Root = "Assets/RiceXR/";
    private const string CatalogPath = Root + "ScriptableObjects/Diseases/CatalogoEnfermedades.asset";
    private const string SelectorPath = Root + "Prefabs/UI/DiseaseSeveritySelectorUI.prefab";
    private const string OptionPath = Root + "Prefabs/UI/DiagnosisOption.prefab";

    [MenuItem("Tools/RiceXR/Enfermedades/Migrar modelo y selector")]
    public static void Migrate()
    {
        var pyri = LoadDisease("Pyricularia");
        var ryncho = LoadDisease("Rynchosporium");
        InitializeDisease(pyri, "pyricularia-oryzae", Enumerable.Range(1, 9).ToArray());
        InitializeDisease(ryncho, "rhynchosporium-oryzae", new[] { 1, 3, 5, 7, 9 });
        var catalog = AssetDatabase.LoadAssetAtPath<DiseaseCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<DiseaseCatalog>();
            catalog.diseases.AddRange(new[] { pyri, ryncho });
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        if (!catalog.IsValid(out string error)) throw new InvalidOperationException(error);
        var pyriPaths = V2Paths("Pyricularia");
        var rynchoPaths = V2Paths("Rynchosporium");
        foreach (string path in pyriPaths.Concat(rynchoPaths))
        {
            var model = path.Contains("/Pyricularia/") ? pyri : ryncho;
            int severity = SeverityOf(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            if (severity > 0 && !model.AllowsSeverity(severity))
                throw new InvalidOperationException($"{path}: severidad {severity} no habilitada en {model.name}.");
        }
        MigrateV2(pyriPaths, pyri);
        MigrateV2(rynchoPaths, ryncho);
        ConfigureLevel("NEWPyriculariaTutorial", pyriPaths, catalog);
        ConfigureLevel("NEWRynchoTutorial", rynchoPaths, catalog);
        ConfigureLevel("Mixto", pyriPaths.Concat(rynchoPaths).ToArray(), catalog);
        ConfigureLevel("TutorialGuiado", new[] { Root + "Prefabs/Leaves/Pyricularia/v2/3Pyri.prefab" }, catalog);
        ConfigureBaseSpawner(catalog);
        BuildSelector(catalog);
        UpdateSceneSelector();
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("[Enfermedades] Migración completada: modelos, hojas v2, niveles y selector.");
    }

    private static DiseaseDefinition LoadDisease(string name) =>
        AssetDatabase.LoadAssetAtPath<DiseaseDefinition>(Root + $"ScriptableObjects/Diseases/{name}.asset")
        ?? throw new InvalidOperationException($"Falta el modelo {name}.");

    private static void UpdateSceneSelector()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Main.unity");
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Additive);
        try
        {
            foreach (var selector in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DiseaseSelectionSystem>(true)))
            {
                var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(selector.gameObject);
                var modifications = PrefabUtility.GetPropertyModifications(instance);
                if (modifications == null || !modifications.Any(m => m.propertyPath == "diseaseSelectionPanel")) continue;
                var sourceTransform = PrefabUtility.GetCorrespondingObjectFromSource(instance.transform);
                var sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                PrefabUtility.SetPropertyModifications(instance, modifications.Where(m =>
                    (m.target == sourceTransform && !m.propertyPath.StartsWith("m_LocalScale"))
                    || m.target == sourceObject).ToArray());
                EditorSceneManager.MarkSceneDirty(scene);
            }
            if (!wasLoaded && scene.isDirty) EditorSceneManager.SaveScene(scene);
        }
        finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    private static string[] V2Paths(string disease) => Directory.GetFiles(
        Root + $"Prefabs/Leaves/{disease}/v2", "*.prefab").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();

    [MenuItem("Tools/RiceXR/Enfermedades/Reparar diagnósticos de hojas v2")]
    public static void RepairV2Leaves()
    {
        var pyri = LoadDisease("Pyricularia");
        var ryncho = LoadDisease("Rynchosporium");
        MigrateV2(V2Paths("Pyricularia"), pyri);
        MigrateV2(V2Paths("Rynchosporium"), ryncho);
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("[Enfermedades] Diagnósticos de hojas v2 reparados.");
    }

    private static void InitializeDisease(DiseaseDefinition disease, string id, int[] values)
    {
        if (!string.IsNullOrWhiteSpace(disease.id)) return;
        disease.id = id;
        disease.severities = values.Select(v => new DiseaseSeverity { value = v }).ToList();
        disease.data.intro.severityBody = "Compara las lesiones de la hoja con las referencias. El 0 representa una hoja sana; diagnostica las hojas enfermas.";
        disease.data.intro.severityRows = Array.Empty<DiseaseIntroRow>();
        disease.data.severityEvolutionImage = null;
        EditorUtility.SetDirty(disease);
    }

    public static int SeverityOf(GameObject prefab)
    {
        var leaf = prefab.GetComponentInChildren<Leaf>(true);
        if (leaf == null) throw new InvalidOperationException($"{prefab.name}: falta Leaf.");
        var names = leaf.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.sharedMesh != null && !IsMarker(f.transform, leaf.transform))
            .Select(f => f.sharedMesh.name).Distinct().ToArray();
        var values = new List<int>();
        foreach (string name in names)
        {
            if (name.EndsWith(".base", StringComparison.OrdinalIgnoreCase)) continue;
            if (!LeafSeverity.TryParseMeshName(name, out int severity))
                throw new InvalidOperationException($"{prefab.name}: nombre de mesh inválido '{name}'.");
            values.Add(severity);
        }
        if (values.Count != 1) throw new InvalidOperationException($"{prefab.name}: se esperaba un mesh de hoja, hay {values.Count}.");
        return values[0];
    }

    private static bool IsMarker(Transform node, Transform leaf)
    {
        for (var t = node; t != null && t != leaf; t = t.parent)
            if (t.name.Contains("Marker")) return true;
        return false;
    }

    public static void AssignDiagnosis(Leaf leaf, DiseaseDefinition disease, int severity)
    {
        if (leaf == null) throw new InvalidOperationException("No se puede asignar un diagnóstico sin Leaf.");
        if (disease == null) throw new InvalidOperationException("No se puede asignar un diagnóstico sin enfermedad.");

        if (leaf.diseaseSpots == null)
            leaf.diseaseSpots = new List<DiseaseSpot>();

        if (severity == 0)
        {
            leaf.diseaseSpots.Clear();
            leaf.showMarkers = false;
            leaf.ableToShowMarkers = false;
            FinalizeDiagnosisAssignment(leaf, severity);
            return;
        }
        if (!disease.AllowsSeverity(severity)) throw new InvalidOperationException($"{disease.name}: severidad {severity} deshabilitada.");

        var marker = leaf.GetComponentInChildren<UnityEngine.UIElements.UIDocument>(true);
        GameObject markerObject = ResolveMarkerObject(marker, leaf.transform);

        leaf.diseaseSpots.Clear();
        leaf.diseaseSpots.Add(new DiseaseSpot
        {
            disease = disease,
            severity = severity,
            markerObject = markerObject
        });
        leaf.showMarkers = false;
        leaf.ableToShowMarkers = true;
        FinalizeDiagnosisAssignment(leaf, severity);
    }

    private static void FinalizeDiagnosisAssignment(Leaf leaf, int severity)
    {
        CleanDiseaseSpotOverrides(leaf, severity);
        PrefabUtility.RecordPrefabInstancePropertyModifications(leaf);
    }

    private static GameObject ResolveMarkerObject(UnityEngine.UIElements.UIDocument document, Transform leaf)
    {
        if (document == null) return null;

        Transform marker = document.transform;
        while (marker != null && marker != leaf)
        {
            if (marker.name == "Marker")
                return marker.gameObject;
            marker = marker.parent;
        }

        return document.gameObject;
    }

    private static void CleanDiseaseSpotOverrides(Leaf leaf, int severity)
    {
        var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(leaf.gameObject);
        var sourceLeaf = PrefabUtility.GetCorrespondingObjectFromSource(leaf);
        if (instanceRoot == null || sourceLeaf == null) return;

        var modifications = PrefabUtility.GetPropertyModifications(instanceRoot);
        if (modifications == null) return;

        var cleaned = modifications.Where(modification =>
        {
            if (modification.target != sourceLeaf ||
                !modification.propertyPath.StartsWith("diseaseSpots", StringComparison.Ordinal))
                return true;

            if (severity == 0) return false;

            return modification.propertyPath == "diseaseSpots.Array.size"
                || modification.propertyPath == "diseaseSpots.Array.data[0].disease"
                || modification.propertyPath == "diseaseSpots.Array.data[0].severity"
                || modification.propertyPath == "diseaseSpots.Array.data[0].markerObject";
        }).ToArray();

        PrefabUtility.SetPropertyModifications(instanceRoot, cleaned);
    }

    private static void MigrateV2(string[] paths, DiseaseDefinition disease)
    {
        foreach (string path in paths)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int severity = SeverityOf(contents);
                var leaf = contents.GetComponentInChildren<Leaf>(true);
                AssignDiagnosis(leaf, disease, severity);
                var reference = disease.severities.FirstOrDefault(s => s.value == severity);
                if (reference != null && reference.referenceImage == null)
                {
                    reference.referenceImage = leaf.GetComponentInChildren<BurnLeafRenderer>(true)?.albedo;
                    EditorUtility.SetDirty(disease);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }

    private static void ConfigureLevel(string name, string[] paths, DiseaseCatalog catalog)
    {
        string path = Root + $"Prefabs/Levels/{name}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so = new SerializedObject(contents.GetComponent<LeavesSpawner>());
            var prefabs = so.FindProperty("grassPrefabs");
            prefabs.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++) prefabs.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            so.FindProperty("diseaseCatalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void ConfigureBaseSpawner(DiseaseCatalog catalog)
    {
        string path = Root + "Prefabs/Levels/SpawnLeavesBase.prefab";
        if (!File.Exists(path)) return;
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so = new SerializedObject(contents.GetComponent<LeavesSpawner>());
            so.FindProperty("diseaseCatalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void BuildSelector(DiseaseCatalog catalog)
    {
        var contents = PrefabUtility.LoadPrefabContents(SelectorPath);
        try
        {
            var controller = contents.GetComponent<DiseaseSelectionSystem>();
            var placement = contents.GetComponent<BodyLeashedPanel>();
            if (placement == null) placement = contents.AddComponent<BodyLeashedPanel>();
            controller.panelPlacement = placement;
            if (controller.view != null) { controller.catalog = catalog; PrefabUtility.SaveAsPrefabAsset(contents, SelectorPath); return; }
            var font = contents.GetComponentInChildren<TMP_Text>(true)?.font ?? TMP_Settings.defaultFontAsset;
            foreach (var billboard in contents.GetComponentsInChildren<BillboardUI>(true)) Object.DestroyImmediate(billboard);
            var panel = controller.diseaseSelectionPanel.transform;
            foreach (var component in panel.GetComponents<Component>())
                if (!(component is Transform)) Object.DestroyImmediate(component);
            while (panel.childCount > 0) Object.DestroyImmediate(panel.GetChild(0).gameObject);
            panel.localPosition = Vector3.zero;
            panel.localRotation = Quaternion.identity;
            panel.localScale = Vector3.one;
            var flat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath("df64bf5efda75be4db0c812f9277d678")), panel);
            PrefabUtility.UnpackPrefabInstance(flat, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            flat.name = "PanelDiagnostico";
            flat.transform.localPosition = Vector3.zero;
            flat.transform.localRotation = Quaternion.identity;
            flat.transform.localScale = Vector3.one;
            var canvas = flat.GetComponentInChildren<Canvas>();
            var rect = (RectTransform)canvas.transform;
            while (rect.childCount > 0) Object.DestroyImmediate(rect.GetChild(0).gameObject);
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * 0.001f;
            rect.sizeDelta = new Vector2(560, 560);
            canvas.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.09f, 0.98f);
            var surface = flat.transform.Find("Surface");
            surface.localPosition = Vector3.zero;
            surface.localScale = new Vector3(0.56f, 0.56f, 0.01f);
            var ray = flat.AddComponent<Oculus.Interaction.RayInteractable>();
            var rayData = new SerializedObject(ray);
            rayData.FindProperty("_surface").objectReferenceValue = surface.GetComponents<Component>().First(c => c.GetType().Name == "ClippedPlaneSurface");
            rayData.FindProperty("_pointableElement").objectReferenceValue = flat.GetComponents<Component>().First(c => c.GetType().Name == "PointableCanvas");
            rayData.ApplyModifiedPropertiesWithoutUndo();

            var view = flat.AddComponent<DiseaseSelectionView>();
            view.title = Text(rect, "Titulo", "1 / 2   Elige la enfermedad", 26, font, 24, 18, 512, 42);
            view.subtitle = Text(rect, "Subtitulo", "Observa la hoja que tienes en la mano", 18, font, 24, 65, 512, 50);
            view.optionsRoot = Rect(rect, "Opciones", 24, 122, 512, 286);
            view.optionsLayout = view.optionsRoot.gameObject.AddComponent<GridLayoutGroup>();
            view.optionsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            view.optionsLayout.constraintCount = 1;
            view.optionsLayout.cellSize = new Vector2(512, 62);
            view.optionsLayout.spacing = new Vector2(13, 10);
            view.summary = Text(rect, "Resumen", "Después elegirás la severidad.", 19, font, 24, 414, 512, 36);
            view.backButton = Button(rect, "Cambiar enfermedad", font, 24, 496, 244, 44);
            view.confirmButton = Button(rect, "Confirmar", font, 282, 496, 254, 44);
            view.previousButton = Button(rect, "Anterior", font, 24, 496, 160, 44);
            view.nextButton = Button(rect, "Siguiente", font, 376, 496, 160, 44);
            view.pageLabel = Text(rect, "Pagina", "", 18, font, 206, 496, 148, 44);
            view.pageLabel.alignment = TextAlignmentOptions.Center;
            view.backButton.gameObject.SetActive(false);
            view.confirmButton.gameObject.SetActive(false);
            view.previousButton.gameObject.SetActive(false);
            view.nextButton.gameObject.SetActive(false);
            var option = AssetDatabase.LoadAssetAtPath<DiagnosisOptionView>(OptionPath);
            if (option == null)
            {
                var templateButton = Button(null, "OpcionDiagnostico", font, 0, 0, 512, 62);
                var template = templateButton.gameObject.AddComponent<DiagnosisOptionView>();
                template.button = templateButton;
                template.background = templateButton.GetComponent<Image>();
                template.label = templateButton.GetComponentInChildren<TMP_Text>();
                template.label.fontSize = 24;
                option = PrefabUtility.SaveAsPrefabAsset(template.gameObject, OptionPath).GetComponent<DiagnosisOptionView>();
                Object.DestroyImmediate(template.gameObject);
            }
            view.optionPrefab = option;
            controller.view = view;
            controller.catalog = catalog;
            controller.animDuration = 0.25f;
            PrefabUtility.SaveAsPrefabAsset(contents, SelectorPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
    private static TMP_Text Text(Transform parent, string name, string text, float size, TMP_FontAsset font,
        float x, float y, float w, float h)
    {
        var label = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
    private static Button Button(Transform parent, string name, TMP_FontAsset font, float x, float y, float w, float h)
    {
        var rect = Rect(parent, name, x, y, w, h);
        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.19f, 0.22f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.7f, 0.9f, 1f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        button.colors = colors;
        var label = Text(rect, "Texto", name, 19, font, 12, 2, w - 24, h - 4);
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(12, 2);
        label.rectTransform.offsetMax = new Vector2(-12, -2);
        return button;
    }

    [MenuItem("Tools/RiceXR/Enfermedades/Validar configuración")]
    public static void Validate()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DiseaseCatalog>(CatalogPath);
        if (catalog == null || !catalog.IsValid(out _)) throw new InvalidOperationException("Catálogo inválido.");
        int count = 0;
        foreach (string path in V2Paths("Pyricularia").Concat(V2Paths("Rynchosporium")))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int severity = SeverityOf(prefab);
            var leaf = prefab.GetComponentInChildren<Leaf>(true);
            bool validDiagnosis = leaf.diseaseSpots != null
                && leaf.diseaseSpots.Count == 1
                && leaf.diseaseSpots[0] != null
                && leaf.diseaseSpots[0].disease != null
                && leaf.diseaseSpots[0].markerObject != null
                && leaf.diseaseSpots[0].severity == severity
                && leaf.IsDiagnosable(catalog);
            if (severity == 0 ? !leaf.IsHealthy : !validDiagnosis)
                throw new InvalidOperationException($"Diagnóstico incoherente: {path}");
            count++;
        }
        var selector = AssetDatabase.LoadAssetAtPath<GameObject>(SelectorPath).GetComponent<DiseaseSelectionSystem>();
        if (selector.catalog != catalog || selector.view == null || selector.view.optionPrefab == null)
            throw new InvalidOperationException("Selector sin cablear.");
        Debug.Log($"[Enfermedades] Validación correcta: {count} hojas v2 y selector.");
    }
}
