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
///   OnLevelStarted → 3 paneles que presentan la enfermedad (portada → qué es →
///                    cuánto ha avanzado), cada uno esperando "Continuar"
///   Al continuar en el último → diseasePanel.Show(asset del índice)
///                    + PublishDiseaseAnalysisCompleted (diferido un frame)
///   OnDiseaseAnalysisCompleted → las hojas crecen
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
/// ⚠ ORDEN CRÍTICO: PublishLevelStarted es síncrono, así que HandleLevelStarted
/// corre ANTES de que GameFlowController arme _waitingAnalysisToSpawnLevel. Por
/// eso DiseaseAnalysisCompleted se publica siempre desde una corrutina que
/// espera al menos un frame: publicarlo síncrono pierde el evento y cuelga el
/// flujo en FlowState.WaitingForDiseaseAnalysis.
///
/// CONFIGURACIÓN EN INSPECTOR:
///   - diseaseDataPerLevel: un PlantDiseaseDataAsset por nivel de aprendizaje,
///     en el mismo orden que levelConfigs en SceneInteractionManager.
///     El nivel final no necesita entrada (puede dejarse vacío o ser más corto).
///   - diseaseIntroController: el controlador de los 3 paneles. Si se deja
///     vacío, el nivel muestra directamente la ficha de consulta.
///   - levelFinalIndex: índice base-0 del nivel de evaluación en levelConfigs.
/// </summary>
public class UIGameListener : MonoBehaviour
{
    [Header("Controllers de UI")]
    [SerializeField] private UIMessagesController messagesController;
    [SerializeField] private PlantDiseasePanelController diseasePanelController;
    [SerializeField] private TutorialPanelController tutorialPanelController;

    [Tooltip("Los 3 paneles que presentan la enfermedad antes de que crezcan las hojas. " +
             "Vacío = solo la ficha de consulta.")]
    [SerializeField] private DiseaseIntroController diseaseIntroController;

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
        diseaseIntroController?.ResetState();

        int  finalLevelIndex         = GetFinalLevelIndex(totalLevels);
        bool isFinalLevel            = levelIndex == finalLevelIndex;
        bool isTutorialGameplayLevel = levelIndex == tutorialLevelIndex;

        if (isFinalLevel || isTutorialGameplayLevel)
        {
            // Las miniguias (FinalLevel, Tutorial) ya las mostró LevelIntroController
            // antes de este evento. Aquí solo se enciende el contador de progreso,
            // o se deja pasar el tiempo del mensaje en el nivel final.
            if (!isFinalLevel)
                messagesController.ShowProgress(0, _currentPlantsRequired);
            else
                _flowCoroutine = StartCoroutine(DelayedFinalLevelFlow());
        }
        else
        {
            // Nivel normal: mostrar panel de enfermedad.
            ShowDiseasePanelForLevel(levelIndex);
        }
    }

    private void HandleLevelCompleted(int levelIndex)
    {
        CancelFlowCoroutine();
        messagesController.HideAll();
        diseasePanelController?.Hide();
        diseaseIntroController?.ResetState();

        if (levelIndex == tutorialLevelIndex)
            tutorialPanelController?.Hide();
    }

    private void HandleAllLevelsCompleted()
    {
        CancelFlowCoroutine();
        messagesController.HideAll();
        diseasePanelController?.Hide();
        diseaseIntroController?.ResetState();
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
        CancelFlowCoroutine();
        messagesController.HideAll();
        messagesController.ShowCongrats("¡Encontraste todas las plantas enfermas!");
        _flowCoroutine = StartCoroutine(DelayedCongratsFlow());
    }

    /// <summary>
    /// La enfermedad ya está presentada y la ficha de consulta a la vista.
    /// GameFlowController escucha este mismo evento para hacer crecer las hojas;
    /// aquí solo se enciende el contador de progreso del nivel.
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
    /// Presenta la enfermedad del nivel en 3 paneles y, al continuar en el
    /// último, deja la ficha de consulta y hace crecer las hojas.
    /// </summary>
    private void ShowDiseasePanelForLevel(int levelIndex)
    {
        PanelDiseaseData data = ResolveDiseaseData(levelIndex);

        // Sin datos no hay nada que enseñar, pero el nivel tiene que arrancar
        // igual: GameFlowController está esperando este evento, y sin él la
        // sesión se queda colgada para siempre.
        if (data == null)
        {
            _flowCoroutine = StartCoroutine(PublishAnalysisNextFrame());
            return;
        }

        if (diseaseIntroController == null)
        {
            ShowDiseaseCard(data);
            return;
        }

        diseaseIntroController.ShowIntro(data, () => ShowDiseaseCard(data));
    }

    /// <summary>
    /// Deja la ficha de la enfermedad a mano para el resto del nivel y libera el
    /// flujo para que las hojas crezcan.
    /// </summary>
    private void ShowDiseaseCard(PanelDiseaseData data)
    {
        diseasePanelController?.SetSeverityEvolutionImage(data.severityEvolutionImage);
        diseasePanelController?.Show(data);

        _flowCoroutine = StartCoroutine(PublishAnalysisNextFrame());
    }

    /// <summary>
    /// Publica DiseaseAnalysisCompleted un frame más tarde. Ver la nota de ORDEN
    /// CRÍTICO en la cabecera de la clase: síncrono, el evento se pierde.
    /// </summary>
    private IEnumerator PublishAnalysisNextFrame()
    {
        yield return null;
        _flowCoroutine = null;
        GameEventBus.PublishDiseaseAnalysisCompleted();
    }

    /// <summary>
    /// Datos del asset configurado para el nivel indicado, o null con un warning
    /// si el índice no tiene asset asignado.
    /// </summary>
    private PanelDiseaseData ResolveDiseaseData(int levelIndex)
    {
        if (diseaseDataPerLevel == null
            || levelIndex < 0
            || levelIndex >= diseaseDataPerLevel.Length
            || diseaseDataPerLevel[levelIndex] == null)
        {
            Debug.LogWarning($"[UIGameListener] No hay PlantDiseaseDataAsset para el nivel {levelIndex}. " +
                             "Asigna uno en el array 'Disease Data Per Level' del Inspector.");
            return null;
        }

        PanelDiseaseData data = diseaseDataPerLevel[levelIndex].data;
        if (data == null)
            Debug.LogWarning($"[UIGameListener] El asset del nivel {levelIndex} existe pero 'data' está vacío.");

        return data;
    }
}