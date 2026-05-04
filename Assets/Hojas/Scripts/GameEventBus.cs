using System;
using UnityEngine;

public enum FlowState
{
    None = 0,
    Idle = 1,
    WaitingForPlaneSelection = 2,
    DisablingPlanes = 3,
    StartupOnboarding = 4,
    Tutorial = 5,
    LevelStarting = 6,
    WaitingForDiseaseAnalysis = 7,
    LevelPlaying = 8,
    WaitingForLevelCompletion = 9,
    LevelTransition = 10,
    AllLevelsCompleted = 11,
    SessionEnding = 12
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

    /// <summary>Emitido cuando el usuario presiona "Iniciar" en el menú principal.</summary>
    public static event Action OnSessionStartRequested;

    /// <summary>Emitido cuando el usuario selecciona un plano MR. Incluye posición, rotación y escala.</summary>
    public static event Action<Vector3, Quaternion, Vector3> OnPlaneSelected;

    /// <summary>Emitido cuando los planos de MR terminan de ocultarse.</summary>
    public static event Action OnPlanesHidden;

    /// <summary>Emitido cuando inicia el onboarding post-plano.</summary>
    public static event Action OnOnboardingStarted;

    /// <summary>Emitido cuando el onboarding post-plano se completa.</summary>
    public static event Action OnOnboardingCompleted;

    /// <summary>Emitido cuando el tutorial inicia (panel visible y gameplay listo).</summary>
    public static event Action OnTutorialStarted;

    /// <summary>Emitido cuando el tutorial se completa (todas las hojas identificadas).</summary>
    public static event Action OnTutorialCompleted;

    /// <summary>Emitido cuando se solicita spawnear un nivel.</summary>
    public static event Action<int> OnLevelSpawnRequested;

    /// <summary>Emitido cuando un nivel fue spawneado.</summary>
    public static event Action<int> OnLevelSpawned;

    /// <summary>Emitido cuando se solicita terminar la sesión.</summary>
    public static event Action OnSessionEndRequested;

    /// <summary>Emitido cuando se solicita guardar datos de sesión.</summary>
    public static event Action OnSessionSaveRequested;

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

    // ── Enfermedad detectada ─────────────────────────────────────────────────

    /// <summary>Emitido cuando se identifica una enfermedad y hay datos para mostrar en el panel.</summary>
    public static event Action<PlantDiseaseData> OnDiseaseIdentified;

    /// <summary>Emitido cuando el panel de enfermedad termina su análisis visual.</summary>
    public static event Action OnDiseaseAnalysisCompleted;

    // ── Sesión (legacy + nuevos) ─────────────────────────────────────────────

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

    public static void PublishSessionStartRequested()
    {
        Debug.Log("[GameEventBus] SessionStartRequested");
        OnSessionStartRequested?.Invoke();
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

    public static void PublishOnboardingStarted()
    {
        Debug.Log("[GameEventBus] OnboardingStarted");
        OnOnboardingStarted?.Invoke();
    }

    public static void PublishOnboardingCompleted()
    {
        Debug.Log("[GameEventBus] OnboardingCompleted");
        OnOnboardingCompleted?.Invoke();
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

    public static void PublishLevelSpawnRequested(int levelIndex)
    {
        Debug.Log($"[GameEventBus] LevelSpawnRequested → {levelIndex}");
        OnLevelSpawnRequested?.Invoke(levelIndex);
    }

    public static void PublishLevelSpawned(int levelIndex)
    {
        Debug.Log($"[GameEventBus] LevelSpawned → {levelIndex}");
        OnLevelSpawned?.Invoke(levelIndex);
    }

    public static void PublishSessionEndRequested()
    {
        Debug.Log("[GameEventBus] SessionEndRequested");
        OnSessionEndRequested?.Invoke();
    }

    public static void PublishSessionSaveRequested()
    {
        Debug.Log("[GameEventBus] SessionSaveRequested");
        OnSessionSaveRequested?.Invoke();
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

    public static void PublishDiseaseIdentified(PlantDiseaseData data)
    {
        Debug.Log($"[GameEventBus] DiseaseIdentified → {data?.title}");
        OnDiseaseIdentified?.Invoke(data);
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
        OnPlaneSelected = null;
        OnPlanesHidden = null;
        OnOnboardingStarted = null;
        OnOnboardingCompleted = null;
        OnTutorialStarted = null;
        OnTutorialCompleted = null;
        OnLevelSpawnRequested = null;
        OnLevelSpawned = null;
        OnSessionEndRequested = null;
        OnSessionSaveRequested = null;
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
        OnDiseaseIdentified = null;
        OnDiseaseAnalysisCompleted = null;
        OnSessionEnded = null;
        OnFlowStateChanged = null;
    }
}
