using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DiseaseModelVerification
{
    public static void MigrateAndRun()
    {
        DiseaseModelMigration.Migrate();
        var paths = Directory.GetFiles("Assets/RiceXR/Prefabs", "*.prefab", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/RiceXR/ScriptableObjects/Diseases", "*.asset"))
            .Append("Assets/Scenes/Main.unity").ToArray();
        var before = paths.ToDictionary(p => p, File.ReadAllText);
        DiseaseModelMigration.Migrate();
        Check(paths.All(p => before[p] == File.ReadAllText(p)), "Migración idempotente");
        Run();
    }
    public static void Run()
    {
        DiseaseModelMigration.Validate();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Additive);
        try
        {
            var selector = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DiseaseSelectionSystem>(true)).Single();
            Check(selector.view != null && selector.catalog != null, "Referencias del selector en Main");
            Check(selector.diseaseSelectionPanel.transform.localPosition == Vector3.zero, "Panel centrado en su ancla");
            Check(selector.GetComponent<BillboardUI>() == null, "Ancla sin seguimiento de cámara");
            var canvas = selector.view.GetComponentInChildren<Canvas>();
            var corners = new Vector3[4];
            ((RectTransform)canvas.transform).GetWorldCorners(corners);
            Check(Mathf.Abs(Vector3.Distance(corners[0], corners[1]) - 0.56f) < 0.001f, "Altura mundial de 56 cm");
            Check(Mathf.Abs(Vector3.Distance(corners[1], corners[2]) - 0.56f) < 0.001f, "Anchura mundial de 56 cm");
            Check(selector.view.GetComponent<Oculus.Interaction.PokeInteractable>() != null, "Entrada poke Meta");
            Check(selector.view.GetComponent<Oculus.Interaction.RayInteractable>() != null, "Entrada de rayo Meta");
            var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SceneInteractionManager>(true)).Single();
            var levels = new SerializedObject(manager).FindProperty("levelConfigs");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var prefab = level.FindPropertyRelative("levelPrefab").objectReferenceValue as GameObject;
                Check(prefab != null, $"Prefab de nivel {i}");
                var spawner = new SerializedObject(prefab.GetComponent<LeavesSpawner>());
                Check(spawner.FindProperty("diseaseCatalog").objectReferenceValue == selector.catalog, $"Catálogo nivel {i}");
                var leaves = spawner.FindProperty("grassPrefabs");
                int eligible = 0;
                for (int j = 0; j < leaves.arraySize; j++)
                {
                    var leafPrefab = leaves.GetArrayElementAtIndex(j).objectReferenceValue as GameObject;
                    Check(leafPrefab != null && AssetDatabase.GetAssetPath(leafPrefab).Contains("/v2/"), $"Hoja v2 nivel {i}");
                    if (leafPrefab.GetComponentInChildren<Leaf>(true).IsDiagnosable(selector.catalog)) eligible++;
                }
                Check(eligible > 0, $"Nivel {i} completable");
                Check(((LeavesSpawner)spawner.targetObject).CanDiagnoseWith(selector.catalog, out _), $"Nivel {i} válido antes de iniciar");
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        VerifyViewAndCapture();
        Debug.Log("[Enfermedades QA] Escena, niveles, interacción, lista discontinua y vistas verificados.");
    }

    private static void VerifyViewAndCapture()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var pipeline = GraphicsSettings.defaultRenderPipeline;
        var qualityPipeline = QualitySettings.renderPipeline;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RiceXR/Prefabs/UI/DiseaseSeveritySelectorUI.prefab");
            var instance = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            var system = instance.GetComponent<DiseaseSelectionSystem>();
            var view = system.view;
            view.ShowDiseases(system.catalog);
            var options = view.optionsRoot.GetComponentsInChildren<DiagnosisOptionView>();
            Check(options.Length == system.catalog.diseases.Count, "Opciones desde catálogo");
            DiseaseDefinition chosen = null;
            view.DiseaseChosen += d => chosen = d;
            options[1].button.onClick.Invoke();
            Check(chosen == system.catalog.diseases[1], "Callback conserva la referencia de enfermedad");
            var cameraObject = new GameObject("Camara QA", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -1);
            camera.orthographic = true;
            camera.orthographicSize = 0.33f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
            camera.scene = scene;
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            Capture(camera, view, "Logs/selector-enfermedades.png");
            Object.DestroyImmediate(instance);
            instance = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            system = instance.GetComponent<DiseaseSelectionSystem>();
            view = system.view;
            view.ShowSeverities(chosen);
            Check(!view.confirmButton.interactable, "Confirmación bloqueada sin severidad");
            options = view.optionsRoot.GetComponentsInChildren<DiagnosisOptionView>();
            Check(options.Length == 5, "Cinco valores discontinuos en Ryncho");
            int selected = -1;
            view.SeverityChosen += v => selected = v;
            options[1].button.onClick.Invoke();
            Check(selected == 3, "Segunda opción de Ryncho transmite 3");
            view.ShowSeverities(chosen, selected);
            Check(view.confirmButton.interactable, "Confirmación habilitada al elegir valor");
            Capture(camera, view, "Logs/selector-severidades.png");
            view.ShowSeverities(system.catalog.diseases[0]);
            Check(!view.confirmButton.interactable && view.optionsRoot.GetComponentsInChildren<DiagnosisOptionView>().Length == 9,
                "Cambio a Pyri limpia severidad y presenta nueve valores");
            view.ShowDiseases(system.catalog);
            Check(!view.confirmButton.gameObject.activeSelf, "Volver a enfermedad limpia el segundo paso");
        }
        finally
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = qualityPipeline;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Capture(Camera camera, DiseaseSelectionView view, string path)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(view.optionsRoot);
        foreach (var label in view.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            label.ForceMeshUpdate(true, true);
            label.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.All);
        }
        Canvas.ForceUpdateCanvases();
        var target = new RenderTexture(1024, 1024, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("[Enfermedades QA] " + description);
    }
}
