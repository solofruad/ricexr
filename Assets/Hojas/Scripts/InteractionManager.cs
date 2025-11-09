using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private Transform spawnParent;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private AnimationType animationType = AnimationType.ScaleDown;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

    public enum AnimationType
    {
        FadeOut,
        ScaleDown,
        ScaleAndFade,
        Dissolve
    }

    private bool hasBeenActivated = false;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Este método se llamará desde el botón UI
    public void OnAnchorButtonClicked(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
    {
        if (hasBeenActivated) return; // Evita clicks múltiples

        this.targetPosition = targetPosition;
        this.targetRotation = targetRotation;
        this.targetScale = targetScale;

        hasBeenActivated = true;
        StartCoroutine(DisableAllAnchorsWithAnimation());
    }

    private IEnumerator DisableAllAnchorsWithAnimation()
    {
        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();

        if (room == null)
        {
            Debug.LogWarning("No room found!");
            yield break;
        }

        // Obtén todos los anchors
        List<MRUKAnchor> anchors = new List<MRUKAnchor>();

        // Puedes filtrar por tipo de anchor si quieres
        foreach (var anchor in room.Anchors)
        {
            anchors.Add(anchor);
        }

        // Anima todos los anchors
        List<Coroutine> animations = new List<Coroutine>();

        foreach (var anchor in anchors)
        {
            if (anchor != null && anchor.gameObject.activeInHierarchy)
            {
                Coroutine anim = StartCoroutine(AnimateAnchorDisappearance(anchor.gameObject));
                animations.Add(anim);
            }
        }

        // Espera a que todas las animaciones terminen
        yield return new WaitForSeconds(fadeDuration);

        // Desactiva todos los anchors
        foreach (var anchor in anchors)
        {
            if (anchor != null)
            {
                anchor.gameObject.SetActive(false);
            }
        }

        SpawnNewObject();
    }

    private IEnumerator AnimateAnchorDisappearance(GameObject anchorObject)
    {
        float elapsed = 0f;
        Vector3 originalScale = anchorObject.transform.localScale;

        // Obtén todos los renderers para el fade
        Renderer[] renderers = anchorObject.GetComponentsInChildren<Renderer>();
        Canvas[] canvases = anchorObject.GetComponentsInChildren<Canvas>();
        CanvasGroup canvasGroup = anchorObject.GetComponent<CanvasGroup>();

        // Si hay canvas y no tiene CanvasGroup, agrégalo
        if (canvases.Length > 0 && canvasGroup == null)
        {
            canvasGroup = anchorObject.AddComponent<CanvasGroup>();
        }

        // Guarda los materiales originales
        Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        Dictionary<Renderer, Material[]> fadeMaterials = new Dictionary<Renderer, Material[]>();

        if (animationType == AnimationType.FadeOut || animationType == AnimationType.ScaleAndFade)
        {
            foreach (var renderer in renderers)
            {
                originalMaterials[renderer] = renderer.materials;
                Material[] newMats = new Material[renderer.materials.Length];

                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    // Crea una copia del material para no afectar otros objetos
                    newMats[i] = new Material(renderer.materials[i]);

                    // Cambia a modo transparente si es necesario
                    if (newMats[i].HasProperty("_Mode"))
                    {
                        newMats[i].SetFloat("_Mode", 3); // Transparent mode
                    }

                    // Habilita transparencia
                    newMats[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    newMats[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    newMats[i].SetInt("_ZWrite", 0);
                    newMats[i].DisableKeyword("_ALPHATEST_ON");
                    newMats[i].EnableKeyword("_ALPHABLEND_ON");
                    newMats[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    newMats[i].renderQueue = 3000;
                }

                fadeMaterials[renderer] = newMats;
                renderer.materials = newMats;
            }
        }

        // Animación
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            float easedT = EaseOutCubic(t);

            switch (animationType)
            {
                case AnimationType.ScaleDown:
                    anchorObject.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);
                    break;

                case AnimationType.FadeOut:
                    // Fade materials
                    foreach (var kvp in fadeMaterials)
                    {
                        foreach (var mat in kvp.Value)
                        {
                            Color color = mat.color;
                            color.a = 1f - easedT;
                            mat.color = color;

                            if (mat.HasProperty("_BaseColor"))
                            {
                                mat.SetColor("_BaseColor", color);
                            }
                        }
                    }

                    // Fade canvas
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 1f - easedT;
                    }
                    break;

                case AnimationType.ScaleAndFade:
                    // Scale
                    anchorObject.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);

                    // Fade materials
                    foreach (var kvp in fadeMaterials)
                    {
                        foreach (var mat in kvp.Value)
                        {
                            Color color = mat.color;
                            color.a = 1f - easedT;
                            mat.color = color;

                            if (mat.HasProperty("_BaseColor"))
                            {
                                mat.SetColor("_BaseColor", color);
                            }
                        }
                    }

                    // Fade canvas
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 1f - easedT;
                    }
                    break;

                case AnimationType.Dissolve:
                    // Combina scale y rotación para un efecto de disolución
                    anchorObject.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);
                    anchorObject.transform.Rotate(Vector3.up, Time.deltaTime * 180f);

                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 1f - easedT;
                    }
                    break;
            }

            yield return null;
        }

        // Limpia los materiales temporales
        foreach (var kvp in fadeMaterials)
        {
            foreach (var mat in kvp.Value)
            {
                Destroy(mat);
            }
        }
    }

    private void SpawnNewObject()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("No prefab assigned to spawn!");
            return;
        }

        ConfigureLeafSpawn();


        //Debug.Log($"Spawned {prefabToSpawn.name} at {spawnPosition}");
    }

    private void ConfigureLeafSpawn()
    {
        Transform parent = spawnParent != null ? spawnParent : transform;

        parent.rotation = targetRotation;
        GameObject instance = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity, parent);
        instance.transform.position = targetPosition;
        instance.transform.rotation = targetRotation;


        SpawnHojas leafSpawner = instance.GetComponent<SpawnHojas>();

        leafSpawner.SpawnAreaSize = new Vector2(targetScale.x, targetScale.y);
        Debug.Log(leafSpawner.SpawnAreaSize);

        leafSpawner.grassParent = parent;
        leafSpawner.grassCount = Mathf.Max((int)Mathf.Sqrt(targetScale.x * targetScale.y), 20 );


        leafSpawner.Activate();
    }

    // Easing function para animación más suave
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}

