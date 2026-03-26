using System;
using UnityEngine;

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

    // ── Sesión ───────────────────────────────────────────────────────────────

    /// <summary>Emitido cuando la sesión completa termina.</summary>
    public static event Action OnSessionEnded;

    // ════════════════════════════════════════════════════════════════════════
    // Métodos de publicación — llamar estos, nunca invocar los eventos directo
    // ════════════════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════════════════
    // Limpieza — llamar en escenas que se descargan para evitar memory leaks
    // ════════════════════════════════════════════════════════════════════════

    public static void ClearAllListeners()
    {
        OnLevelStarted = null;
        OnLevelCompleted = null;
        OnAllLevelsCompleted = null;
        OnPlantSelected = null;
        OnDiagnosisAttemptEvaluated = null;
        OnAllPlantsSelected = null;
        OnDiseaseIdentified = null;
        OnDiseaseAnalysisCompleted = null;
        OnSessionEnded = null;
    }
}
