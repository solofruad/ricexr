using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Es el menu UI con su sistema para la seleccion de enfermedades.
///
/// Funcionalidades principales:
/// 1. Gestiona dos grupos de seleccion: Enfermedades y Niveles de Severidad
/// 2. Valida las selecciones del usuario contra TODOS los DiseaseSpots de la hoja
/// 3. Si un spot tiene severity == -1, solo se valida la enfermedad (severidad ignorada)
/// 4. Proporciona feedback visual inmediato (correcto/incorrecto)
/// 5. Permite un margen de error configurable para la severidad
/// 6. Integra con el sistema de progreso de niveles del SceneInteractionManager
/// 7. Se mantiene oculto hasta que SceneInteractionManager llama a Show()
///
/// Flujo de trabajo:
/// 1. SceneInteractionManager.WaitForStartSignal() → ShowAnimated()
/// 2. Usuario selecciona enfermedad y severidad mediante Toggles
/// 3. Usuario presiona boton de submit
/// 4. Sistema verifica hoja agarrada y compara con TODOS sus DiseaseSpots
/// 5. Muestra feedback visual y registra metricas
/// 6. Si correcto: muestra marcadores y avanza nivel
/// </summary>
public class DiseaseSelectionSystem : MonoBehaviour
{
    public static DiseaseSelectionSystem Instance { get; private set; }

    // ── Panel raiz ────────────────────────────────────────────────────────
    [Header("Panel raiz")]
    [Tooltip("Raiz de toda la UI del selector. Se oculta al inicio y se activa al iniciar pruebas.")]
    public GameObject diseaseSelectionPanel;

    [Header("Posicionamiento en mundo")]
    [Tooltip("Desplazamiento relativo al plano elegido. X = lateral, Y = altura, Z = profundidad")]
    public Vector3 positionOffset = new Vector3(0.6f, 0.1f, 0f);

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

    [Header("Boton de Submit")]
    public Button submitButton;

    [Header("Objetos de Feedback")]
    [Tooltip("GameObject que se mostrara cuando sea correcto")]
    public GameObject correctFeedbackObject;

    [Tooltip("GameObject que se mostrara cuando sea incorrecto")]
    public GameObject incorrectFeedbackObject;

    [Tooltip("GameObject que se mostrara cuando no hay hoja seleccionada")]
    public GameObject noLeafSelectedFeedbackObject;

    [Tooltip("Duracion que el feedback estara visible")]
    public float feedbackDuration = 3f;

    [Tooltip("Margen de error permitido en la severidad")]
    public int MarginOfError = 0;

    [Header("Animacion de aparicion")]
    [Tooltip("Duracion en segundos del agrandar/achicar al mostrar u ocultar")]
    public float animDuration = 0.3f;

    private Vector3 _originalScale = Vector3.one;

    void Start()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (diseaseSelectionPanel != null)
        {
            _originalScale = diseaseSelectionPanel.transform.localScale;
            diseaseSelectionPanel.SetActive(false);
        }
        if (correctFeedbackObject != null) correctFeedbackObject.SetActive(false);
        if (incorrectFeedbackObject != null) incorrectFeedbackObject.SetActive(false);
        if (noLeafSelectedFeedbackObject != null) noLeafSelectedFeedbackObject.SetActive(false);

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmit);
    }



    /// <summary>Muestra el panel con animacion de escala.</summary>
    public void ShowAnimated()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.transform.DOKill();
        diseaseSelectionPanel.transform.localScale = Vector3.zero;
        diseaseSelectionPanel.SetActive(true);
        diseaseSelectionPanel.transform.DOScale(_originalScale, animDuration).SetEase(Ease.OutBack);
    }

    /// <summary>Oculta el panel con animacion de escala.</summary>
    public void HideAnimated()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.transform.DOKill();
        diseaseSelectionPanel.transform
            .DOScale(Vector3.zero, animDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => diseaseSelectionPanel.SetActive(false));
    }

    /// <summary>Muestra instantaneamente (sin animacion).</summary>
    public void Show()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.transform.localScale = _originalScale;
        diseaseSelectionPanel.SetActive(true);
    }

    /// <summary>Oculta instantaneamente (sin animacion).</summary>
    public void Hide()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.SetActive(false);
    }

    /// <summary>
    /// Posiciona el panel al lado del plano elegido y lo orienta hacia la camara.
    /// planePosition: centro del plano en world space.
    /// planeRight:    vector derecha del plano para desplazar lateralmente.
    /// </summary>
    public void PlaceNextTo(Vector3 planePosition, Vector3 planeRight, Vector3 planeNormal)
    {
        if (diseaseSelectionPanel == null) return;

        Vector3 worldPos = planePosition
            + planeRight * positionOffset.x
            + Vector3.up * positionOffset.y
            + planeNormal * positionOffset.z;

        diseaseSelectionPanel.transform.position = worldPos;

        Vector3 toCam = Camera.main != null
            ? Camera.main.transform.position - worldPos
            : Vector3.forward;
        toCam.y = 0f;
        if (toCam != Vector3.zero)
            diseaseSelectionPanel.transform.rotation = Quaternion.LookRotation(-toCam);
    }

    // ── Logica de submit ─────────────────────────────────────────────────
    void OnSubmit()
    {
        Leaf currentLeaf = GrabbableObjectListener.Instance.ActualLeafGrabbed;

        if (currentLeaf == null)
        {
            Debug.LogWarning("No hay ninguna hoja agarrada");
            ShowFeedbackObject(noLeafSelectedFeedbackObject);
            return;
        }

        string selectedDisease = GetSelectedDisease();
        int selectedSeverity = GetSelectedSeverity();

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

        int levelIndex = SceneInteractionManager.Instance != null
            ? SceneInteractionManager.Instance.GetCurrentLevelIndex()
            : 0;

        bool isCorrect = ValidateSelection(currentLeaf, selectedDisease, selectedSeverity, out _);

        ShowFeedback(isCorrect);

        if (isCorrect)
        {
            if (SessionMetricsTracker.Instance != null)
                SessionMetricsTracker.Instance.CompleteLevel(levelIndex, selectedDisease, selectedSeverity);

            currentLeaf.ableToShowMarkers = true;
            currentLeaf.showMarkers = true;
            currentLeaf.SetMarkersVisibility(true);

            // Esperar al feedback y luego avanzar nivel
            DOVirtual.DelayedCall(feedbackDuration, () =>
            {
                ResetSelection();
                if (SceneInteractionManager.Instance != null)
                    SceneInteractionManager.Instance.AdvanceToNextLevel();
                else
                    Debug.LogWarning("SceneInteractionManager.Instance no encontrado");
            });
        }
        else
        {
            if (SessionMetricsTracker.Instance != null)
                SessionMetricsTracker.Instance.RegisterFailedAttempt(levelIndex, selectedDisease, selectedSeverity);
        }
    }

    // ── Helpers de seleccion ─────────────────────────────────────────────
    string GetSelectedDisease()
    {
        if (diseaseToggleGroup == null)
        {
            Debug.LogError("No se ha asignado el ToggleGroup de enfermedades");
            return null;
        }

        Toggle activeToggle = diseaseToggleGroup.GetFirstActiveToggle();
        if (activeToggle == null) return null;

        int index = diseaseToggles.IndexOf(activeToggle);
        if (index >= 0 && index < diseaseNames.Count)
            return diseaseNames[index];

        Debug.LogWarning("Toggle activo no encontrado en la lista de disease toggles");
        return null;
    }

    int GetSelectedSeverity()
    {
        if (severityToggleGroup == null)
        {
            Debug.LogError("No se ha asignado el ToggleGroup de severidad");
            return -1;
        }

        Toggle activeToggle = severityToggleGroup.GetFirstActiveToggle();
        if (activeToggle == null) return -1;

        int index = severityToggles.IndexOf(activeToggle);
        if (index >= 0) return index + 1;

        Debug.LogWarning("Toggle activo no encontrado en la lista de severity toggles");
        return -1;
    }

    /// <summary>
    /// Devuelve true si la seleccion coincide con CUALQUIERA de los DiseaseSpots de la hoja.
    /// Si un spot tiene severity == -1, la severidad se ignora y solo se valida la enfermedad.
    /// El spot que coincidio se devuelve en matchedSpot.
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

        var sb = new StringBuilder("INCORRECTO. Seleccionaste: ");
        sb.Append($"{selectedDisease} Sev:{selectedSeverity}. Validos: ");
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
        if (correctFeedbackObject != null) correctFeedbackObject.SetActive(false);
        if (incorrectFeedbackObject != null) incorrectFeedbackObject.SetActive(false);

        GameObject target = isCorrect ? correctFeedbackObject : incorrectFeedbackObject;
        ShowFeedbackObject(target);
    }

    void ShowFeedbackObject(GameObject feedbackObject)
    {
        if (feedbackObject == null)
        {
            Debug.LogWarning("No hay objeto asignado para este tipo de feedback");
            return;
        }

        feedbackObject.SetActive(true);
        DOVirtual.DelayedCall(feedbackDuration, () =>
        {
            if (feedbackObject != null)
                feedbackObject.SetActive(false);
        });
    }

    public void ResetSelection()
    {
        if (diseaseToggleGroup != null) diseaseToggleGroup.SetAllTogglesOff();
        if (severityToggleGroup != null) severityToggleGroup.SetAllTogglesOff();
    }

    void OnDestroy()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnSubmit);

        diseaseSelectionPanel?.transform.DOKill();
    }
}