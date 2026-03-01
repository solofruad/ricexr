using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SISTEMA DE MÉTRICAS DE SESIÓN
/// 
/// Singleton que registra en tiempo real todas las métricas de la sesión actual:
/// - Tiempo que tarda el usuario en cada prueba (nivel)
/// - Cuántas veces falló en cada prueba
/// - Tiempo total de la sesión completa
/// - Qué enfermedad y severidad escogió en cada intento
/// 
/// Cómo usarlo:
/// - Al iniciar una prueba: llamar StartLevel(levelIndex)
/// - Al fallar un intento:  llamar RegisterFailedAttempt(levelIndex, disease, severity)
/// - Al completar un nivel: llamar CompleteLevel(levelIndex)
/// - Al terminar todo:      llamar EndSession()  ?  devuelve SessionResult listo para guardar
/// </summary>
public class SessionMetricsTracker : MonoBehaviour
{
    public static SessionMetricsTracker Instance { get; private set; }

    // ?? Datos por nivel ??????????????????????????????????????????????????
    [System.Serializable]
    public class LevelAttempt
    {
        public string selectedDisease;
    public int selectedSeverity;
      public bool wasCorrect;
    }

    [System.Serializable]
    public class LevelMetrics
    {
        public int levelIndex;
        public float timeToComplete;        // segundos desde StartLevel ? CompleteLevel
 public int failCount;       // veces que respondió mal antes de acertar
        public List<LevelAttempt> attempts = new List<LevelAttempt>();
    }

    // ?? Resultado final de la sesión (listo para serializar a JSON) ??????
    [System.Serializable]
    public class SessionResult
    {
     public string nickname;
        public string dateTime;
        public float totalTimeSeconds;
        public int totalLevels;
        public int totalFailures;
        public float averageTimePerLevel;
        public List<LevelMetrics> levels = new List<LevelMetrics>();
    }

    // ?? Estado interno ???????????????????????????????????????????????????
    private float _sessionStartTime;
    private float _levelStartTime;
    private int   _currentLevel = -1;

    private List<LevelMetrics> _levelMetrics = new List<LevelMetrics>();
    private LevelMetrics _activeLevelMetrics;

    // ?? Lifecycle ????????????????????????????????????????????????????????
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

    // ?? API pública ???????????????????????????????????????????????????????

    /// <summary>Llama esto cuando comienza la sesión completa (botón "Iniciar pruebas").</summary>
    public void StartSession()
    {
_sessionStartTime = Time.time;
     _levelMetrics.Clear();
        _currentLevel = -1;
        Debug.Log("[Metrics] Sesión iniciada.");
    }

    /// <summary>Llama esto cuando arranca un nuevo nivel/prueba.</summary>
    public void StartLevel(int levelIndex)
    {
        _currentLevel     = levelIndex;
        _levelStartTime   = Time.time;
      _activeLevelMetrics = new LevelMetrics
        {
            levelIndex = levelIndex,
            failCount= 0
        };
        Debug.Log($"[Metrics] Nivel {levelIndex} iniciado.");
    }

    /// <summary>Llama esto cada vez que el usuario responde INCORRECTAMENTE.</summary>
    public void RegisterFailedAttempt(int levelIndex, string selectedDisease, int selectedSeverity)
    {
        if (_activeLevelMetrics == null || _activeLevelMetrics.levelIndex != levelIndex) return;

 _activeLevelMetrics.failCount++;
        _activeLevelMetrics.attempts.Add(new LevelAttempt
        {
selectedDisease  = selectedDisease,
            selectedSeverity = selectedSeverity,
      wasCorrect       = false
        });
    Debug.Log($"[Metrics] Fallo en nivel {levelIndex}. Total fallos: {_activeLevelMetrics.failCount}");
    }

    /// <summary>Llama esto cuando el usuario responde CORRECTAMENTE y se pasa de nivel.</summary>
    public void CompleteLevel(int levelIndex, string correctDisease, int correctSeverity)
    {
        if (_activeLevelMetrics == null || _activeLevelMetrics.levelIndex != levelIndex) return;

        _activeLevelMetrics.timeToComplete = Time.time - _levelStartTime;
        _activeLevelMetrics.attempts.Add(new LevelAttempt
        {
            selectedDisease  = correctDisease,
    selectedSeverity = correctSeverity,
            wasCorrect  = true
        });

    float completedTime = _activeLevelMetrics.timeToComplete; // guardar antes de null
  _levelMetrics.Add(_activeLevelMetrics);
        _activeLevelMetrics = null;

     Debug.Log($"[Metrics] Nivel {levelIndex} completado en {completedTime:F1}s");
    }

    /// <summary>
    /// Llama esto cuando el usuario termina TODAS las pruebas.
    /// Devuelve un SessionResult listo para mostrar en UI y guardar en JSON.
    /// </summary>
    public SessionResult EndSession(string nickname = "")
    {
float totalTime    = Time.time - _sessionStartTime;
        int   totalFails   = 0;
      float totalLvlTime = 0f;

        foreach (var lm in _levelMetrics)
        {
     totalFails   += lm.failCount;
       totalLvlTime += lm.timeToComplete;
        }

        float avgTime = _levelMetrics.Count > 0 ? totalLvlTime / _levelMetrics.Count : 0f;

    var result = new SessionResult
        {
            nickname     = nickname,
       dateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
     totalTimeSeconds     = totalTime,
       totalLevels          = _levelMetrics.Count,
  totalFailures        = totalFails,
    averageTimePerLevel  = avgTime,
            levels     = new List<LevelMetrics>(_levelMetrics)
        };

   Debug.Log($"[Metrics] Sesión finalizada. Tiempo total: {totalTime:F1}s, Fallos: {totalFails}");
    return result;
 }

    // ?? Getters para mostrar en la UI de resultados ???????????????????????

    public float GetCurrentSessionTime()   => Time.time - _sessionStartTime;
    public int   GetTotalFailuresSoFar()
    {
        int t = 0;
        foreach (var lm in _levelMetrics) t += lm.failCount;
        if (_activeLevelMetrics != null)   t += _activeLevelMetrics.failCount;
        return t;
    }
    public int   GetCompletedLevelCount()  => _levelMetrics.Count;
    public List<LevelMetrics> GetLevelMetrics() => _levelMetrics;
}
