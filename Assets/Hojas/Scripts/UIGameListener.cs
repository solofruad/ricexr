using UnityEngine;
using DG.Tweening;

/// <summary>
/// Orquesta toda la UI del juego escuchando el GameEventBus.
///
/// FLUJO COMPLETO:
///
/// [Tutorial]
///   TutorialPanelController.ShowAndStart() → barra corre → OnTutorialCompleted()
///   → SceneInteractionManager.StartFirstLevel()
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

    [Tooltip("Índice base-0 del nivel de evaluación. En ese nivel no se muestra " +
             "el panel de enfermedad ni marcadores.")]
    [SerializeField] private int levelFinalIndex = 2;

    [Header("Mensajes")]
    [SerializeField] private string tutorialLevelSubtitle = "Tutorial práctico: selecciona y clasifica las hojas requeridas.";

    [Header("Timings (segundos)")]
    [SerializeField] private float congratsDuration = 2.0f;
    [SerializeField] private float nextLevelMessageDuration = 2.0f;
    [SerializeField] private float finalLevelMessageDuration = 3.0f;

    private Tween _flowTween;
    private int _currentLevelIndex = -1;
    private int _currentPlantsRequired = 1;

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
        _flowTween?.Kill();
    }

    // ─────────────────────────────────────────────
    // Handlers
    // ─────────────────────────────────────────────

    private void HandleLevelStarted(int levelIndex, int totalLevels, int plantsRequired)
    {
        _flowTween?.Kill();
        _currentLevelIndex = levelIndex;
        _currentPlantsRequired = plantsRequired < 1 ? 1 : plantsRequired;

        messages.HideAll();
        diseasePanel?.Hide();

        bool isFinalLevel = levelIndex == levelFinalIndex;
        bool isTutorialGameplayLevel = levelIndex == tutorialLevelIndex;

        if (isFinalLevel)
        {
            messages.ShowFinalLevel();
            _flowTween = DOVirtual.DelayedCall(finalLevelMessageDuration, () =>
            {
                messages.HideAll();
                messages.ShowProgress(0, _currentPlantsRequired);
            });
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
                _flowTween = DOVirtual.DelayedCall(nextLevelMessageDuration, () =>
                {
                    messages.HideAll();
                    ShowDiseasePanelForLevel(levelIndex, _currentPlantsRequired);
                });
            }
            else
            {
                ShowDiseasePanelForLevel(levelIndex, _currentPlantsRequired);
            }
        }
    }

    private void HandleLevelCompleted(int levelIndex)
    {
        _flowTween?.Kill();
        messages.HideAll();
        diseasePanel?.Hide();

        if (levelIndex == tutorialLevelIndex)
            tutorialPanel?.Hide();
    }

    private void HandleAllLevelsCompleted()
    {
        _flowTween?.Kill();
        messages.HideAll();
        diseasePanel?.Hide();
        messages.ShowCongrats("¡Completaste todas las pruebas!");
    }

    private void HandlePlantSelected(bool isCorrect, int plantsSelected, int plantsRequired)
    {
        _flowTween?.Kill();
        messages.HideAll();

        if (plantsSelected < _currentPlantsRequired)
            messages.ShowProgress(plantsSelected, _currentPlantsRequired);
    }

    private void HandleAllPlantsSelected()
    {
        _flowTween?.Kill();
        messages.HideAll();
        messages.ShowCongrats("¡Encontraste todas las plantas enfermas!");
        _flowTween = DOVirtual.DelayedCall(congratsDuration, () =>
        {
            messages.HideAll();
            GameEventBus.PublishDiseaseAnalysisCompleted();
        });
    }

    /// <summary>
    /// La barra del panel terminó — el panel ya subió internamente (SlideUp en controller).
    /// SceneInteractionManager escucha este mismo evento para spawnear el cultivo.
    /// </summary>
    private void HandleDiseaseAnalysisCompleted()
    {
        if (ShouldShowDiseasePanel(_currentLevelIndex))
            messages.ShowProgress(0, _currentPlantsRequired);
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    private bool ShouldShowDiseasePanel(int levelIndex)
    {
        if (levelIndex < 0) return false;
        return levelIndex != levelFinalIndex && levelIndex != tutorialLevelIndex;
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

    // ─────────────────────────────────────────────
    // API pública para el tutorial
    // ─────────────────────────────────────────────

    /// <summary>
    /// Llamar desde TutorialPanelController cuando la barra del tutorial termina.
    /// Inicia el primer nivel de aprendizaje.
    /// </summary>
    public void OnTutorialCompleted()
    {
        messages.HideAll();
        SceneInteractionManager.Instance?.StartFirstLevel();
    }
}