using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LEADERBOARD
/// 
/// Lee todas las sesiones guardadas en el JSON y llena una lista UI con:
///   - Apodo del usuario
///   - Fecha/hora
///   - Tiempo total
///   - Total de fallos
///   - Tiempo promedio por nivel
/// 
/// Cómo configurar en Unity:
///   1. Crear un ScrollView?  Content (objeto padre de las filas)
///   2. Crear un prefab de fila (LeaderboardRowPrefab) con varios TextMeshProUGUI
///      en este orden de hijos: Nickname | DateTime | TotalTime | Failures | AvgTime
///   3. Asignar entryContainer = el Content del ScrollView
///   4. Asignar entryPrefab    = el prefab de fila
/// </summary>
public class LeaderboardController : MonoBehaviour
{
  [Header("UI References")]
    [Tooltip("Transform padre donde se instanciarán las filas (Content del ScrollView)")]
    [SerializeField] private Transform entryContainer;

    [Tooltip("Prefab de una fila del leaderboard")]
    [SerializeField] private GameObject entryPrefab;

    [Tooltip("Texto que aparece cuando no hay datos guardados")]
    [SerializeField] private TextMeshProUGUI emptyLabel;

    // ?? API pública ???????????????????????????????????????????????????????

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

        foreach (var session in sessions)
  CreateRow(session);
    }

    // ?? Privados ??????????????????????????????????????????????????????????

    private void CreateRow(SessionMetricsTracker.SessionResult session)
    {
        if (entryPrefab == null || entryContainer == null) return;

      GameObject row = Instantiate(entryPrefab, entryContainer);

        // Obtiene todos los TMP en orden de jerarquía
        TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>(true);

        // Asigna textos según posición en el prefab
        // El prefab debe tener los textos en este orden:
  // [0] Nickname[1] Fecha  [2] Tiempo Total  [3] Fallos  [4] Promedio
        if (texts.Length > 0) texts[0].text = string.IsNullOrEmpty(session.nickname) ? "—" : session.nickname;
      if (texts.Length > 1) texts[1].text = session.dateTime;
 if (texts.Length > 2) texts[2].text = FormatTime(session.totalTimeSeconds);
   if (texts.Length > 3) texts[3].text = session.totalFailures.ToString();
      if (texts.Length > 4) texts[4].text = FormatTime(session.averageTimePerLevel);
    }

    private void ClearEntries()
    {
        if (entryContainer == null) return;
        foreach (Transform child in entryContainer)
          Destroy(child.gameObject);
    }

    private void ShowEmpty(bool show)
    {
        if (emptyLabel != null)
     emptyLabel.gameObject.SetActive(show);
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return m > 0 ? $"{m}m {s:00}s" : $"{s}s";
    }
}
