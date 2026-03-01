using System.Collections.Generic;
using System.IO;
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
/// </summary>
public class SessionDataSaver : MonoBehaviour
{
    public static SessionDataSaver Instance { get; private set; }

    private const string FILE_NAME = "sessions.json";

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

    // ?? Ruta del archivo ?????????????????????????????????????????????????
    private string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    // ?? Guardar sesión ???????????????????????????????????????????????????

    /// <summary>
    /// Añade una sesión al JSON. Si el archivo no existe lo crea.
    /// Devuelve true si se guardó correctamente.
    /// </summary>
    public bool SaveSession(SessionMetricsTracker.SessionResult result)
    {
        try
{
          SessionsFile file = LoadFile();
 file.sessions.Add(result);

  string json = JsonUtility.ToJson(file, prettyPrint: true);
            File.WriteAllText(FilePath, json);

Debug.Log($"[Saver] Sesión guardada en: {FilePath}");
    return true;
  }
        catch (System.Exception e)
        {
    Debug.LogError($"[Saver] Error al guardar sesión: {e.Message}");
        return false;
        }
    }

    // ?? Cargar todas las sesiones ????????????????????????????????????????

    /// <summary>
    /// Carga y devuelve todas las sesiones guardadas.
    /// Devuelve lista vacía si el archivo no existe o está corrupto.
    /// </summary>
    public List<SessionMetricsTracker.SessionResult> LoadAllSessions()
    {
        return LoadFile().sessions;
    }

    // ?? Privados ?????????????????????????????????????????????????????????

    private SessionsFile LoadFile()
    {
        if (!File.Exists(FilePath))
       return new SessionsFile();

        try
 {
      string json = File.ReadAllText(FilePath);
 return JsonUtility.FromJson<SessionsFile>(json) ?? new SessionsFile();
        }
  catch (System.Exception e)
        {
            Debug.LogWarning($"[Saver] No se pudo leer el archivo, se creará uno nuevo. Error: {e.Message}");
        return new SessionsFile();
        }
    }

    // ?? Utilidad para editor / debugging ????????????????????????????????

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
}
