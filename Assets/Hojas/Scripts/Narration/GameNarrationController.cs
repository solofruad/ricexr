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
    [SerializeField] private NarrationReproductor narrationReproductor; // Reemplazado TTSSpeaker
    [SerializeField] private NarrationLineLibrary lineLibrary;

    [Header("Binding opcional (granularidad fina del tutorial)")]
    [Tooltip("Solo para eventos de actos del tutorial. Los eventos de inicio/fin vienen del bus.")]
    [SerializeField] private TutorialPanelController tutorialPanelController;

    [Header("Comportamiento")]
    [SerializeField] private bool autoFindDependencies = true;
    [SerializeField] private bool stopCurrentBeforeSpeaking = true;
    [SerializeField] private bool verboseLogs = false;
    [SerializeField] private int tutorialLevelIndex = 0;

    private readonly HashSet<string> _spokenSession = new HashSet<string>();
    private readonly Dictionary<int, HashSet<string>> _spokenPerLevel = new Dictionary<int, HashSet<string>>();
    private readonly Dictionary<string, float> _lastPlayedAt = new Dictionary<string, float>();
    private readonly HashSet<int> _errorGuidedLevels = new HashSet<int>();
    private readonly Dictionary<string, NarrationLineEntry> _fallbackLines = new Dictionary<string, NarrationLineEntry>();

    private TutorialPanelController _boundTutorialPanelController;

    private int _currentLevelIndex = -1;
    private bool _sessionActive;
    private bool _tutorialGuidedCompleted;

    private void Awake()
    {
        BuildFallbackIndex();
        ResolveDependencies();
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

    private void BuildFallbackIndex()
    {
        _fallbackLines.Clear();

        List<NarrationLineEntry> defaults = GameNarrationDefaults.CreateDefaultEntries();
        for (int i = 0; i < defaults.Count; i++)
        {
            NarrationLineEntry line = defaults[i];
            if (line == null || string.IsNullOrWhiteSpace(line.id)) continue;
            if (_fallbackLines.ContainsKey(line.id)) continue;
            _fallbackLines.Add(line.id, line);
        }
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

        if (levelIndex == totalLevels - 1)
        {
            if (plantsRequired >= 3)
            {
                SpeakSequence(true, GameNarrationLineIds.FinalWarning, GameNarrationLineIds.ProgressRemain3);
            }
            else
            {
                SpeakLine(GameNarrationLineIds.FinalWarning, true);
            }
            return;
        }

        if (levelIndex == 1)
        {
            if (plantsRequired >= 3)
            {
                SpeakSequence(true, GameNarrationLineIds.Level2Intro, GameNarrationLineIds.Level2Explain, GameNarrationLineIds.ProgressRemain3);
            }
            else
            {
                SpeakSequence(true, GameNarrationLineIds.Level2Intro, GameNarrationLineIds.Level2Explain);
            }
            return;
        }

        if (levelIndex == 2)
        {
            if (plantsRequired >= 3)
            {
                SpeakSequence(true, GameNarrationLineIds.Level3Intro, GameNarrationLineIds.Level3Explain, GameNarrationLineIds.ProgressRemain3);
            }
            else
            {
                SpeakSequence(true, GameNarrationLineIds.Level3Intro, GameNarrationLineIds.Level3Explain);
            }
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
            SpeakLine(GameNarrationLineIds.ProgressRemain3, true);
            return;
        }

        if (remaining == 2)
        {
            SpeakSequence(true, GameNarrationLineIds.FirstCorrect, GameNarrationLineIds.ProgressRemain2);
            return;
        }

        if (remaining == 1)
        {
            SpeakSequence(true, GameNarrationLineIds.SecondCorrect, GameNarrationLineIds.ProgressRemain1);
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

        if (interruptCurrent && stopCurrentBeforeSpeaking)
            narrationReproductor.Stop();

        // Mandamos el ID al reproductor en lugar del texto
        narrationReproductor.Speak(line.id); 
        
        RegisterPlayed(line);
        Log($"Speak: {lineId}");
        return true;
    }

    private void SpeakSequence(bool interruptCurrent, params string[] lineIds)
    {
        if (narrationReproductor == null) return;
        if (lineIds == null || lineIds.Length == 0) return;

        List<NarrationLineEntry> playableLines = new List<NarrationLineEntry>();
        for (int i = 0; i < lineIds.Length; i++)
        {
            string lineId = lineIds[i];
            if (!TryResolveLine(lineId, out NarrationLineEntry line)) continue;
            if (!CanPlay(line)) continue;
            playableLines.Add(line);
        }

        if (playableLines.Count == 0) return;

        if (interruptCurrent && stopCurrentBeforeSpeaking)
            narrationReproductor.Stop();

        for (int i = 0; i < playableLines.Count; i++)
        {
            NarrationLineEntry line = playableLines[i];
            if (i == 0)
                narrationReproductor.Speak(line.id); // El primero se reproduce normal
            else
                narrationReproductor.SpeakQueued(line.id); // Los siguientes se encolan

            RegisterPlayed(line);
            Log($"Queue: {line.id}");
        }
    }

    private bool TryResolveLine(string lineId, out NarrationLineEntry line)
    {
        line = null;
        if (string.IsNullOrWhiteSpace(lineId)) return false;

        if (lineLibrary != null && lineLibrary.TryGetLine(lineId, out line))
            return line != null;

        return _fallbackLines.TryGetValue(lineId, out line);
    }

    private bool CanPlay(NarrationLineEntry line)
    {
        if (line == null) return false;
        if (!line.enabled) return false;
        // Quité la validación de string.IsNullOrWhiteSpace(line.text) porque ahora reproducimos por ID.

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