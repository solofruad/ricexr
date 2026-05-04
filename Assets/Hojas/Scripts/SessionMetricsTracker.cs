using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SISTEMA DE METRICAS DE SESION
///
/// Singleton que registra en tiempo real todas las metricas de la sesion actual:
/// - Tiempo que tarda el usuario en cada prueba (nivel)
/// - Cuantas veces fallo en cada prueba
/// - Tiempo total de la sesion completa
/// - Que enfermedad y severidad escogio en cada intento
///
/// Como usarlo:
/// - Al iniciar sesion: StartSession()
/// - Durante la sesion: escucha GameEventBus automaticamente
/// - Al terminar todo: EndSession()  →  devuelve SessionResult listo para guardar
/// </summary>
public class SessionMetricsTracker : MonoBehaviour
{
    public static SessionMetricsTracker Instance { get; private set; }

    // ── Datos por nivel ──────────────────────────────────────────────────────
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
        public float timeToComplete;    // segundos desde StartLevel → CompleteLevel
        public int failCount;           // veces que respondio mal antes de acertar
        public List<LevelAttempt> attempts = new List<LevelAttempt>();
    }

    // ── Resultado final de la sesion (listo para serializar a JSON) ──────────
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

    // ── Estado interno ───────────────────────────────────────────────────────
    private float _sessionStartTime;
    private float _levelStartTime;
    private int _currentLevel = -1;
    private readonly List<LevelMetrics> _levelMetrics = new List<LevelMetrics>();
    private LevelMetrics _activeLevelMetrics;
    private bool _sessionRunning = false;

    // ── Lifecycle ────────────────────────────────────────────────────────────
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

    private void OnEnable()
    {
        GameEventBus.OnLevelStarted += HandleLevelStarted;
        GameEventBus.OnDiagnosisAttemptEvaluated += HandleDiagnosisAttemptEvaluated;
        GameEventBus.OnLevelCompleted += HandleLevelCompleted;
        GameEventBus.OnSessionEndRequested += HandleSessionEndRequested;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStarted -= HandleLevelStarted;
        GameEventBus.OnDiagnosisAttemptEvaluated -= HandleDiagnosisAttemptEvaluated;
        GameEventBus.OnLevelCompleted -= HandleLevelCompleted;
        GameEventBus.OnSessionEndRequested -= HandleSessionEndRequested;
    }

    // ── API publica ──────────────────────────────────────────────────────────

    /// <summary>Llama esto cuando comienza la sesion completa (boton "Iniciar pruebas").</summary>
    public void StartSession()
    {
        _sessionStartTime = Time.time;
        _levelMetrics.Clear();
        _currentLevel = -1;
        _activeLevelMetrics = null;
        _sessionRunning = true;
        Debug.Log("[Metrics] Sesion iniciada.");
    }

    /// <summary>
    /// Llama esto cuando el usuario termina TODAS las pruebas.
    /// Devuelve un SessionResult listo para mostrar en UI y guardar en JSON.
    /// </summary>
    public SessionResult EndSession(string nickname = "")
    {
        if (!_sessionRunning)
        {
            return BuildResult(nickname, 0f);
        }

        float totalTime = Time.time - _sessionStartTime;
        _sessionRunning = false;

        var result = BuildResult(nickname, totalTime);
        Debug.Log($"[Metrics] Sesion finalizada. Tiempo total: {totalTime:F1}s, Fallos: {result.totalFailures}");
        return result;
    }

    // ── Handlers de eventos ──────────────────────────────────────────────────

    private void HandleLevelStarted(int levelIndex, int totalLevels, int plantsRequired)
    {
        if (!_sessionRunning) return;

        _currentLevel = levelIndex;
        _levelStartTime = Time.time;
        _activeLevelMetrics = new LevelMetrics
        {
            levelIndex = levelIndex,
            failCount = 0
        };

        Debug.Log($"[Metrics] Nivel {levelIndex} iniciado. Requeridas: {plantsRequired}");
    }

    private void HandleDiagnosisAttemptEvaluated(string selectedDisease, int selectedSeverity, bool isCorrect)
    {
        if (!_sessionRunning || _activeLevelMetrics == null) return;

        _activeLevelMetrics.attempts.Add(new LevelAttempt
        {
            selectedDisease = selectedDisease,
            selectedSeverity = selectedSeverity,
            wasCorrect = isCorrect
        });

        if (!isCorrect)
        {
            _activeLevelMetrics.failCount++;
            Debug.Log($"[Metrics] Fallo en nivel {_activeLevelMetrics.levelIndex}. Total fallos: {_activeLevelMetrics.failCount}");
        }
    }

    private void HandleLevelCompleted(int levelIndex)
    {
        if (!_sessionRunning || _activeLevelMetrics == null) return;
        if (_activeLevelMetrics.levelIndex != levelIndex) return;

        _activeLevelMetrics.timeToComplete = Time.time - _levelStartTime;
        float completedTime = _activeLevelMetrics.timeToComplete;

        _levelMetrics.Add(_activeLevelMetrics);
        _activeLevelMetrics = null;

        Debug.Log($"[Metrics] Nivel {levelIndex} completado en {completedTime:F1}s");
    }

    private void HandleSessionEndRequested()
    {
        // Auto-end session cuando se solicita desde el bus
        // EndSessionController llama EndSession directamente con el nickname,
        // pero este handler asegura que la sesión se cierre si nadie más lo hace.
    }

    // ── Getters para mostrar en la UI de resultados ──────────────────────────
    public float GetCurrentSessionTime() => _sessionRunning ? Time.time - _sessionStartTime : 0f;

    public int GetTotalFailuresSoFar()
    {
        int t = 0;
        foreach (var lm in _levelMetrics) t += lm.failCount;
        if (_activeLevelMetrics != null) t += _activeLevelMetrics.failCount;
        return t;
    }

    public int GetCompletedLevelCount() => _levelMetrics.Count;
    public List<LevelMetrics> GetLevelMetrics() => _levelMetrics;

    private SessionResult BuildResult(string nickname, float totalTime)
    {
        int totalFails = 0;
        float totalLvlTime = 0f;

        foreach (var lm in _levelMetrics)
        {
            totalFails += lm.failCount;
            totalLvlTime += lm.timeToComplete;
        }

        float avgTime = _levelMetrics.Count > 0 ? totalLvlTime / _levelMetrics.Count : 0f;

        return new SessionResult
        {
            nickname = nickname,
            dateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            totalTimeSeconds = totalTime,
            totalLevels = _levelMetrics.Count,
            totalFailures = totalFails,
            averageTimePerLevel = avgTime,
            levels = new List<LevelMetrics>(_levelMetrics)
        };
    }
}
