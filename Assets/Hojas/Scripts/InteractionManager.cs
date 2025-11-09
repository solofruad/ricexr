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
    [SerializeField] private float scaleDuration = 0.5f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;

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
        StartCoroutine(DisableAllPlanePrefabsWithAnimation());
    }

    private IEnumerator DisableAllPlanePrefabsWithAnimation()
    {
        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();

        if (room == null)
        {
            Debug.LogWarning("No room found!");
            yield break;
        }

        // Obtén todos los PlanePrefab de cada anchor
        List<GameObject> planePrefabs = new List<GameObject>();

        foreach (var anchor in room.Anchors)
        {
            if (anchor != null && anchor.gameObject.activeInHierarchy)
            {
                // Busca el PlanePrefab dentro del anchor
                Transform planePrefab = FindPlanePrefab(anchor.transform);
                if (planePrefab != null)
                {
                    planePrefabs.Add(planePrefab.gameObject);
                }
            }
        }

        // Anima todos los PlanePrefabs
        List<Coroutine> animations = new List<Coroutine>();

        foreach (var planePrefab in planePrefabs)
        {
            if (planePrefab != null && planePrefab.activeInHierarchy)
            {
                Coroutine anim = StartCoroutine(AnimatePlanePrefabScaleDown(planePrefab));
                animations.Add(anim);
            }
        }

        // Espera a que todas las animaciones terminen
        yield return new WaitForSeconds(scaleDuration);

        // Desactiva solo los PlanePrefabs
        foreach (var planePrefab in planePrefabs)
        {
            if (planePrefab != null)
            {
                planePrefab.SetActive(false);
            }
        }

        SpawnNewObject();
    }

    // Busca el PlanePrefab en la jerarquía del anchor
    private Transform FindPlanePrefab(Transform parent)
    {
        // Busca primero en los hijos directos
        foreach (Transform child in parent)
        {
            if (child.name.Contains("PlanePrefab"))
            {
                return child;
            }
        }

        // Si no lo encuentra, busca recursivamente
        foreach (Transform child in parent)
        {
            Transform found = FindPlanePrefab(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private IEnumerator AnimatePlanePrefabScaleDown(GameObject planePrefab)
    {
        float elapsed = 0f;
        Vector3 originalScale = planePrefab.transform.localScale;

        // Animación de escala
        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleDuration;
            float easedT = EaseOutCubic(t);

            planePrefab.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);

            yield return null;
        }

        // Asegura que termine en escala cero
        planePrefab.transform.localScale = Vector3.zero;
    }

    private void SpawnNewObject()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("No prefab assigned to spawn!");
            return;
        }

        ConfigureLeafSpawn();
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
        leafSpawner.grassCount = Mathf.Max((int)(targetScale.x * targetScale.y * 0.7f), 20);

        leafSpawner.Activate();
    }

    // Easing function para animación más suave
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}