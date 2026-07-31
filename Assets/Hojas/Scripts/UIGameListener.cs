using System.Collections;
using UnityEngine;

/// <summary>
/// Orquesta toda la UI del juego escuchando el GameEventBus.
///
/// FLUJO COMPLETO:
///
/// [Tutorial]
///   TutorialPanelController.ShowAndStart() → barra corre → TutorialCompleted (bus)
///   → GameFlowController arranca primer nivel
///
/// [Niveles de aprendizaje (no tutorial, no final)]
///   OnLevelStarted → diseasePanel.Show(asset del índice) + StartProgressBar()
///   OnDiseaseAnalysisCompleted → panel sube solo (SlideUp interno), cultivo aparece
///   OnPlantSelected(correcto)  → MsgCorrect → MsgProgress
///   OnPlantSelected(incorrecto)→ MsgIncorrect → MsgProgress
///   OnAllPlantsSelected        → MsgCongrats(nivel)
///   OnLevelCompleted           → diseasePanel.Hide() + HideAll mensajes
///
/// [Nivel final de evaluación (final index)]
///   OnLevelStarted → (sin panel de enfermedad, sin MsgFinalLevel — ya lo mostro LevelIntroController)
///   Resto igual que niveles normales
///
/// NOTA: Se usan Coroutines en vez de DOVirtual.DelayedCall para las
/// transiciones críticas de flujo. DOVirtual.DelayedCall puede fallar
/// silenciosamente en builds Android IL2CPP (Meta Quest).
///
/// NOTA: Las miniguias de inicio de nivel (Tutorial, NextLevel, FinalLevel)
/// son ahora responsabilidad de LevelIntroController y se muestran ANTES
/// de que OnLevelStarted se emita. Este listener ya no las muestra.
///
/// CONFIGURACIÓN EN INSPECTOR:
///   - diseaseDataPerLevel: un PlantDiseaseDataAsset por nivel de aprendizaje,
///     en el mismo orden que levelConfigs en SceneInteractionManager.
///     El nivel final no necesita entrada (puede dejarse vacío o ser más corto).
///   - levelFinalIndex: índice base-0 del nivel de evaluación en levelConfigs.
/// </summary>
public class UIGameListener : MonoBehaviour
{
    [Header("Controllers de UI")]
    [SerializeField] private UIMessagesController messagesController;
    [SerializeField] private PlantDiseasePanelController diseasePanelController;
    [SerializeField] private TutorialPanelController tutorialPanelController;

    [Header("Datos de enfermedad por nivel")]
    [Tooltip("Un PlantDiseaseDataAsset por cada nivel de aprendizaje, en el mismo " +
             "orden que levelConfigs en SceneInteractionManager. " +
             "El nivel final no necesita entrada.")]
    [SerializeField] private PanelDiseaseDataObject[] diseaseDataPerLevel;

    [Header("Índices especiales")]
    [Tooltip("Índice base-0 del nivel de tutorial jugable (práctica sin panel de enfermedad).")]
    [SerializeField] private int tutorialLevelIndex = 0;


    [Header("Timings (segundos)")]
    [SerializeField] private float congratsDuration    = 2.0f;

    private Coroutine _flowCoroutine;
    private int _currentLevelIndex = -1;
    private int _currentPlantsRequired = 1;
    private int _currentTotalLevels = 0;

    // ─────────────────────────────────────────────
    // Suscripción al bus
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnLevelStarted += HandleLevelStarted;
        GameEventBus.OnLevelCompleted += HandleLevelCompleted;
        GameEventBus.OnAllLevelsCompleted += HandleAllLevelsCompleted;
        GameEventBus.OnPlantSelected += HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected += HandleAllPlantsSelected;
        GameEventBus.OnDiseaseAnalysisCompleted += HandleDiseaseAnalysisCompleted;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStarted -= HandleLevelStarted;
        GameEventBus.OnLevelCompleted -= HandleLevelCompleted;
        GameEventBus.OnAllLevelsCompleted -= HandleAllLevelsCompleted;
        GameEventBus.OnPlantSelected -= HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected -= HandleAllPlantsSelected;
        GameEventBus.OnDiseaseAnalysisCompleted -= HandleDiseaseAnalysisCompleted;
        CancelFlowCoroutine();
    }

    // ─────────────────────────────────────────────
    // Handlers
    // ─────────────────────────────────────────────

    private void HandleLevelStarted(int levelIndex, int totalLevels, int plantsRequired)
    {
        CancelFlowCoroutine();
        _currentLevelIndex    = levelIndex;
        _currentTotalLevels   = totalLevels;
        _currentPlantsRequired = plantsRequired < 1 ? 1 : plantsRequired;

        messagesController.HideAll();
        diseasePanelController?.Hide();

        int  finalLevelIndex         = GetFinalLevelIndex(totalLevels);
        bool isFinalLevel            = levelIndex == finalLevelIndex;
        bool isTutorialGameplayLevel = levelIndex == tutorialLevelIndex;

        if (isFinalLevel || isTutorialGameplayLevel)
        {
            // Las miniguias (FinalLevel, Tutorial) ya las mostró LevelIntroController antes de este evento.
            // Aquí solo iniciamos el progreso o esperamos DiseaseAnalysis.
            if (!isFinalLevel)
                messagesController.ShowProgress(0, _currentPlantsRequired);
            else
                _flowCoroutine = StartCoroutine(DelayedFinalLevelFlow());
        }
        else
        {
            // Nivel normal: mostrar panel de enfermedad.
            ShowDiseasePanelForLevel(levelIndex, _currentPlantsRequired);
        }
    }

    private void HandleLevelCompleted(int levelIndex)
    {
        CancelFlowCoroutine();
        messagesController.HideAll();
        diseasePanelController?.Hide();

        if (levelIndex == tutorialLevelIndex)
            tutorialPanelController?.Hide();
    }

    private void HandleAllLevelsCompleted()
    {
        CancelFlowCoroutine();
        messagesController.HideAll();
        diseasePanelController?.Hide();
    }

    private void HandlePlantSelected(bool isCorrect, int plantsSelected, int plantsRequired)
    {
        CancelFlowCoroutine();
        messagesController.HideAll();

        if (plantsSelected < _currentPlantsRequired)
            messagesController.ShowProgress(plantsSelected, _currentPlantsRequired);
    }

    private void HandleAllPlantsSelected()
    {
        if (_currentLevelIndex == tutorialLevelIndex)
            return; // The avatar celebration owns the transition, never this UI timer.

        CancelFlowCoroutine();
        messagesController.HideAll();
        messagesController.ShowCongrats("¡Encontraste todas las plantas enfermas!");
        _flowCoroutine = StartCoroutine(DelayedCongratsFlow());
    }

    /// <summary>
    /// La barra del panel terminó — el panel ya subió internamente (SlideUp en controller).
    /// GameFlowController escucha este mismo evento para spawnear el cultivo.
    /// </summary>
    private void HandleDiseaseAnalysisCompleted()
    {
        if (ShouldShowDiseasePanel(_currentLevelIndex))
            messagesController.ShowProgress(0, _currentPlantsRequired);
    }

    // ─────────────────────────────────────────────
    // Coroutines (reemplazan DOVirtual.DelayedCall)
    // ─────────────────────────────────────────────

    private IEnumerator DelayedFinalLevelFlow()
    {
        yield return new WaitForSeconds(congratsDuration);
        messagesController.HideAll();
        GameEventBus.PublishDiseaseAnalysisCompleted();
    }

    private IEnumerator DelayedCongratsFlow()
    {
        yield return new WaitForSeconds(congratsDuration);
        messagesController.HideAll();
        GameEventBus.PublishDiseaseAnalysisCompleted();
    }

    private void CancelFlowCoroutine()
    {
        if (_flowCoroutine != null)
        {
            StopCoroutine(_flowCoroutine);
            _flowCoroutine = null;
        }
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    private bool ShouldShowDiseasePanel(int levelIndex)
    {
        if (levelIndex < 0) return false;
        return levelIndex != GetFinalLevelIndex(_currentTotalLevels) && levelIndex != tutorialLevelIndex;
    }

    private int GetFinalLevelIndex(int totalLevels)
    {
        return Mathf.Max(0, totalLevels - 1);
    }

    /// <summary>
    /// Obtiene los datos del asset configurado para el nivel indicado
    /// y los muestra en el panel de enfermedad. Si no hay asset configurado
    /// para ese índice, el panel no se muestra (falla silenciosamente con warning).
    /// </summary>
    private void ShowDiseasePanelForLevel(int levelIndex, int plantsRequired)
    {
        if (diseasePanelController == null) return;

        if (diseaseDataPerLevel == null
            || levelIndex >= diseaseDataPerLevel.Length
            || diseaseDataPerLevel[levelIndex] == null)
        {
            Debug.LogWarning($"[UIGameListener] No hay PlantDiseaseDataAsset para el nivel {levelIndex}. " +
                             "Asigna uno en el array 'Disease Data Per Level' del Inspector.");
            return;
        }

        PanelDiseaseData data = diseaseDataPerLevel[levelIndex].data;
        if (data == null)
        {
            Debug.LogWarning($"[UIGameListener] El asset del nivel {levelIndex} existe pero 'data' está vacío.");
            return;
        }

        diseasePanelController.SetSeverityEvolutionImage(data.severityEvolutionImage);
        diseasePanelController.Show(data);
        diseasePanelController.StartProgressBar();
    }
}
