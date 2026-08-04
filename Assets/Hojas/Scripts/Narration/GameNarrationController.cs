using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centraliza la narracion por eventos del juego.
///
/// Diseno:
/// - Un solo controlador (alta cohesion) escucha eventos de flujo.
/// - NarrationReproductor se usa como detalle de infraestructura desacoplado de la logica.
/// - Textos vienen de biblioteca configurable; si falta, usa defaults offline.
/// </summary>
[DisallowMultipleComponent]
public class GameNarrationController : MonoBehaviour
{
    [Header("Dependencias")]
    [SerializeField] private NarrationReproductor narrationReproductor;
    [SerializeField] private NarrationLineLibrary lineLibrary;

    [Header("Binding opcional (granularidad fina del tutorial)")]
    [Tooltip("Solo para eventos de actos del tutorial. Los eventos de inicio/fin vienen del bus.")]
    [SerializeField] private TutorialPanelController tutorialPanelController;

    [Header("Comportamiento")]
    [SerializeField] private bool autoFindDependencies = true;
    [SerializeField] private bool stopCurrentBeforeSpeaking = true;
    [SerializeField] private bool verboseLogs = false;
    [SerializeField] private int tutorialLevelIndex = 0;

    /// <summary>
    /// Guion de intro por nivel. Desacopla la narración de una estructura fija de niveles:
    /// añadir o reordenar niveles ya no requiere tocar el código, solo esta lista.
    /// El último nivel (levelIndex == totalLevels - 1) usa siempre "FinalWarning" y no
    /// necesita entrada aquí. El nivel del tutorial se narra por sus propios eventos.
    /// </summary>
    [System.Serializable]
    private class LevelIntroNarration
    {
        public int levelIndex;
        public string[] lineIds;
    }

    [Header("Narración por nivel (data-driven)")]
    [Tooltip("IDs de línea de intro por índice de nivel. El último nivel usa 'FinalWarning' automáticamente.")]
    [SerializeField]
    private List<LevelIntroNarration> levelIntroNarrations = new List<LevelIntroNarration>();

    private readonly HashSet<string> _spokenSession = new HashSet<string>();
    private readonly Dictionary<int, HashSet<string>> _spokenPerLevel = new Dictionary<int, HashSet<string>>();
    private readonly Dictionary<string, float> _lastPlayedAt = new Dictionary<string, float>();
    private readonly HashSet<int> _errorGuidedLevels = new HashSet<int>();

    private TutorialPanelController _boundTutorialPanelController;

    private int _currentLevelIndex = -1;
    private bool _sessionActive;
    private bool _tutorialGuidedCompleted;

    private void Awake()
    {
        ResolveDependencies();
        EnsureLevelIntroDefaults();
    }

    /// <summary>
    /// Rellena el guion por nivel con los valores por defecto si la lista está vacía
    /// (p. ej. en escenas creadas antes de exponer este campo). Así el comportamiento
    /// es correcto sin necesidad de poblar el asset en el Editor.
    /// </summary>
    private void EnsureLevelIntroDefaults()
    {
        if (levelIntroNarrations != null && levelIntroNarrations.Count > 0) return;

        levelIntroNarrations = new List<LevelIntroNarration>
        {
            new LevelIntroNarration
            {
                levelIndex = 1,
                lineIds = new[] { GameNarrationLineIds.Level2Intro, GameNarrationLineIds.Level2Explain }
            },
            new LevelIntroNarration
            {
                levelIndex = 2,
                lineIds = new[] { GameNarrationLineIds.Level3Intro, GameNarrationLineIds.Level3Explain }
            },
        };
    }

    private string[] GetLevelIntroLines(int levelIndex)
    {
        if (levelIntroNarrations == null) return null;
        foreach (var entry in levelIntroNarrations)
            if (entry != null && entry.levelIndex == levelIndex)
                return entry.lineIds;
        return null;
    }

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void ResolveDependencies()
    {
        if (!autoFindDependencies) return;

        if (narrationReproductor == null)
            narrationReproductor = GetComponent<NarrationReproductor>() ?? FindObjectOfType<NarrationReproductor>(true);

        if (tutorialPanelController == null)
            tutorialPanelController = FindObjectOfType<TutorialPanelController>(true);
    }

    private void SubscribeEvents()
    {
        GameEventBus.OnSessionStartRequested += HandleStartFlowRequested;
        GameEventBus.OnLevelIntroStarted += HandleLevelIntroStarted;
        GameEventBus.OnTutorialStarted += HandleTutorialStarted;
        GameEventBus.OnTutorialCompleted += HandleTutorialCompleted;

        GameEventBus.OnLevelStarted += HandleLevelStarted;
        GameEventBus.OnPlantSelected += HandlePlantSelected;
        GameEventBus.OnLevelCompleted += HandleLevelCompleted;
        GameEventBus.OnAllLevelsCompleted += HandleAllLevelsCompleted;

        GameEventBus.OnEndPanelShown += HandleEndPanelShown;
        GameEventBus.OnSaveCompleted += HandleSaveCompleted;
        GameEventBus.OnReturningToMenu += HandleReturningToMenu;

        BindTutorialActEvents();
    }

    private void UnsubscribeEvents()
    {
        GameEventBus.OnSessionStartRequested -= HandleStartFlowRequested;
        GameEventBus.OnLevelIntroStarted -= HandleLevelIntroStarted;
        GameEventBus.OnTutorialStarted -= HandleTutorialStarted;
        GameEventBus.OnTutorialCompleted -= HandleTutorialCompleted;

        GameEventBus.OnLevelStarted -= HandleLevelStarted;
        GameEventBus.OnPlantSelected -= HandlePlantSelected;
        GameEventBus.OnLevelCompleted -= HandleLevelCompleted;
        GameEventBus.OnAllLevelsCompleted -= HandleAllLevelsCompleted;

        GameEventBus.OnEndPanelShown -= HandleEndPanelShown;
        GameEventBus.OnSaveCompleted -= HandleSaveCompleted;
        GameEventBus.OnReturningToMenu -= HandleReturningToMenu;

        UnbindTutorialActEvents();
    }

    private void BindTutorialActEvents()
    {
        if (tutorialPanelController != null && _boundTutorialPanelController != tutorialPanelController)
        {
            if (_boundTutorialPanelController != null)
            {
                _boundTutorialPanelController.GuidedPhaseCompleted -= HandleTutorialGuidedPhaseCompleted;
                _boundTutorialPanelController.TutorialActChanged -= HandleTutorialActChanged;
            }

            _boundTutorialPanelController = tutorialPanelController;
            _boundTutorialPanelController.GuidedPhaseCompleted += HandleTutorialGuidedPhaseCompleted;
            _boundTutorialPanelController.TutorialActChanged += HandleTutorialActChanged;
        }
    }

    private void UnbindTutorialActEvents()
    {
        if (_boundTutorialPanelController != null)
        {
            _boundTutorialPanelController.GuidedPhaseCompleted -= HandleTutorialGuidedPhaseCompleted;
            _boundTutorialPanelController.TutorialActChanged -= HandleTutorialActChanged;
            _boundTutorialPanelController = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Handlers (Se mantienen igual)
    // ═══════════════════════════════════════════════════════════════════════

    private void HandleStartFlowRequested()
    {
        ResolveDependencies();
        BindTutorialActEvents();

        if (_boundTutorialPanelController == null)
            Debug.LogWarning("[Narration] TutorialPanelController no enlazado. La narracion de actos del tutorial no se ejecutara.");

        ResetSessionState();
        _sessionActive = true;
        SpeakLine(GameNarrationLineIds.StartSelectPlane, true);
    }

    private void HandleLevelIntroStarted()
    {
        SpeakSequence(true,
            GameNarrationLineIds.OnboardingOverview,
            GameNarrationLineIds.OnboardingContinue);
    }

    private void HandleTutorialStarted()
    {
        _tutorialGuidedCompleted = false;
        SpeakLine(GameNarrationLineIds.TutorialBegin, true);
    }

    private void HandleTutorialGuidedPhaseCompleted()
    {
        _tutorialGuidedCompleted = true;
        SpeakLine(GameNarrationLineIds.TutorialFreePractice, true);
    }

    private void HandleTutorialActChanged(TutorialGuidanceAct act)
    {
        switch (act)
        {
            case TutorialGuidanceAct.GRAB_LEAF:
                SpeakLine(GameNarrationLineIds.TutorialGrab, true);
                break;
            case TutorialGuidanceAct.OBSERVE_LEAF:
                SpeakLine(GameNarrationLineIds.TutorialInspect, true);
                break;
            case TutorialGuidanceAct.REVEAL_HINT:
                // La pista tiene una entrada de audio propia. Si aun no se ha
                // grabado, al menos no dejamos sonando la frase de observacion.
                StopNarration();
                SpeakLine(GameNarrationLineIds.TutorialHint, true);
                break;
            case TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF:
                SpeakSequence(true,
                    GameNarrationLineIds.TutorialMenuOpen,
                    GameNarrationLineIds.TutorialSelectDisease,
                    GameNarrationLineIds.TutorialSelectSeverity,
                    GameNarrationLineIds.TutorialConfirm);
                break;
        }
    }

    private void HandleTutorialCompleted()
    {
        SpeakLine(GameNarrationLineIds.TutorialDone, true);
    }

    private void HandleLevelStarted(int levelIndex, int totalLevels, int plantsRequired)
    {
        _currentLevelIndex = levelIndex;
        if (levelIndex == tutorialLevelIndex)
            _tutorialGuidedCompleted = false;
        EnsureLevelBucket(levelIndex);

        string progressRemainId = GetProgressRemainId(plantsRequired);
        bool appendProgress = !string.IsNullOrEmpty(progressRemainId) && plantsRequired >= 2;

        // Último nivel: aviso final. El índice se calcula (totalLevels - 1), no está hardcodeado.
        if (levelIndex == totalLevels - 1)
        {
            if (appendProgress)
            {
                SpeakSequence(true, GameNarrationLineIds.FinalWarning, progressRemainId);
            }
            else
            {
                SpeakLine(GameNarrationLineIds.FinalWarning, true);
            }
            return;
        }

        // Resto de niveles: guion definido por datos en levelIntroNarrations.
        string[] baseLines = GetLevelIntroLines(levelIndex);
        if (baseLines == null || baseLines.Length == 0)
            return;

        if (appendProgress)
        {
            var sequence = new string[baseLines.Length + 1];
            System.Array.Copy(baseLines, sequence, baseLines.Length);
            sequence[baseLines.Length] = progressRemainId;
            SpeakSequence(true, sequence);
        }
        else
        {
            SpeakSequence(true, baseLines);
        }
    }

    private void HandlePlantSelected(bool isCorrect, int plantsSelected, int plantsRequired)
    {
        if (!_sessionActive) return;

        if (_currentLevelIndex == tutorialLevelIndex)
        {
            if (!_tutorialGuidedCompleted)
            {
                if (!isCorrect) SpeakLine(GameNarrationLineIds.TutorialRetryGuided, true);
                return;
            }

            if (isCorrect && plantsSelected >= plantsRequired)
                SpeakLine(GameNarrationLineIds.ProgressDone, true);
            return;
        }

        if (!isCorrect)
        {
            SpeakFirstErrorOnlyForCurrentLevel();
            return;
        }

        if (plantsRequired <= 1)
        {
            SpeakLine(GameNarrationLineIds.FirstCorrect, true);
            return;
        }

        int remaining = Mathf.Max(0, plantsRequired - plantsSelected);

        if (remaining >= 3)
        {
            string progressRemainId = GetProgressRemainId(remaining);
            if (!string.IsNullOrEmpty(progressRemainId))
                SpeakLine(progressRemainId, true);
            return;
        }

        if (remaining == 2)
        {
            string progressRemainId = GetProgressRemainId(remaining);
            if (!string.IsNullOrEmpty(progressRemainId))
                SpeakSequence(true, GameNarrationLineIds.FirstCorrect, progressRemainId);
            else
                SpeakLine(GameNarrationLineIds.FirstCorrect, true);
            return;
        }

        if (remaining == 1)
        {
            string progressRemainId = GetProgressRemainId(remaining);
            if (!string.IsNullOrEmpty(progressRemainId))
                SpeakSequence(true, GameNarrationLineIds.SecondCorrect, progressRemainId);
            else
                SpeakLine(GameNarrationLineIds.SecondCorrect, true);
            return;
        }

        SpeakSequence(true, GameNarrationLineIds.ThirdCorrect, GameNarrationLineIds.ProgressDone);
    }

    private void HandleLevelCompleted(int levelIndex)
    {
        _currentLevelIndex = levelIndex;
        SpeakLine(GameNarrationLineIds.LevelComplete, true);
    }

    private void HandleAllLevelsCompleted()
    {
        SpeakLine(GameNarrationLineIds.AllLevelsComplete, true);
    }

    private void HandleEndPanelShown()
    {
        SpeakLine(GameNarrationLineIds.EndSessionPromptSave, true);
    }

    private void HandleSaveCompleted(bool saved)
    {
        SpeakLine(saved ? GameNarrationLineIds.SaveOk : GameNarrationLineIds.SaveFail, true);
    }

    private void HandleReturningToMenu()
    {
        SpeakLine(GameNarrationLineIds.ReturnMenu, true);
        _sessionActive = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Core Reproducción utilizando NarrationReproductor
    // ═══════════════════════════════════════════════════════════════════════

    private void SpeakFirstErrorOnlyForCurrentLevel()
    {
        if (_currentLevelIndex < 0) return;
        if (_errorGuidedLevels.Contains(_currentLevelIndex)) return;

        if (SpeakLine(GameNarrationLineIds.FirstErrorOnly, true))
            _errorGuidedLevels.Add(_currentLevelIndex);
    }

    private bool SpeakLine(string lineId, bool interruptCurrent)
    {
        if (narrationReproductor == null) return false;
        if (!TryResolveLine(lineId, out NarrationLineEntry line)) return false;
        if (!CanPlay(line)) return false;
        if (!TryGetRandomClip(line, out AudioClip clip)) return false;

        if (interruptCurrent && stopCurrentBeforeSpeaking)
            narrationReproductor.Stop();

        narrationReproductor.Speak(clip);
        
        RegisterPlayed(line);
        Log($"Speak: {lineId}");
        return true;
    }

    private void SpeakSequence(bool interruptCurrent, params string[] lineIds)
    {
        if (narrationReproductor == null) return;
        if (lineIds == null || lineIds.Length == 0) return;

        List<NarrationLineEntry> playableLines = new List<NarrationLineEntry>();
        List<AudioClip> playableClips = new List<AudioClip>();
        for (int i = 0; i < lineIds.Length; i++)
        {
            string lineId = lineIds[i];
            if (!TryResolveLine(lineId, out NarrationLineEntry line)) continue;
            if (!CanPlay(line)) continue;
            if (!TryGetRandomClip(line, out AudioClip clip)) continue;
            playableLines.Add(line);
            playableClips.Add(clip);
        }

        if (playableLines.Count == 0) return;

        if (interruptCurrent && stopCurrentBeforeSpeaking)
            narrationReproductor.Stop();

        for (int i = 0; i < playableLines.Count; i++)
        {
            NarrationLineEntry line = playableLines[i];
            if (i == 0)
                narrationReproductor.Speak(playableClips[i]); // El primero se reproduce normal
            else
                narrationReproductor.SpeakQueued(playableClips[i]); // Los siguientes se encolan

            RegisterPlayed(line);
            Log($"Queue: {line.id}");
        }
    }

    private bool TryResolveLine(string lineId, out NarrationLineEntry line)
    {
        line = null;
        if (string.IsNullOrWhiteSpace(lineId)) return false;

        if (lineLibrary == null) return false;

        if (lineLibrary.TryGetLine(lineId, out line))
            return line != null;

        Debug.LogWarning($"[Narration] LineId no encontrado: {lineId}");
        return false;
    }

    private bool CanPlay(NarrationLineEntry line)
    {
        if (line == null) return false;
        if (!line.enabled) return false;

        if (line.oncePerSession && _spokenSession.Contains(line.id))
            return false;

        if (line.oncePerLevel && _currentLevelIndex >= 0)
        {
            EnsureLevelBucket(_currentLevelIndex);
            if (_spokenPerLevel[_currentLevelIndex].Contains(line.id))
                return false;
        }

        if (line.cooldownSec > 0f && _lastPlayedAt.TryGetValue(line.id, out float lastPlayed))
        {
            if (Time.time - lastPlayed < line.cooldownSec)
                return false;
        }

        if (line.firstErrorOnly && _currentLevelIndex >= 0 && _errorGuidedLevels.Contains(_currentLevelIndex))
            return false;

        return true;
    }

    private bool TryGetRandomClip(NarrationLineEntry line, out AudioClip clip)
    {
        clip = null;
        if (line == null || line.audioClips == null || line.audioClips.Count == 0)
        {
            Debug.LogWarning($"[Narration] Sin audios para {line?.id}");
            return false;
        }

        int attempts = line.audioClips.Count;
        for (int i = 0; i < attempts; i++)
        {
            int index = Random.Range(0, line.audioClips.Count);
            AudioClip candidate = line.audioClips[index];
            if (candidate != null)
            {
                clip = candidate;
                return true;
            }
        }

        Debug.LogWarning($"[Narration] Audios nulos para {line.id}");
        return false;
    }

    private string GetProgressRemainId(int remaining)
    {
        if (remaining <= 0) return null;

        if (remaining > 10)
            Debug.LogWarning($"[Narration] remaining={remaining} excede el maximo soportado (10). Se usara 10.");

        int clamped = Mathf.Clamp(remaining, 1, 10);
        switch (clamped)
        {
            case 10: return GameNarrationLineIds.ProgressRemain10;
            case 9: return GameNarrationLineIds.ProgressRemain9;
            case 8: return GameNarrationLineIds.ProgressRemain8;
            case 7: return GameNarrationLineIds.ProgressRemain7;
            case 6: return GameNarrationLineIds.ProgressRemain6;
            case 5: return GameNarrationLineIds.ProgressRemain5;
            case 4: return GameNarrationLineIds.ProgressRemain4;
            case 3: return GameNarrationLineIds.ProgressRemain3;
            case 2: return GameNarrationLineIds.ProgressRemain2;
            case 1: return GameNarrationLineIds.ProgressRemain1;
            default: return null;
        }
    }

    private void RegisterPlayed(NarrationLineEntry line)
    {
        _spokenSession.Add(line.id);
        _lastPlayedAt[line.id] = Time.time;

        if ((line.oncePerLevel || line.firstErrorOnly) && _currentLevelIndex >= 0)
        {
            EnsureLevelBucket(_currentLevelIndex);
            _spokenPerLevel[_currentLevelIndex].Add(line.id);
        }

        if (line.firstErrorOnly && _currentLevelIndex >= 0)
            _errorGuidedLevels.Add(_currentLevelIndex);
    }

    private void EnsureLevelBucket(int levelIndex)
    {
        if (!_spokenPerLevel.ContainsKey(levelIndex))
            _spokenPerLevel.Add(levelIndex, new HashSet<string>());
    }

    private void ResetSessionState()
    {
        _spokenSession.Clear();
        _spokenPerLevel.Clear();
        _lastPlayedAt.Clear();
        _errorGuidedLevels.Clear();
        _currentLevelIndex = -1;
        _tutorialGuidedCompleted = false;
        StopNarration();
    }

    private void StopNarration()
    {
        if (narrationReproductor == null) return;
        narrationReproductor.Stop();
    }

    private void Log(string message)
    {
        if (!verboseLogs) return;
        Debug.Log($"[Narration] {message}");
    }
}
