using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NarrationLineEntry
{
    public string id;

    [TextArea(2, 8)]
    public string text;

    public bool enabled = true;
    public float cooldownSec = 0f;
    public bool oncePerSession;
    public bool oncePerLevel;
    public bool firstErrorOnly;
}

[CreateAssetMenu(fileName = "NarrationLineLibrary", menuName = "Hojas/Narration/Line Library")]
public class NarrationLineLibrary : ScriptableObject
{
    [SerializeField] private List<NarrationLineEntry> lines = new List<NarrationLineEntry>();

    private readonly Dictionary<string, NarrationLineEntry> _lineById = new Dictionary<string, NarrationLineEntry>();

    public IReadOnlyList<NarrationLineEntry> Lines => lines;

    public bool TryGetLine(string id, out NarrationLineEntry line)
    {
        line = null;
        if (string.IsNullOrWhiteSpace(id)) return false;

        RebuildIndexIfNeeded();
        return _lineById.TryGetValue(id, out line);
    }

    [ContextMenu("Load Default Offline Lines")]
    public void LoadDefaultOfflineLines()
    {
        lines = GameNarrationDefaults.CreateDefaultEntries();
        ForceRebuildIndex();
    }

    public void ForceRebuildIndex()
    {
        _lineById.Clear();
        RebuildIndexIfNeeded();
    }

    private void RebuildIndexIfNeeded()
    {
        if (_lineById.Count > 0) return;

        if (lines == null) return;

        for (int i = 0; i < lines.Count; i++)
        {
            NarrationLineEntry candidate = lines[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.id)) continue;

            if (!_lineById.ContainsKey(candidate.id))
            {
                _lineById.Add(candidate.id, candidate);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _lineById.Clear();
    }
#endif
}
