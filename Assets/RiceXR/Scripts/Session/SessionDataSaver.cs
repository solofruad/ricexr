using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// GUARDADO Y CARGA DE DATOS DE SESIÓN EN JSON
///
/// Gestiona la persistencia de los resultados de todas las sesiones en un archivo JSON
/// ubicado en Application.persistentDataPath/sessions.json
///
/// Cada vez que una sesión termina, sus datos se AÑADEN al archivo existente
/// (no lo sobreescribe), permitiendo acumular datos de múltiples personas.
///
/// El archivo tiene la estructura:
/// {
///   "sessions": [ { sesión1 }, { sesión2 }, ... ]
/// }
///
/// Además del JSON, puede exportar un resumen en CSV (sessions.csv) para facilitar
/// la recolección de datos de la fase experimental vía `adb pull`.
/// </summary>
public class SessionDataSaver : MonoBehaviour
{
    public static SessionDataSaver Instance { get; private set; }

    private const string FILE_NAME = "sessions.json";
    private const string CSV_FILE_NAME = "sessions.csv";

    // Wrapper para serializar la lista completa
    [System.Serializable]
    private class SessionsFile
    {
        public List<SessionMetricsTracker.SessionResult> sessions = new List<SessionMetricsTracker.SessionResult>();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ── Rutas de archivo ─────────────────────────────────────────────────
    private string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);
    private string CsvFilePath => Path.Combine(Application.persistentDataPath, CSV_FILE_NAME);

    // ── Guardar sesión ───────────────────────────────────────────────────

    /// <summary>
    /// Añade una sesión al JSON. Si el archivo no existe lo crea.
    /// Devuelve true si se guardó correctamente.
    /// Mantiene además un CSV de resumen sincronizado.
    /// </summary>
    public bool SaveSession(SessionMetricsTracker.SessionResult result)
    {
        try
        {
            SessionsFile file = LoadFile();
            file.sessions.Add(result);

            string json = JsonUtility.ToJson(file, prettyPrint: true);
            File.WriteAllText(FilePath, json, Encoding.UTF8);

            Debug.Log($"[Saver] Sesión guardada en: {FilePath}");

            // Mantener el CSV de resumen al día tras cada guardado correcto.
            WriteCsv(file.sessions);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Saver] Error al guardar sesión: {e.Message}");
            return false;
        }
    }

    // ── Cargar todas las sesiones ────────────────────────────────────────

    /// <summary>
    /// Carga y devuelve todas las sesiones guardadas.
    /// Devuelve lista vacía si el archivo no existe o está corrupto.
    /// </summary>
    public List<SessionMetricsTracker.SessionResult> LoadAllSessions()
    {
        return LoadFile().sessions;
    }

    // ── Exportar a CSV ───────────────────────────────────────────────────

    /// <summary>
    /// Exporta un resumen de todas las sesiones a Application.persistentDataPath/sessions.csv.
    /// Una fila por sesión con las métricas agregadas, lista para análisis (adb pull).
    /// </summary>
    [ContextMenu("Exportar sesiones a CSV")]
    public void ExportSessionsCsv()
    {
        WriteCsv(LoadAllSessions());
        Debug.Log($"[Saver] Sesiones exportadas a CSV en: {CsvFilePath}");
    }

    private void WriteCsv(List<SessionMetricsTracker.SessionResult> sessions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("nickname,dateTime,totalTimeSeconds,totalLevels,totalFailures,averageTimePerLevel");

        foreach (var s in sessions)
        {
            sb.Append(EscapeCsv(s.nickname)).Append(',');
            sb.Append(EscapeCsv(s.dateTime)).Append(',');
            sb.Append(s.totalTimeSeconds.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.totalLevels.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.totalFailures.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.averageTimePerLevel.ToString("F2", CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        File.WriteAllText(CsvFilePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Escapa un campo para CSV (comillas, comas y saltos de línea).</summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    // ── Privados ─────────────────────────────────────────────────────────

    private SessionsFile LoadFile()
    {
        if (!File.Exists(FilePath))
            return new SessionsFile();

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            return JsonUtility.FromJson<SessionsFile>(json) ?? new SessionsFile();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Saver] No se pudo leer el archivo, se creará uno nuevo. Error: {e.Message}");
            return new SessionsFile();
        }
    }

    // ── Utilidad para editor / debugging ─────────────────────────────────

    /// <summary>Elimina el archivo JSON (solo para tests).</summary>
    public void DeleteSaveFile()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
            Debug.Log("[Saver] Archivo de sesiones eliminado.");
        }
    }

    public string GetFilePath() => FilePath;
    public string GetCsvFilePath() => CsvFilePath;
}
