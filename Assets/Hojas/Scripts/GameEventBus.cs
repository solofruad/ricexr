using System;
using UnityEngine;

public enum FlowState
{
    None = 0,
    Idle = 1,
    WaitingForPlaneSelection = 2,
    DisablingPlanes = 3,
    LevelIntro = 4,
    Tutorial = 5,
    LevelStarting = 6,
    WaitingForDiseaseAnalysis = 7,
    LevelPlaying = 8,
    WaitingForLevelCompletion = 9,
    LevelTransition = 10,
    AllLevelsCompleted = 11,
    SessionEnding = 12,
    SessionIntro = 13
}

/// <summary>
/// Bus de eventos global del juego.
/// 
/// Ningún sistema conoce a otro directamente para notificar cambios de estado.
/// En su lugar, publican un evento aquí y quien necesite lo escucha.
/// Esto permite agregar o quitar listeners (UI, analytics, audio, etc.)
/// sin tocar la lógica del juego.
///
/// USO — Publicar:
///   GameEventBus.PublishPlantSelected(isCorrect, selected, required);
///
/// USO — Escuchar (en OnEnable/OnDisable):
///   GameEventBus.OnPlantSelected += HandlePlantSelected;
///   GameEventBus.OnPlantSelected -= HandlePlantSelected;
/// </summary>
public static class GameEventBus
{
    // ── Sesión / Flujo (NUEVOS) ─────────────────────────────────────────────

    /// <summary>
    /// Emitido cuando el usuario presiona un boton de inicio en el menu principal.
    /// skipPlaneSelection indica si se debe reutilizar el plano MR de la sesion anterior.
    /// </summary>
    public static event Action<bool> OnSessionStartRequested;

    /// <summary>Emitido cuando arranca la introducción que precede a la selección de plano.</summary>
    public static event Action OnSessionIntroStarted;

    /// <summary>
    /// Emitido cada vez que un panel de la introducción aparece en pantalla.
    /// Lleva el id de línea de narración que le corresponde, para que la narración
    /// pueda reaccionar sin que la intro conozca clips ni reglas de reproducción.
    /// </summary>
    public static event Action<string> OnSessionIntroPanelShown;

    /// <summary>Emitido cuando la introducción termina (por el botón o porque se omitió).</summary>
    public static event Action OnSessionIntroCompleted;

    /// <summary>Emitido cuando el usuario selecciona un plano MR. Incluye posición, rotación y escala.</summary>
    public static event Action<Vector3, Quaternion, Vector3> OnPlaneSelected;

    /// <summary>Emitido cuando los planos de MR terminan de ocultarse.</summary>
    public static event Action OnPlanesHidden;

    /// <summary>Emitido cuando inicia la intro del nivel actual.</summary>
    public static event Action OnLevelIntroStarted;

    /// <summary>Emitido cuando la intro del nivel actual se completa.</summary>
    public static event Action OnLevelIntroCompleted;

    /// <summary>Emitido cuando el tutorial inicia (panel visible y gameplay listo).</summary>
    public static event Action OnTutorialStarted;

    /// <summary>Emitido cuando el tutorial se completa (todas las hojas identificadas).</summary>
    public static event Action OnTutorialCompleted;

    /// <summary>Emitido cuando un nivel fue spawneado.</summary>
    public static event Action<int> OnLevelSpawned;

    /// <summary>Emitido cuando se solicita volver al menú principal.</summary>
    public static event Action OnReturnToMenuRequested;

    // ── Nivel ────────────────────────────────────────────────────────────────

    /// <summary>Emitido cuando un nivel comienza. Incluye índice (0-based), total y plantas requeridas.</summary>
    public static event Action<int, int, int> OnLevelStarted;

    /// <summary>Emitido cuando el jugador completa un nivel correctamente.</summary>
    public static event Action<int> OnLevelCompleted;

    /// <summary>Emitido cuando todos los niveles han sido completados.</summary>
    public static event Action OnAllLevelsCompleted;

    // ── Selección de plantas ─────────────────────────────────────────────────

    /// <summary>
    /// Emitido cuando el jugador selecciona una planta.
    /// isCorrect: si la planta era la enferma.
    /// plantsSelected: cuántas correctas lleva.
    /// plantsRequired: cuántas necesita en este nivel.
    /// </summary>
    public static event Action<bool, int, int> OnPlantSelected;

    /// <summary>Emitido cuando se procesa un intento de diagnóstico (correcto o incorrecto).</summary>
    public static event Action<string, int, bool> OnDiagnosisAttemptEvaluated;

    /// <summary>Emitido cuando el jugador completa la selección de todas las plantas del nivel.</summary>
    public static event Action OnAllPlantsSelected;

    // ── Presentación de la enfermedad ────────────────────────────────────────

    /// <summary>
    /// Emitido cada vez que aparece uno de los 3 paneles que presentan la
    /// enfermedad. Lleva el id de línea de narración del panel, para que la
    /// narración pueda reaccionar sin que la presentación conozca clips ni
    /// reglas de reproducción. Igual que OnSessionIntroPanelShown.
    /// </summary>
    public static event Action<string> OnDiseaseIntroPanelShown;

    /// <summary>
    /// Emitido cuando lo que GameFlowController estaba esperando ya terminó, y
    /// puede seguir. Tiene dos usos según qué esté esperando:
    ///   - Tras presentar la enfermedad → spawnea el nivel (las hojas crecen)
    ///   - Tras el mensaje de felicitación → avanza al siguiente nivel
    /// GameFlowController distingue con sus flags internos.
    /// </summary>
    public static event Action OnDiseaseAnalysisCompleted;

    // ── Sesión ───────────────────────────────────────────────────────────────

    /// <summary>Emitido cuando la sesión completa termina.</summary>
    public static event Action OnSessionEnded;

    /// <summary>Emitido cuando cambia el estado del flujo principal.</summary>
    public static event Action<FlowState, FlowState, string> OnFlowStateChanged;

    // ── End Session UI ───────────────────────────────────────────────────────

    /// <summary>Emitido cuando el panel de fin de sesión se muestra.</summary>
    public static event Action OnEndPanelShown;

    /// <summary>Emitido cuando se completa el guardado (true = ok, false = error).</summary>
    public static event Action<bool> OnSaveCompleted;

    /// <summary>Emitido cuando se está regresando al menú.</summary>
    public static event Action OnReturningToMenu;

    // ════════════════════════════════════════════════════════════════════════
    // Métodos de publicación — llamar estos, nunca invocar los eventos directo
    // ════════════════════════════════════════════════════════════════════════

    // ── Sesión / Flujo ───────────────────────────────────────────────────────

    public static void PublishSessionStartRequested(bool skipPlaneSelection = false)
    {
        Debug.Log($"[GameEventBus] SessionStartRequested | Omitir plano: {skipPlaneSelection}");
        OnSessionStartRequested?.Invoke(skipPlaneSelection);
    }

    public static void PublishSessionIntroStarted()
    {
        Debug.Log("[GameEventBus] SessionIntroStarted");
        OnSessionIntroStarted?.Invoke();
    }

    public static void PublishSessionIntroPanelShown(string narrationLineId)
    {
        Debug.Log($"[GameEventBus] SessionIntroPanelShown → {narrationLineId}");
        OnSessionIntroPanelShown?.Invoke(narrationLineId);
    }

    public static void PublishSessionIntroCompleted()
    {
        Debug.Log("[GameEventBus] SessionIntroCompleted");
        OnSessionIntroCompleted?.Invoke();
    }

    public static void PublishPlaneSelected(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Debug.Log($"[GameEventBus] PlaneSelected → pos:{position}");
        OnPlaneSelected?.Invoke(position, rotation, scale);
    }

    public static void PublishPlanesHidden()
    {
        Debug.Log("[GameEventBus] PlanesHidden");
        OnPlanesHidden?.Invoke();
    }

    public static void PublishLevelIntroStarted()
    {
        Debug.Log("[GameEventBus] LevelIntroStarted");
        OnLevelIntroStarted?.Invoke();
    }

    public static void PublishLevelIntroCompleted()
    {
        Debug.Log("[GameEventBus] LevelIntroCompleted");
        OnLevelIntroCompleted?.Invoke();
    }

    public static void PublishTutorialStarted()
    {
        Debug.Log("[GameEventBus] TutorialStarted");
        OnTutorialStarted?.Invoke();
    }

    public static void PublishTutorialCompleted()
    {
        Debug.Log("[GameEventBus] TutorialCompleted");
        OnTutorialCompleted?.Invoke();
    }

    public static void PublishLevelSpawned(int levelIndex)
    {
        Debug.Log($"[GameEventBus] LevelSpawned → {levelIndex}");
        OnLevelSpawned?.Invoke(levelIndex);
    }

    public static void PublishReturnToMenuRequested()
    {
        Debug.Log("[GameEventBus] ReturnToMenuRequested");
        OnReturnToMenuRequested?.Invoke();
    }

    // ── End Session UI ───────────────────────────────────────────────────────

    public static void PublishEndPanelShown()
    {
        Debug.Log("[GameEventBus] EndPanelShown");
        OnEndPanelShown?.Invoke();
    }

    public static void PublishSaveCompleted(bool success)
    {
        Debug.Log($"[GameEventBus] SaveCompleted → {success}");
        OnSaveCompleted?.Invoke(success);
    }

    public static void PublishReturningToMenu()
    {
        Debug.Log("[GameEventBus] ReturningToMenu");
        OnReturningToMenu?.Invoke();
    }

    // ── Nivel ────────────────────────────────────────────────────────────────

    public static void PublishLevelStarted(int levelIndex, int totalLevels, int plantsRequired)
    {
        Debug.Log($"[GameEventBus] LevelStarted → {levelIndex + 1}/{totalLevels} | Plantas: {plantsRequired}");
        OnLevelStarted?.Invoke(levelIndex, totalLevels, plantsRequired);
    }

    public static void PublishLevelCompleted(int levelIndex)
    {
        Debug.Log($"[GameEventBus] LevelCompleted → {levelIndex + 1}");
        OnLevelCompleted?.Invoke(levelIndex);
    }

    public static void PublishAllLevelsCompleted()
    {
        Debug.Log("[GameEventBus] AllLevelsCompleted");
        OnAllLevelsCompleted?.Invoke();
    }

    public static void PublishPlantSelected(bool isCorrect, int plantsSelected, int plantsRequired)
    {
        Debug.Log($"[GameEventBus] PlantSelected → correct:{isCorrect} {plantsSelected}/{plantsRequired}");
        OnPlantSelected?.Invoke(isCorrect, plantsSelected, plantsRequired);
    }

    public static void PublishDiagnosisAttemptEvaluated(string selectedDisease, int selectedSeverity, bool isCorrect)
    {
        Debug.Log($"[GameEventBus] DiagnosisAttempt → {selectedDisease} Sev:{selectedSeverity} Correct:{isCorrect}");
        OnDiagnosisAttemptEvaluated?.Invoke(selectedDisease, selectedSeverity, isCorrect);
    }

    public static void PublishAllPlantsSelected()
    {
        Debug.Log("[GameEventBus] AllPlantsSelected");
        OnAllPlantsSelected?.Invoke();
    }

    public static void PublishDiseaseIntroPanelShown(string narrationLineId)
    {
        Debug.Log($"[GameEventBus] DiseaseIntroPanelShown → {narrationLineId}");
        OnDiseaseIntroPanelShown?.Invoke(narrationLineId);
    }

    public static void PublishDiseaseAnalysisCompleted()
    {
        Debug.Log("[GameEventBus] DiseaseAnalysisCompleted");
        OnDiseaseAnalysisCompleted?.Invoke();
    }

    public static void PublishSessionEnded()
    {
        Debug.Log("[GameEventBus] SessionEnded");
        OnSessionEnded?.Invoke();
    }

    public static void PublishFlowStateChanged(FlowState previousState, FlowState nextState, string reason)
    {
        string displayReason = string.IsNullOrEmpty(reason) ? "unspecified" : reason;
        Debug.Log($"[GameEventBus] FlowStateChanged → {previousState} -> {nextState} ({displayReason})");
        OnFlowStateChanged?.Invoke(previousState, nextState, displayReason);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Limpieza — llamar en escenas que se descargan para evitar memory leaks
    // ════════════════════════════════════════════════════════════════════════

    public static void ClearAllListeners()
    {
        // Sesión / Flujo
        OnSessionStartRequested = null;
        OnSessionIntroStarted = null;
        OnSessionIntroPanelShown = null;
        OnSessionIntroCompleted = null;
        OnPlaneSelected = null;
        OnPlanesHidden = null;
        OnLevelIntroStarted = null;
        OnLevelIntroCompleted = null;
        OnTutorialStarted = null;
        OnTutorialCompleted = null;
        OnLevelSpawned = null;
        OnReturnToMenuRequested = null;

        // End Session UI
        OnEndPanelShown = null;
        OnSaveCompleted = null;
        OnReturningToMenu = null;

        // Nivel
        OnLevelStarted = null;
        OnLevelCompleted = null;
        OnAllLevelsCompleted = null;
        OnPlantSelected = null;
        OnDiagnosisAttemptEvaluated = null;
        OnAllPlantsSelected = null;
        OnDiseaseIntroPanelShown = null;
        OnDiseaseAnalysisCompleted = null;
        OnSessionEnded = null;
        OnFlowStateChanged = null;
    }

    /// <summary>
    /// Limpia suscripciones estáticas colgadas al arrancar el juego. Es útil cuando el
    /// "Domain Reload" está desactivado (Enter Play Mode Options), pues los eventos
    /// estáticos conservarían listeners de la sesión de Play anterior. Se ejecuta antes
    /// de que cualquier MonoBehaviour se suscriba en OnEnable, así que no afecta a los
    /// singletons persistentes (DontDestroyOnLoad).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticListenersOnStartup()
    {
        ClearAllListeners();
    }
}
