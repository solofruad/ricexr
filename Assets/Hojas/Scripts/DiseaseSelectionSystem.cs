using System.Collections;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de selección de enfermedades para simulación de diagnóstico vegetal.
///
/// Funcionalidades principales:
/// 1. Gestiona dos grupos de selección: Enfermedades y Niveles de Severidad
/// 2. Valida las selecciones del usuario contra TODOS los DiseaseSpots de la hoja
/// 3. Si un spot tiene severity == -1, solo se valida la enfermedad (severidad ignorada)
/// 4. Proporciona feedback visual inmediato (correcto/incorrecto)
/// 5. Permite un margen de error configurable para la severidad
/// 6. Integra con el sistema de progreso de niveles del SceneInteractionManager
/// 7. Se mantiene oculto hasta que SceneInteractionManager llama a Show()
///
/// Flujo de trabajo:
/// 1. SceneInteractionManager.WaitForStartSignal() → Show()
/// 2. Usuario selecciona enfermedad y severidad mediante Toggles
/// 3. Usuario presiona botón de submit
/// 4. Sistema verifica hoja agarrada y compara con TODOS sus DiseaseSpots
/// 5. Muestra feedback visual y registra métricas
/// 6. Si correcto: muestra marcadores y avanza nivel
/// </summary>
public class DiseaseSelectionSystem : MonoBehaviour
{
    public static DiseaseSelectionSystem Instance { get; private set; }

    // ── Panel raíz ────────────────────────────────────────────────────────
    [Header("Panel raíz")]
    [Tooltip("Raíz de toda la UI del selector. Se oculta al inicio y se activa al iniciar pruebas.")]
    public GameObject diseaseSelectionPanel;
    [Header("Toggle Groups")]
    [Tooltip("ToggleGroup para las enfermedades")]
    public ToggleGroup diseaseToggleGroup;

    [Tooltip("ToggleGroup para la severidad")]
    public ToggleGroup severityToggleGroup;

    [Header("Mapeo de Enfermedades")]
    [Tooltip("Toggles de enfermedades en orden")]
    public List<Toggle> diseaseToggles = new List<Toggle>();

    [Tooltip("Nombres correspondientes a cada toggle")]
    public List<string> diseaseNames = new List<string>
    {
        "Pyricularia oryzae",
        "Rhynchosporium oryzae",
    };

    [Header("Mapeo de Severidad")]
    [Tooltip("Toggles de severidad en orden (1-5)")]
    public List<Toggle> severityToggles = new List<Toggle>();

    [Header("Botón de Submit")]
    public Button submitButton;

    [Header("Objetos de Feedback")]
    [Tooltip("GameObject que se mostrará cuando sea correcto")]
    public GameObject correctFeedbackObject;

    [Tooltip("GameObject que se mostrará cuando sea incorrecto")]
    public GameObject incorrectFeedbackObject;

    [Tooltip("GameObject que se mostrará cuando no hay hoja seleccionada")]
    public GameObject noLeafSelectedFeedbackObject;

    [Tooltip("Duración que el feedback estará visible")]
    public float feedbackDuration = 3f;

    [Tooltip("Margen de error permitido en la severidad")]
    public int MarginOfError = 0;

    void Start()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Ocultar la UI hasta que el usuario pulse "Iniciar"
        if (diseaseSelectionPanel        != null) diseaseSelectionPanel.SetActive(false);
        if (correctFeedbackObject        != null) correctFeedbackObject.SetActive(false);
        if (incorrectFeedbackObject      != null) incorrectFeedbackObject.SetActive(false);
        if (noLeafSelectedFeedbackObject != null) noLeafSelectedFeedbackObject.SetActive(false);

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmit);
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>Muestra la UI del selector. Llamar desde SceneInteractionManager.WaitForStartSignal().</summary>
    public void Show()
    {
        if (diseaseSelectionPanel != null)
            diseaseSelectionPanel.SetActive(true);
    }

    /// <summary>Oculta la UI del selector.</summary>
    public void Hide()
    {
        if (diseaseSelectionPanel != null)
            diseaseSelectionPanel.SetActive(false);
    }

    void ShowNoLeafFeedback()
    {
        if (noLeafSelectedFeedbackObject == null)
        {
            Debug.LogWarning("No hay objeto asignado para feedback de 'sin hoja seleccionada'");
            return;
        }

        // Mostrar el objeto
        noLeafSelectedFeedbackObject.SetActive(true);

        // Ocultarlo después de un tiempo
        StartCoroutine(HideFeedbackAfterDelay(noLeafSelectedFeedbackObject));
    }

    void OnSubmit()
    {
        // Verificar que hay una hoja agarrada
        Leaf currentLeaf = GrabbableObjectListener.Instance.ActualLeafGrabbed;

        if (currentLeaf == null)
        {
            Debug.LogWarning("No hay ninguna hoja agarrada");
            ShowNoLeafFeedback(); 
            return;
        }

        // Obtener las selecciones actuales
        string selectedDisease = GetSelectedDisease();
        int selectedSeverity = GetSelectedSeverity();

        // Verificar que se ha seleccionado algo
        if (string.IsNullOrEmpty(selectedDisease))
        {
            Debug.LogWarning("Debes seleccionar una enfermedad");
            return;
        }

        if (selectedSeverity <= 0)
        {
            Debug.LogWarning("Debes seleccionar una severidad");
            return;
        }

        // Obtener índice del nivel actual para las métricas
        int levelIndex = SceneInteractionManager.Instance != null
   ? SceneInteractionManager.Instance.GetCurrentLevelIndex()
            : 0;

        // Validar contra TODOS los spots de la hoja
        DiseaseSpot matchedSpot;
        bool isCorrect = ValidateSelection(currentLeaf, selectedDisease, selectedSeverity, out matchedSpot);

        ShowFeedback(isCorrect);

        if (isCorrect)
        {
            if (SessionMetricsTracker.Instance != null)
                SessionMetricsTracker.Instance.CompleteLevel(levelIndex, selectedDisease, selectedSeverity);

            currentLeaf.ableToShowMarkers = true;
            currentLeaf.showMarkers       = true;
            currentLeaf.SetMarkersVisibility(true);

            StartCoroutine(AdvanceToNextLevel());
        }
        else
        {
            if (SessionMetricsTracker.Instance != null)
                SessionMetricsTracker.Instance.RegisterFailedAttempt(levelIndex, selectedDisease, selectedSeverity);
        }
    }

    string GetSelectedDisease()
    {
        if (diseaseToggleGroup == null)
        {
            Debug.LogError("No se ha asignado el ToggleGroup de enfermedades");
            return null;
        }

        // Obtener el toggle activo del grupo
        Toggle activeToggle = diseaseToggleGroup.GetFirstActiveToggle();

        if (activeToggle == null)
        {
            return null;
        }

        // Encontrar el índice del toggle activo
        int index = diseaseToggles.IndexOf(activeToggle);

        if (index >= 0 && index < diseaseNames.Count)
        {
            return diseaseNames[index];
        }

        Debug.LogWarning($"Toggle activo no encontrado en la lista de disease toggles");
        return null;
    }

    int GetSelectedSeverity()
    {
        if (severityToggleGroup == null)
        {
            Debug.LogError("No se ha asignado el ToggleGroup de severidad");
            return -1;
        }

        // Obtener el toggle activo del grupo
        Toggle activeToggle = severityToggleGroup.GetFirstActiveToggle();

        if (activeToggle == null)
        {
            return -1;
        }

        // Encontrar el índice del toggle activo (índice + 1 = severidad)
        int index = severityToggles.IndexOf(activeToggle);

        if (index >= 0)
        {
            return index + 1;
        }

        Debug.LogWarning($"Toggle activo no encontrado en la lista de severity toggles");
        return -1;
    }

    /// <summary>
    /// Devuelve true si la selección coincide con CUALQUIERA de los DiseaseSpots de la hoja.
    /// Si un spot tiene severity == -1, la severidad se ignora y solo se valida la enfermedad.
    /// El spot que coincidió se devuelve en matchedSpot.
    /// </summary>
    bool ValidateSelection(Leaf leaf, string selectedDisease, int selectedSeverity, out DiseaseSpot matchedSpot)
    {
        matchedSpot = null;

        if (leaf.diseaseSpots == null || leaf.diseaseSpots.Count == 0)
        {
            Debug.LogWarning("La hoja no tiene manchas configuradas");
            return false;
        }

        foreach (DiseaseSpot spot in leaf.diseaseSpots)
        {
            bool diseaseMatch = selectedDisease == spot.diseaseName;

            // Si severity == -1 en el spot, no se evalúa la severidad
            bool severityMatch = spot.severity == -1
                ? true
                : Mathf.Abs(selectedSeverity - spot.severity) <= MarginOfError;

            if (diseaseMatch && severityMatch)
            {
                matchedSpot = spot;
                Debug.Log($"¡CORRECTO! Enfermedad: {selectedDisease}, Severidad: {selectedSeverity}");
                return true;
            }
        }

        // Construir mensaje con todas las opciones válidas
        StringBuilder sb = new StringBuilder("INCORRECTO. Seleccionaste: ");
        sb.Append($"{selectedDisease} Sev:{selectedSeverity}. Válidos: ");
        foreach (DiseaseSpot spot in leaf.diseaseSpots)
        {
            string sevStr = spot.severity == -1 ? "cualquiera" : spot.severity.ToString();
            sb.Append($"[{spot.diseaseName} Sev:{sevStr}] ");
        }
        Debug.Log(sb.ToString());
        return false;
    }

    void ShowFeedback(bool isCorrect)
    {
        // Ocultar ambos objetos primero
        if (correctFeedbackObject != null)
        {
            correctFeedbackObject.SetActive(false);
        }

        if (incorrectFeedbackObject != null)
        {
            incorrectFeedbackObject.SetActive(false);
        }

        // Seleccionar el objeto apropiado
        GameObject objectToShow = isCorrect ? correctFeedbackObject : incorrectFeedbackObject;

        if (objectToShow == null)
        {
            Debug.LogWarning($"No hay objeto asignado para feedback {(isCorrect ? "correcto" : "incorrecto")}");
            return;
        }

        // Mostrar el objeto
        objectToShow.SetActive(true);

        // Ocultarlo después de un tiempo
        StartCoroutine(HideFeedbackAfterDelay(objectToShow));
    }

    IEnumerator HideFeedbackAfterDelay(GameObject feedbackObject)
    {
        yield return new WaitForSeconds(feedbackDuration);

        if (feedbackObject != null)
        {
            feedbackObject.SetActive(false);
        }
    }

    IEnumerator AdvanceToNextLevel()
    {
        // Esperar a que termine de mostrarse el feedback
        yield return new WaitForSeconds(feedbackDuration);

        // Resetear la selección
        ResetSelection();

        // Notificar al InteractionManager para que avance al siguiente nivel
        if (SceneInteractionManager.Instance != null)
        {
            SceneInteractionManager.Instance.AdvanceToNextLevel();
        }
        else
        {
            Debug.LogWarning("InteractionManager.Instance no encontrado");
        }
    }

    public void ResetSelection()
    {
        // Desactivar todos los toggles
        if (diseaseToggleGroup != null)
        {
            diseaseToggleGroup.SetAllTogglesOff();
        }

        if (severityToggleGroup != null)
        {
            severityToggleGroup.SetAllTogglesOff();
        }
    }

    void OnDestroy()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(OnSubmit);
        }
    }
}   