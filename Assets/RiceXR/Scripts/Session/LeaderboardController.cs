using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LEADERBOARD
///
/// Lee todas las sesiones guardadas en el JSON y llena una lista UI.
///
/// Como configurar el prefab de fila (entryPrefab):
///   Pon TextMeshProUGUI con estos NOMBRES exactos en cualquier lugar de la jerarquia:
///     "NicknameText"   ? apodo del usuario
///     "DateText"       ? fecha/hora
///     "TotalTimeText"  ? tiempo total
///     "FailuresText"   ? total de fallos
///     "AvgTimeText"    ? tiempo promedio por nivel
///
///   El layout interno del prefab es libre (columnas, cards, etc.).
///   El script busca los campos por nombre, no por indice.
/// </summary>
public class LeaderboardController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Transform padre donde se instanciaran las filas (Content del ScrollView)")]
    [SerializeField] private Transform entryContainer;

    [Tooltip("Prefab de una fila del leaderboard")]
    [SerializeField] private GameObject entryPrefab;

    [Tooltip("Prefab que se instancia cuando no hay datos guardados (puede ser cualquier GameObject)")]
    [SerializeField] private GameObject emptyPrefab;

    private GameObject _emptyInstance;

    // Nombres de los GameObjects TMP dentro del prefab de fila
    private const string NICKNAME_OBJ  = "NicknameText";
    private const string DATE_OBJ      = "DateText";
    private const string TOTALTIME_OBJ = "TotalTimeText";
    private const string FAILURES_OBJ  = "FailuresText";
    private const string AVGTIME_OBJ   = "AvgTimeText";
    private const string NUMBER_OBJ    = "Number";

    /// <summary>Limpia la lista y la vuelve a llenar con los datos actuales del JSON.</summary>
    public void Populate()
    {
        ClearEntries();

        if (SessionDataSaver.Instance == null)
        {
            Debug.LogWarning("[Leaderboard] SessionDataSaver no encontrado.");
            ShowEmpty(true);
            return;
        }

        List<SessionMetricsTracker.SessionResult> sessions = SessionDataSaver.Instance.LoadAllSessions();

        if (sessions == null || sessions.Count == 0)
        {
            ShowEmpty(true);
            return;
        }

        ShowEmpty(false);

        // Ordenar por tiempo total ascendente (menor tiempo = mejor)
        sessions.Sort((a, b) => a.totalTimeSeconds.CompareTo(b.totalTimeSeconds));

        int rank = 1;
        foreach (var session in sessions){
            CreateRow(session, rank);
            rank++;
        }
    }


    private void CreateRow(SessionMetricsTracker.SessionResult session, int rank)
    {
        if (entryPrefab == null || entryContainer == null) return;

        GameObject row = Instantiate(entryPrefab, entryContainer);

        SetText(row, NUMBER_OBJ,    $"{rank}");
        SetText(row, NICKNAME_OBJ,  string.IsNullOrEmpty(session.nickname) ? ": " : session.nickname);
        SetText(row, DATE_OBJ,      session.dateTime);
        SetText(row, TOTALTIME_OBJ, FormatTime(session.totalTimeSeconds));
        SetText(row, FAILURES_OBJ,  session.totalFailures.ToString());
        SetText(row, AVGTIME_OBJ,   FormatTime(session.averageTimePerLevel));
    }

    /// <summary>
    /// Busca un hijo (a cualquier profundidad) con el nombre dado y le asigna el texto.
    /// Muestra warning si no lo encuentra, para facilitar la configuracion del prefab.
    /// </summary>
    private void SetText(GameObject root, string objectName, string value)
    {
        Transform found = FindDeep(root.transform, objectName);
        if (found == null)
        {
            Debug.LogWarning($"[Leaderboard] No se encontro '{objectName}' en el prefab de fila.");
            return;
        }

        TextMeshProUGUI tmp = found.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogWarning($"[Leaderboard] '{objectName}' no tiene TextMeshProUGUI.");
            return;
        }

        tmp.text = value;
    }

    /// <summary>Busqueda recursiva de un Transform por nombre.</summary>
    private Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void ClearEntries()
    {
        if (entryContainer == null) return;
        foreach (Transform child in entryContainer)
            Destroy(child.gameObject);

        // Destruir instancia del empty si existe
        if (_emptyInstance != null)
        {
            Destroy(_emptyInstance);
            _emptyInstance = null;
        }
    }

    private void ShowEmpty(bool show)
    {
        if (!show)
        {
            if (_emptyInstance != null)
            {
                Destroy(_emptyInstance);
                _emptyInstance = null;
            }
            return;
        }

        if (emptyPrefab == null || entryContainer == null) return;
        if (_emptyInstance != null) return;  // ya existe

        _emptyInstance = Instantiate(emptyPrefab, entryContainer);
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return m > 0 ? $"{m}m {s:00}s" : $"{s}s";
    }
}
