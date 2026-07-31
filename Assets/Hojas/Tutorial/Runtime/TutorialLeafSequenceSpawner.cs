using System;
using UnityEngine;

/// <summary>Spawns precisely one configured guided leaf and one deterministic practice leaf.</summary>
[DisallowMultipleComponent]
public class TutorialLeafSequenceSpawner : MonoBehaviour
{
    [Header("Optional explicit prefabs; otherwise the first two LeavesSpawner prefabs are used")]
    [SerializeField] private GameObject guidedLeafPrefab;
    [SerializeField] private GameObject practiceLeafPrefab;
    [SerializeField] private Transform leafParent;
    [SerializeField] private Vector3 guidedLocalPosition = new Vector3(-.18f, 0f, 0f);
    [SerializeField] private Vector3 practiceLocalPosition = new Vector3(.18f, 0f, 0f);

    public Leaf CurrentLeaf { get; private set; }
    public Leaf GuidedLeaf { get; private set; }
    public Leaf PracticeLeaf { get; private set; }
    public bool PracticeSpawned => PracticeLeaf != null;
    public DiseaseSpot GuidedTarget => GuidedLeaf != null && GuidedLeaf.diseaseSpots != null && GuidedLeaf.diseaseSpots.Count > 0
        ? GuidedLeaf.diseaseSpots[0] : null;
    public event Action<Leaf> GuidedLeafSpawned;
    public event Action<Leaf> PracticeLeafSpawned;

    public void BeginSequence()
    {
        ClearSequence();
        ResolveFallbackPrefabs();
        GuidedLeaf = Spawn(guidedLeafPrefab, guidedLocalPosition, hideMarkers: true);
        CurrentLeaf = GuidedLeaf;
        if (GuidedLeaf == null) Debug.LogError("[TutorialSequence] No se pudo crear la hoja guiada. Configure un prefab de hoja.");
        else GuidedLeafSpawned?.Invoke(GuidedLeaf);
    }

    public Leaf SpawnPracticeLeaf()
    {
        if (PracticeLeaf != null) return PracticeLeaf;
        ResolveFallbackPrefabs();
        PracticeLeaf = Spawn(practiceLeafPrefab, practiceLocalPosition, hideMarkers: false);
        CurrentLeaf = PracticeLeaf;
        if (PracticeLeaf == null) Debug.LogError("[TutorialSequence] No se pudo crear la hoja de práctica.");
        else PracticeLeafSpawned?.Invoke(PracticeLeaf);
        return PracticeLeaf;
    }

    public void ClearSequence()
    {
        if (GuidedLeaf != null) Destroy(GuidedLeaf.gameObject);
        if (PracticeLeaf != null) Destroy(PracticeLeaf.gameObject);
        GuidedLeaf = PracticeLeaf = CurrentLeaf = null;
    }

    private Leaf Spawn(GameObject prefab, Vector3 localPosition, bool hideMarkers)
    {
        if (prefab == null) return null;
        Transform parent = leafParent != null ? leafParent : transform;
        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.identity;
        Leaf leaf = instance.GetComponentInChildren<Leaf>(true);
        if (leaf == null)
        {
            Debug.LogError($"[TutorialSequence] '{prefab.name}' no contiene Leaf.");
            Destroy(instance);
            return null;
        }
        leaf.ableToShowMarkers = !hideMarkers;
        leaf.showMarkers = !hideMarkers;
        leaf.SetMarkersVisibility(!hideMarkers);
        return leaf;
    }

    private void ResolveFallbackPrefabs()
    {
        LeavesSpawner source = GetComponent<LeavesSpawner>();
        if (source == null) source = GetComponentInChildren<LeavesSpawner>(true);
        if (source == null) return;
        if (guidedLeafPrefab == null) guidedLeafPrefab = source.GetDeterministicPrefab(0);
        if (practiceLeafPrefab == null) practiceLeafPrefab = source.GetDeterministicPrefab(1) ?? guidedLeafPrefab;
    }

    private void OnDestroy() => ClearSequence();
}
