using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta en la escena activa el grafo de IActiveState que alimenta a InGameOptionsOpener:
///   - Gesto: palma izquierda hacia arriba + mano abierta, y NO palma hacia la cara
///     (Meta reserva palma hacia la cara + pinch para el menu universal).
///   - Boton de menu (☰) del mando izquierdo.
///
/// Crea el GameObject "InGameOptionsOpener" con sus hijos, los proveedores de estado de
/// dedos y de transform de la mano izquierda, y cablea todas las referencias por
/// SerializedObject. Es idempotente: si el objeto ya existe se reutiliza y se reconfigura
/// sin duplicar componentes. Tras ejecutarlo hay que guardar la escena.
///
/// Menu: Tools/RiceXR/Montar apertura de opciones en partida.
/// </summary>
public static class InGameOptionsSetup
{
    private const string LogPrefix = "[InGameOptionsSetup]";
    private const string RootName = "InGameOptionsOpener";

    private const string FingersFlexionAllOpenGuid = "0c2253e876d14fa4f923781b4b2d4e0f";
    private const string FingersAllNotClosedGuid = "4a3b8e6157d10b6478b98530bd35dcae";
    private const string DefaultFingerThresholdsGuid = "8b470e0c4b48d474c9a42d7f661d48c7";
    private const string DefaultTransformThresholdsGuid = "039cf5a7424e1e046b79287e9375cf09";

    // Estado "True" de cada TransformFeature: indice de la feature * 3 (ver
    // TransformFeatureProperties del Interaction SDK). PalmUp = 3, PalmTowardsFace = 4.
    private const string PalmUpTrueState = "9";
    private const string PalmTowardsFaceTrueState = "12";

    private const int TransformFeaturePalmUp = 3;
    private const int TransformFeaturePalmTowardsFace = 4;
    private const int UpVectorTypeWorld = 2;
    private const int ControllerButtonUsageMenuButton = 1 << 6;
    private const int HandFingerCount = 5;

    /// <summary>
    /// Construye o reconfigura el grafo de apertura de opciones en la escena activa.
    /// </summary>
    [MenuItem("Tools/RiceXR/Montar apertura de opciones en partida")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"{LogPrefix} No hay una escena activa cargada.");
            return;
        }

        Hand leftHand = FindLeft(FindInScene<Hand>(scene), out SerializedObject handSource);
        if (leftHand == null)
        {
            Debug.LogError($"{LogPrefix} No se encontro la mano izquierda del Interaction SDK " +
                           "(Hand con handedness Left) en la escena activa. Abortado.");
            return;
        }

        Controller leftController = FindLeft(FindInScene<Controller>(scene), out _);
        if (leftController == null)
        {
            Debug.LogError($"{LogPrefix} No se encontro el mando izquierdo del Interaction SDK " +
                           "(Controller con handedness Left) en la escena activa. Abortado.");
            return;
        }

        Hmd hmd = FirstOrNull(FindInScene<Hmd>(scene));
        if (hmd == null)
        {
            Debug.LogError($"{LogPrefix} No se encontro un Hmd del Interaction SDK en la escena. Abortado.");
            return;
        }

        Object trackingToWorld = ResolveTrackingToWorld(handSource, scene);
        if (trackingToWorld == null)
        {
            Debug.LogError($"{LogPrefix} No se encontro un ITrackingToWorldTransformer " +
                           "(TransformTrackingToWorldTransformer) en la escena. Abortado.");
            return;
        }

        Object flexionAllOpen = LoadByGuid(FingersFlexionAllOpenGuid);
        Object allNotClosed = LoadByGuid(FingersAllNotClosedGuid);
        Object fingerThresholds = LoadByGuid(DefaultFingerThresholdsGuid);
        Object transformThresholds = LoadByGuid(DefaultTransformThresholdsGuid);
        if (flexionAllOpen == null || allNotClosed == null ||
            fingerThresholds == null || transformThresholds == null)
        {
            Debug.LogError($"{LogPrefix} Faltan assets del Interaction SDK (formas o umbrales por " +
                           "defecto). Verificar que el paquete com.meta.xr.sdk.interaction este importado. Abortado.");
            return;
        }

        GameObject root = FindRoot(scene);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Montar apertura de opciones");
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        InGameOptionsOpener opener = GetOrAdd<InGameOptionsOpener>(root);

        HandRef handRef = GetOrAdd<HandRef>(GetChild(root, "HandRef"));
        SetObject(handRef, "_hand", leftHand);

        GameObject providersGo = GetChild(root, "Providers");
        Oculus.Interaction.PoseDetection.FingerFeatureStateProvider fingerProvider =
            GetOrAdd<Oculus.Interaction.PoseDetection.FingerFeatureStateProvider>(providersGo);
        ConfigureFingerProvider(fingerProvider, leftHand, fingerThresholds);

        Oculus.Interaction.PoseDetection.TransformFeatureStateProvider transformProvider =
            GetOrAdd<Oculus.Interaction.PoseDetection.TransformFeatureStateProvider>(providersGo);
        SetObject(transformProvider, "_hand", leftHand);
        SetObject(transformProvider, "_hmd", hmd);
        SetObject(transformProvider, "_trackingToWorldTransformer", trackingToWorld);

        var openHand = GetOrAdd<Oculus.Interaction.PoseDetection.ShapeRecognizerActiveState>(
            GetChild(root, "OpenHand"));
        SetObject(openHand, "_hand", handRef);
        SetObject(openHand, "_fingerFeatureStateProvider", fingerProvider);
        SetObjectArray(openHand, "_shapes", new[] { flexionAllOpen, allNotClosed });

        var palmUp = GetOrAdd<Oculus.Interaction.PoseDetection.TransformRecognizerActiveState>(
            GetChild(root, "PalmUp"));
        ConfigureTransformRecognizer(palmUp, handRef, transformProvider, transformThresholds,
            TransformFeaturePalmUp, PalmUpTrueState);

        var palmTowardsFace = GetOrAdd<Oculus.Interaction.PoseDetection.TransformRecognizerActiveState>(
            GetChild(root, "PalmTowardsFace"));
        ConfigureTransformRecognizer(palmTowardsFace, handRef, transformProvider, transformThresholds,
            TransformFeaturePalmTowardsFace, PalmTowardsFaceTrueState);

        ActiveStateNot notPalmTowardsFace = GetOrAdd<ActiveStateNot>(GetChild(root, "NotPalmTowardsFace"));
        SetObject(notPalmTowardsFace, "_activeState", palmTowardsFace);

        ActiveStateGroup gesture = GetOrAdd<ActiveStateGroup>(GetChild(root, "OpenGesture"));
        SetObjectArray(gesture, "_activeStates", new Object[] { openHand, palmUp, notPalmTowardsFace });
        SetInt(gesture, "_logicOperator", (int)ActiveStateGroup.ActiveStateGroupLogicOperator.AND);

        ControllerButtonUsageActiveState menuButton =
            GetOrAdd<ControllerButtonUsageActiveState>(GetChild(root, "MenuButton"));
        SetObject(menuButton, "_controller", leftController);
        SetInt(menuButton, "_controllerButtonUsage", ControllerButtonUsageMenuButton);

        OptionsController options = FirstOrNull(FindInScene<OptionsController>(scene));
        if (options == null)
            Debug.LogWarning($"{LogPrefix} No hay un OptionsController en la escena: " +
                             "InGameOptionsOpener no podra abrir ningun panel.");

        SetObject(opener, "optionsController", options);
        SetObject(opener, "openGestureSource", gesture);
        SetObject(opener, "menuButtonSource", menuButton);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log($"{LogPrefix} Grafo montado en '{RootName}'. Mano izquierda: '{leftHand.name}', " +
                  $"mando izquierdo: '{leftController.name}', Hmd: '{hmd.name}'. " +
                  $"OptionsController: {(options != null ? options.name : "no encontrado")}. " +
                  "Guarda la escena (Ctrl+S) para conservar los cambios.");
    }

    private static void ConfigureFingerProvider(
        Oculus.Interaction.PoseDetection.FingerFeatureStateProvider provider, Object hand, Object thresholds)
    {
        var so = new SerializedObject(provider);
        so.FindProperty("_hand").objectReferenceValue = hand;

        SerializedProperty list = so.FindProperty("_fingerStateThresholds");
        list.arraySize = HandFingerCount;
        for (int i = 0; i < HandFingerCount; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Finger").intValue = i;
            entry.FindPropertyRelative("StateThresholds").objectReferenceValue = thresholds;
        }

        so.ApplyModifiedProperties();
    }

    private static void ConfigureTransformRecognizer(
        Object recognizer, Object hand, Object provider, Object thresholds, int feature, string state)
    {
        var so = new SerializedObject(recognizer);
        so.FindProperty("_hand").objectReferenceValue = hand;
        so.FindProperty("_transformFeatureStateProvider").objectReferenceValue = provider;

        SerializedProperty values = so.FindProperty("_transformFeatureConfigs._values");
        values.arraySize = 1;
        SerializedProperty config = values.GetArrayElementAtIndex(0);
        config.FindPropertyRelative("_mode").intValue = 0;
        config.FindPropertyRelative("_feature").intValue = feature;
        config.FindPropertyRelative("_state").stringValue = state;

        SerializedProperty transformConfig = so.FindProperty("_transformConfig");
        transformConfig.FindPropertyRelative("PositionOffset").vector3Value = Vector3.zero;
        transformConfig.FindPropertyRelative("RotationOffset").vector3Value = Vector3.zero;
        transformConfig.FindPropertyRelative("UpVectorType").intValue = UpVectorTypeWorld;
        transformConfig.FindPropertyRelative("FeatureThresholds").objectReferenceValue = thresholds;

        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Devuelve el primer componente cuya cadena de DataModifier termina en una fuente con
    /// handedness Left. Entrega ademas el SerializedObject de esa fuente (FromOVRHandDataSource
    /// o FromOVRControllerDataSource) para poder leer sus referencias.
    /// </summary>
    private static T FindLeft<T>(List<T> candidates, out SerializedObject source) where T : Component
    {
        foreach (T candidate in candidates)
        {
            SerializedObject found = ResolveHandednessSource(candidate, out int handedness);
            if (found != null && handedness == (int)Handedness.Left)
            {
                source = found;
                return candidate;
            }
        }

        source = null;
        return null;
    }

    private static SerializedObject ResolveHandednessSource(Component start, out int handedness)
    {
        handedness = -1;
        Component current = start;
        for (int depth = 0; depth < 16 && current != null; depth++)
        {
            var so = new SerializedObject(current);
            SerializedProperty handednessProp = so.FindProperty("_handedness");
            if (handednessProp != null)
            {
                handedness = handednessProp.intValue;
                return so;
            }

            SerializedProperty next = so.FindProperty("_iModifyDataFromSourceMono");
            current = next?.objectReferenceValue as Component;
        }

        return null;
    }

    private static Object ResolveTrackingToWorld(SerializedObject handSource, Scene scene)
    {
        Object fromSource = handSource?.FindProperty("_trackingToWorldTransformer")?.objectReferenceValue;
        if (fromSource != null) return fromSource;

        return FirstOrNull(FindInScene<TransformTrackingToWorldTransformer>(scene));
    }

    private static GameObject FindRoot(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == RootName) return go;
        }

        return null;
    }

    private static GameObject GetChild(GameObject parent, string name)
    {
        Transform existing = parent.transform.Find(name);
        if (existing != null) return existing.gameObject;

        var child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Montar apertura de opciones");
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(go);
    }

    private static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (T item in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.gameObject.scene == scene) result.Add(item);
        }

        return result;
    }

    private static T FirstOrNull<T>(List<T> list) where T : class
    {
        return list.Count > 0 ? list[0] : null;
    }

    private static Object LoadByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
    }

    private static void SetObject(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetObjectArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty array = so.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }
}
