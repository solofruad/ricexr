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
/// [Niveles de aprendizaje (índice menor que levelFinalIndex)]
///   OnLevelStarted → diseasePanel.Show(asset del índice) + StartProgressBar()
///   OnDiseaseAnalysisCompleted → panel sube solo (SlideUp interno), cultivo aparece
///   OnPlantSelected(correcto)  → MsgCorrect → MsgProgress
///   OnPlantSelected(incorrecto)→ MsgIncorrect → MsgProgress
///   OnAllPlantsSelected        → MsgCongrats(nivel)
///   OnLevelCompleted           → diseasePanel.Hide() + HideAll mensajes
///
/// [Nivel final de evaluación (índice == levelFinalIndex)]
///   OnLevelStarted → MsgFinalLevel (sin panel de enfermedad)
///   Resto igual que niveles normales
///
/// NOTA: Se usan Coroutines en vez de DOVirtual.DelayedCall para las
/// transiciones críticas de flujo. DOVirtual.DelayedCall puede fallar
/// silenciosamente en builds Android IL2CPP (Meta Quest).
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
    [SerializeField] private UIMessagesController messages;
    [SerializeField] private PlantDiseasePanelController diseasePanel;
    [SerializeField] private TutorialPanelController tutorialPanel;

    [Header("Datos de enfermedad por nivel")]
    [Tooltip("Un PlantDiseaseDataAsset por cada nivel de aprendizaje, en el mismo " +
             "orden que levelConfigs en SceneInteractionManager. " +
             "El nivel final no necesita entrada.")]
    [SerializeField] private PlantDiseaseDataAsset[] diseaseDataPerLevel;

    [Header("Índices especiales")]
    [Tooltip("Índice base-0 del nivel de tutorial jugable (práctica sin panel de enfermedad).")]
    [SerializeField] private int tutorialLevelIndex = 0;


    [Header("Timings (segundos)")]
    [SerializeField] private float congratsDuration = 2.0f;
    [SerializeField] private float nextLevelMessageDuration = 2.0f;
    [SerializeField] private float finalLevelMessageDuration = 3.0f;

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
        _currentLevelIndex = levelIndex;
        _currentTotalLevels = totalLevels;
        _currentPlantsRequired = plantsRequired < 1 ? 1 : plantsRequired;

        messages.HideAll();
        diseasePanel?.Hide();

        int finalLevelIndex = GetFinalLevelIndex(totalLevels);
        bool isFinalLevel = levelIndex == finalLevelIndex;
        bool isTutorialGameplayLevel = levelIndex == tutorialLevelIndex;

        if (isFinalLevel)
        {
            messages.ShowFinalLevel();
            _flowCoroutine = StartCoroutine(DelayedFinalLevelFlow());
        }
        else if (isTutorialGameplayLevel)
        {
            // En tutorial NO mostrar "siguiente nivel" automáticamente.
            // Solo mostrar el progreso requerido del nivel tutorial.
            messages.ShowProgress(0, _currentPlantsRequired);
        }
        else
        {
            if (levelIndex > 0)
            {
                messages.ShowNextLevel($"Nivel {levelIndex + 1} de {totalLevels}");
                _flowCoroutine = StartCoroutine(DelayedNextLevelFlow(levelIndex));
            }
            else
            {
                ShowDiseasePanelForLevel(levelIndex, _currentPlantsRequired);
            }
        }
    }

    private void HandleLevelCompleted(int levelIndex)
    {
        CancelFlowCoroutine();
        messages.HideAll();
        diseasePanel?.Hide();

        if (levelIndex == tutorialLevelIndex)
            tutorialPanel?.Hide();
    }

    private void HandleAllLevelsCompleted()
    {
        CancelFlowCoroutine();
        messages.HideAll();
        diseasePanel?.Hide();
        messages.ShowCongrats("¡Completaste todas las pruebas!");
    }

    private void HandlePlantSelected(bool isCorrect, int plantsSelected, int plantsRequired)
    {
        CancelFlowCoroutine();
        messages.HideAll();

        if (plantsSelected < _currentPlantsRequired)
            messages.ShowProgress(plantsSelected, _currentPlantsRequired);
    }

    private void HandleAllPlantsSelected()
    {
        CancelFlowCoroutine();
        messages.HideAll();
        messages.ShowCongrats("¡Encontraste todas las plantas enfermas!");
        _flowCoroutine = StartCoroutine(DelayedCongratsFlow());
    }

    /// <summary>
    /// La barra del panel terminó — el panel ya subió internamente (SlideUp en controller).
    /// GameFlowController escucha este mismo evento para spawnear el cultivo.
    /// </summary>
    private void HandleDiseaseAnalysisCompleted()
    {
        if (ShouldShowDiseasePanel(_currentLevelIndex))
            messages.ShowProgress(0, _currentPlantsRequired);
    }

    // ─────────────────────────────────────────────
    // Coroutines (reemplazan DOVirtual.DelayedCall)
    // ─────────────────────────────────────────────

    private IEnumerator DelayedNextLevelFlow(int levelIndex)
    {
        yield return new WaitForSeconds(nextLevelMessageDuration);
        messages.HideAll();
        ShowDiseasePanelForLevel(levelIndex, _currentPlantsRequired);
    }

    private IEnumerator DelayedFinalLevelFlow()
    {
        yield return new WaitForSeconds(finalLevelMessageDuration);
        messages.HideAll();
        GameEventBus.PublishDiseaseAnalysisCompleted();
    }

    private IEnumerator DelayedCongratsFlow()
    {
        yield return new WaitForSeconds(congratsDuration);
        messages.HideAll();
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
        if (diseasePanel == null) return;

        if (diseaseDataPerLevel == null
            || levelIndex >= diseaseDataPerLevel.Length
            || diseaseDataPerLevel[levelIndex] == null)
        {
            Debug.LogWarning($"[UIGameListener] No hay PlantDiseaseDataAsset para el nivel {levelIndex}. " +
                             "Asigna uno en el array 'Disease Data Per Level' del Inspector.");
            return;
        }

        PlantDiseaseData data = diseaseDataPerLevel[levelIndex].data;
        if (data == null)
        {
            Debug.LogWarning($"[UIGameListener] El asset del nivel {levelIndex} existe pero 'data' está vacío.");
            return;
        }

        diseasePanel.Show(data);
        diseasePanel.StartProgressBar();
    }
}