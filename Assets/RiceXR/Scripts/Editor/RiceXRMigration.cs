using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Migra las hojas (Ryncho/Pyri) al modelo nuevo exportado desde Blender (ryncho_v2.fbx / pyri_v2.fbx):
/// cada hoja tiene dos meshes en el FBX: '{hoja}' (la que se lleva al agarrar) y '{hoja}.base'
/// (la base que se queda pegada al suelo).
///
/// Por prefab construye la estructura del EJEMPLO_PREFAB:
///   root (BaseLeafDeasease)
///     ├── {hoja}.base  -> mesh base bajo la raiz (fuera de GrabHoja): se queda al arrancar la hoja
///     ├── GrabHoja
///     │    └── Visuals
///     │         └── {hoja} -> mesh que se lleva (BurnLeafRenderer + viento)
///     └── ...
///
/// Materiales:
///   - Hoja: Custom/BurnLeaf_VR (viento + quemado), textura de la hoja.
///   - Base: URP/Lit con alpha clip y doble cara (el fondo del PNG es transparente).
///
/// Ejecutar desde el menu Tools/RiceXR/Migrar Hojas ..., o en batch:
///   Unity -batchmode -projectPath . -executeMethod RiceXRMigration.MigrateRyncho -quit
///   Unity -batchmode -projectPath . -executeMethod RiceXRMigration.MigratePyri -quit
///
/// Para agregar una hoja nueva: exportar el FBX de Blender con '{hoja}' y '{hoja}.base',
/// copiar la textura a Art/Textures/Leaves/&lt;Enfermedad&gt;/ con el mismo nombre y anadir la linea
/// "PrefabN|{hoja}|{hoja}.ext" al mapeo. Si el prefab no existe se crea copiando la plantilla.
/// </summary>
public static class RiceXRMigration
{
    private const string RynchoFbxPath = "Assets/RiceXR/Art/Models/ryncho_v2.fbx";
    private const string RynchoOldFbxPath = "Assets/RiceXR/Art/Models/ryncho2.fbx";
    private const string RynchoMatDir = "Assets/RiceXR/Art/Materials/Leaves/Rynchosporium";
    private const string RynchoTexDir = "Assets/RiceXR/Art/Textures/Leaves/Rynchosporium";
    private const string RynchoPrefabDir = "Assets/RiceXR/Prefabs/Leaves/Rynchosporium/v2";

    private const string PyriFbxPath = "Assets/RiceXR/Art/Models/pyri_v2.fbx";
    private const string PyriOldFbxPath = "Assets/RiceXR/Art/Models/pyri.fbx";
    private const string PyriMatDir = "Assets/RiceXR/Art/Materials/Leaves/Pyricularia";
    private const string PyriTexDir = "Assets/RiceXR/Art/Textures/Leaves/Pyricularia";
    private const string PyriPrefabDir = "Assets/RiceXR/Prefabs/Leaves/Pyricularia/v2";

    private const string NoisePath = "Assets/RiceXR/Art/Textures/World/noise.png";

    // prefab | nombre del mesh (y del material .mat) | textura (mismo nombre que el material, otra extension)
    private static readonly string[] RynchoMapping =
    {
        "Ryncho1|0Ryncho (1)|0Ryncho (1).jpg",
        "Ryncho2|0Ryncho (2)|0Ryncho (2).jpg",
        "Ryncho3|1Ryncho (1)|1Ryncho (1).png",
        "Ryncho4|1Ryncho (2)|1Ryncho (2).jpg",
        "Ryncho5|3Ryncho (1)|3Ryncho (1).png",
        "Ryncho6|5Ryncho (1)|5Ryncho (1).png",
        "Ryncho7|7Ryncho (1)|7Ryncho (1).png",
        "Ryncho8|9Ryncho (1)|9Ryncho (1).png",
        "Ryncho9|9Ryncho (2)|9Ryncho (2).png",
    };

    private static readonly string[] PyriMapping =
    {
        "Pyricularia1|0Pyri (3)|0Pyri (3).jpg",
        "Pyricularia2|1Pyri (3)|1Pyri (3).png",
        "Pyricularia3|2Pyri (1) G|2Pyri (1) G.png",
        "Pyricularia4|3Pyri (2)|3Pyri (2).png",
        "Pyricularia5|4Pyri (1) G|4Pyri (1) G.png",
        "Pyricularia6|4Pyri (2) G|4Pyri (2) G.png",
        "Pyricularia7|5Pyri (1) G|5Pyri (1) G.png",
        "Pyricularia8|5Pyri (2) G|5Pyri (2) G.png",
        "Pyricularia9|5Pyri (3) G|5Pyri (3) G.png",
        "Pyricularia10|6Pyri (1) G|6Pyri (1) G.png",
        "Pyricularia11|6Pyri (2) G|6Pyri (2) G.png",
        "Pyricularia12|6Pyri (3) G|6Pyri (3) G.png",
        "Pyricularia13|7Pyri (1) G|7Pyri (1) G.png",
        "Pyricularia14|7Pyri (3) G|7Pyri (3) G.png",
        "Pyricularia15|8Pyri (2) G|8Pyri (2) G.png",
        "Pyricularia16|9Pyri (2) G|9Pyri (2) G.png",
        "Pyricularia17|9Pyri (3) G|9Pyri (3) G.png",
    };

    [MenuItem("Tools/RiceXR/Migrar Hojas Ryncho v2")]
    public static void MigrateRyncho()
    {
        RunMigration("Ryncho", RynchoFbxPath, RynchoOldFbxPath, RynchoMatDir, RynchoTexDir, RynchoPrefabDir, RynchoMapping, "Ryncho1");
    }

    [MenuItem("Tools/RiceXR/Migrar Hojas Pyri v2")]
    public static void MigratePyri()
    {
        RunMigration("Pyri", PyriFbxPath, PyriOldFbxPath, PyriMatDir, PyriTexDir, PyriPrefabDir, PyriMapping, "Pyricularia1");
    }

    private static void RunMigration(
        string tag, string fbxPath, string oldFbxPath, string matDir, string texDir,
        string prefabDir, string[] mapping, string templatePrefab)
    {
        Debug.Log($"[RiceXRMigration] === Inicio migracion {tag} v2 ===");

        // 1) Importar el FBX nuevo con los settings correctos
        AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null)
        {
            Debug.LogError("[RiceXRMigration] No se encontro el ModelImporter de " + fbxPath);
            return;
        }

        bool dirty = false;
        if (!imp.isReadable) { imp.isReadable = true; dirty = true; }
        if (imp.importAnimation) { imp.importAnimation = false; dirty = true; }
        if (imp.importCameras) { imp.importCameras = false; dirty = true; }
        if (imp.importLights) { imp.importLights = false; dirty = true; }
        if (imp.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            dirty = true;
        }
        if (dirty) imp.SaveAndReimport();

        // 2) Recoger los meshes del FBX por nombre normalizado
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (root == null)
        {
            Debug.LogError("[RiceXRMigration] No se pudo cargar el FBX como GameObject");
            return;
        }

        var meshes = new Dictionary<string, Mesh>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            string key = Normalize(mf.gameObject.name);
            meshes[key] = mf.sharedMesh;
            Debug.Log($"[RiceXRMigration] Mesh '{mf.gameObject.name}' -> '{key}' bounds={mf.sharedMesh.bounds}");
        }

        // 3) Verificar vertex colors en las hojas (mascara de viento en R)
        foreach (var kv in meshes)
        {
            if (kv.Key.EndsWith(".base")) continue; // las bases no llevan mascara
            Mesh m = kv.Value;
            if (!m.isReadable)
            {
                Debug.LogError($"[RiceXRMigration] Mesh {kv.Key} no es readable (isReadable)");
                continue;
            }
            Color[] cols = m.colors;
            if (cols == null || cols.Length == 0)
            {
                Debug.LogError($"[RiceXRMigration] Mesh {kv.Key} NO tiene vertex colors");
                continue;
            }
            float minR = float.MaxValue, maxR = float.MinValue, sum = 0f;
            foreach (Color c in cols)
            {
                minR = Mathf.Min(minR, c.r);
                maxR = Mathf.Max(maxR, c.r);
                sum += c.r;
            }
            Debug.Log($"[RiceXRMigration] Mesh {kv.Key}: {cols.Length} colores, R[min={minR:F3}, max={maxR:F3}, avg={sum / cols.Length:F3}]");
        }

        // 4) Textura de ruido compartida
        Texture2D noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        if (noise == null)
        {
            Debug.LogError("[RiceXRMigration] No se encontro el ruido en " + NoisePath);
            return;
        }

        // 5) Por cada hoja: textura, materiales y prefab
        foreach (string entry in mapping)
        {
            string[] parts = entry.Split('|');
            string prefabName = parts[0];
            string leafKey = Normalize(parts[1]);
            string texFile = parts[2];

            if (!meshes.TryGetValue(leafKey, out Mesh leafMesh))
            {
                Debug.LogError($"[RiceXRMigration] No hay mesh para '{parts[1]}' (key '{leafKey}')");
                continue;
            }

            // Base mesh opcional (misma hoja + ".base")
            Mesh baseMesh = null;
            if (meshes.TryGetValue(leafKey + ".base", out Mesh bm)) baseMesh = bm;
            else Debug.LogWarning($"[RiceXRMigration] No hay mesh base para '{parts[1]}' (se omite la base)");

            string texPath = $"{texDir}/{texFile}";
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogError("[RiceXRMigration] No se encontro la textura " + texPath);
                continue;
            }

            // Material de la hoja (viento + quemado)
            string leafMatPath = $"{matDir}/{parts[1]}.mat";
            Material leafMat = GetOrCreateLeafMaterial(leafMatPath, tex, noise);
            if (leafMat == null) continue;

            // Material de la base (URP/Lit, alpha clip, doble cara)
            string baseMatPath = $"{matDir}/{parts[1]}_base.mat";
            Material baseMat = baseMesh != null ? GetOrCreateBaseMaterial(baseMatPath, tex) : null;

            string prefabPath = $"{prefabDir}/{prefabName}.prefab";
            if (!File.Exists(prefabPath))
            {
                if (string.IsNullOrEmpty(templatePrefab))
                {
                    Debug.LogError("[RiceXRMigration] No existe el prefab " + prefabPath + " y no hay plantilla configurada");
                    continue;
                }
                string templatePath = $"{prefabDir}/{templatePrefab}.prefab";
                if (!File.Exists(templatePath))
                {
                    Debug.LogError("[RiceXRMigration] La plantilla no existe: " + templatePath);
                    continue;
                }
                File.Copy(templatePath, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[RiceXRMigration] Prefab nuevo creado copiando plantilla: " + prefabPath);
            }

            MigratePrefab(prefabPath, prefabName, leafMesh, baseMesh, leafMat, baseMat, tex, oldFbxPath, fbxPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[RiceXRMigration] === Migracion {tag} v2 terminada ===");
    }

    // -------------------------------------------------------------------------
    // Materiales
    // -------------------------------------------------------------------------

    private static Material GetOrCreateLeafMaterial(string path, Texture2D baseMap, Texture2D noise)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Custom/BurnLeaf_VR");
            if (shader == null)
            {
                Debug.LogError("[RiceXRMigration] No se encontro el shader Custom/BurnLeaf_VR");
                return null;
            }
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("[RiceXRMigration] Material de hoja creado: " + path);
        }

        mat.SetTexture("_BaseMap", baseMap);
        mat.SetTexture("_NoiseMap", noise);
        mat.SetFloat("_Cull", 0f); // doble cara
        mat.SetFloat("_NoiseTiling", 0.3f);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_EdgeColorA", new Color(1f, 0f, 0f));
        mat.SetColor("_EdgeColorB", new Color(1f, 0.97f, 0f));
        mat.SetFloat("_EdgeIntensity", 1.7f);
        return mat;
    }

    private static Material GetOrCreateBaseMaterial(string path, Texture2D baseMap)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[RiceXRMigration] No se encontro el shader URP/Lit");
                return null;
            }
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("[RiceXRMigration] Material de base creado: " + path);
        }

        mat.SetTexture("_BaseMap", baseMap);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Cull", 0f);          // doble cara
        mat.SetFloat("_AlphaClip", 1f);     // recorte por alpha (PNG con fondo transparente)
        mat.SetFloat("_Cutoff", 0.5f);
        mat.renderQueue = 2450;             // AlphaTest
        return mat;
    }

    // -------------------------------------------------------------------------
    // Prefab
    // -------------------------------------------------------------------------

    private static void MigratePrefab(
        string prefabPath, string prefabName, Mesh leafMesh, Mesh baseMesh,
        Material leafMat, Material baseMat, Texture2D tex,
        string oldFbxPath, string fbxPath)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            contents.name = prefabName; // renombrar la raiz (relevante al clonar una plantilla)
            Transform rootT = contents.transform;
            Transform visualsT = FindVisuals(rootT);

            // 1) Eliminar los mesh children visuales previos (de migraciones anteriores)
            var toDelete = new List<GameObject>();
            foreach (var mf in contents.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.gameObject.activeInHierarchy) continue;
                if (IsInsideMarker(mf.transform, rootT)) continue;
                toDelete.Add(mf.gameObject);
            }
            foreach (GameObject go in toDelete)
            {
                Debug.Log($"[RiceXRMigration] Eliminando mesh visual previo: {go.name}");
                Object.DestroyImmediate(go);
            }

            // 2) Base: mesh que se queda, hijo directo de la raiz (fuera de GrabHoja)
            if (baseMesh != null && baseMat != null)
            {
                Quaternion baseRot = ComputeStandRotation(baseMesh);
                GameObject baseGo = new GameObject($"{prefabName}.base");
                baseGo.transform.SetParent(rootT, false);
                baseGo.AddComponent<MeshFilter>().sharedMesh = baseMesh;
                var baseRend = baseGo.AddComponent<MeshRenderer>();
                baseRend.sharedMaterials = new[] { baseMat };
                baseGo.transform.localRotation = baseRot;
                baseGo.transform.localScale = Vector3.one;
                Vector3 basePos = Vector3.zero;
                basePos.y = -RotatedMinY(baseMesh, baseRot);
                baseGo.transform.localPosition = basePos;
                Debug.Log($"[RiceXRMigration] Base '{baseGo.name}' creada bajo la raiz, rot={baseRot.eulerAngles} pos={basePos}");
            }
            else
            {
                Debug.LogWarning($"[RiceXRMigration] {prefabPath}: sin base mesh/material, se omite la base");
            }

            // 3) Hoja: mesh que se lleva, hijo de Visuals (dentro de GrabHoja)
            Quaternion leafRot = ComputeStandRotation(leafMesh);
            Vector3 leafGroundPos = Vector3.zero;
            leafGroundPos.y = -RotatedMinY(leafMesh, leafRot);

            GameObject leafGo = new GameObject(prefabName);
            leafGo.transform.SetParent(visualsT != null ? visualsT : rootT, false);
            leafGo.AddComponent<MeshFilter>().sharedMesh = leafMesh;
            var leafRend = leafGo.AddComponent<MeshRenderer>();
            leafRend.sharedMaterials = new[] { leafMat };
            var blr = leafGo.AddComponent<BurnLeafRenderer>();
            blr.albedo = tex;

            // Si la hoja va bajo Visuals (que tiene un tilt de ~9.5 grados),
            // compensar la rotacion del contenedor para que quede de pie en mundo
            if (visualsT != null)
            {
                Quaternion visualsRot = visualsT.localRotation;
                leafGo.transform.localRotation = Quaternion.Inverse(visualsRot) * leafRot;
                leafGo.transform.localPosition = Quaternion.Inverse(visualsRot) * leafGroundPos;
            }
            else
            {
                leafGo.transform.localRotation = leafRot;
                leafGo.transform.localPosition = leafGroundPos;
            }
            leafGo.transform.localScale = Vector3.one;

            // 4) Cablear Leaf -> burnLeafRenderer de la hoja
            var leaf = contents.GetComponentInChildren<Leaf>(true);
            if (leaf != null)
            {
                var so = new SerializedObject(leaf);
                SerializedProperty prop = so.FindProperty("burnLeafRenderer");
                if (prop != null)
                {
                    prop.objectReferenceValue = blr;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 5) LeafWindController en la raiz (auto-detecta renderers; la base es URP/Lit,
            //    asi que el viento no la afecta aunque el controller la toque)
            if (rootT.GetComponent<LeafWindController>() == null)
                rootT.gameObject.AddComponent<LeafWindController>();

            Debug.Log($"[RiceXRMigration] {prefabPath}: hoja '{leafGo.name}' bajo {(visualsT != null ? "Visuals" : "raiz")}, rot={leafGo.transform.localRotation.eulerAngles} pos={leafGo.transform.localPosition}");
        }
        finally
        {
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static Transform FindVisuals(Transform root)
    {
        Transform v = root.Find("GrabHoja/Visuals");
        if (v != null) return v;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "Visuals") return t;
        return null;
    }

    private static bool IsInsideMarker(Transform t, Transform root)
    {
        while (t != null && t != root)
        {
            if (t.name == "Marker") return true;
            t = t.parent;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Helpers de orientacion
    // -------------------------------------------------------------------------

    private static Quaternion ComputeStandRotation(Mesh mesh)
    {
        Vector3 size = mesh.bounds.size;
        int longAxis = AxisOfMax(size);
        int flatAxis = AxisOfMin(size);

        Vector3 l = UnitAxis(longAxis);
        Vector3 f = UnitAxis(flatAxis);

        Quaternion rot = Quaternion.FromToRotation(l, Vector3.up);

        Vector3 fT = rot * f;
        if (Mathf.Abs(fT.y) < 0.99f)
        {
            float angle = Mathf.Atan2(fT.x, fT.z) * Mathf.Rad2Deg;
            rot = Quaternion.AngleAxis(angle, Vector3.up) * rot;
        }

        return rot;
    }

    private static int AxisOfMax(Vector3 v)
    {
        if (v.x >= v.y && v.x >= v.z) return 0;
        if (v.y >= v.z) return 1;
        return 2;
    }

    private static int AxisOfMin(Vector3 v)
    {
        if (v.x <= v.y && v.x <= v.z) return 0;
        if (v.y <= v.z) return 1;
        return 2;
    }

    private static Vector3 UnitAxis(int axis)
    {
        return axis == 0 ? Vector3.right : (axis == 1 ? Vector3.up : Vector3.forward);
    }

    private static float RotatedMinY(Mesh mesh, Quaternion rot)
    {
        Bounds b = mesh.bounds;
        Vector3[] corners =
        {
            new Vector3(b.min.x, b.min.y, b.min.z),
            new Vector3(b.min.x, b.min.y, b.max.z),
            new Vector3(b.min.x, b.max.y, b.min.z),
            new Vector3(b.min.x, b.max.y, b.max.z),
            new Vector3(b.max.x, b.min.y, b.min.z),
            new Vector3(b.max.x, b.min.y, b.max.z),
            new Vector3(b.max.x, b.max.y, b.min.z),
            new Vector3(b.max.x, b.max.y, b.max.z),
        };
        float minY = float.MaxValue;
        foreach (Vector3 c in corners)
            minY = Mathf.Min(minY, (rot * c).y);
        return minY;
    }

    // -------------------------------------------------------------------------
    // Nombres
    // -------------------------------------------------------------------------

    private static string Normalize(string s)
    {
        s = Regex.Replace(s, @"\.\d{3}$", "");
        return new string(s.Where(ch => !char.IsWhiteSpace(ch)).Select(char.ToLowerInvariant).ToArray());
    }
}