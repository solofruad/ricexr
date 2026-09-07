using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using RiceXR.Core;

/// <summary>
/// Es el menu UI con su sistema para la seleccion de enfermedades.
/// Funcionalidades principales:
/// 1. Gestiona dos grupos de seleccion: Enfermedades y Niveles de Severidad
/// 2. Valida las selecciones del usuario contra TODOS los DiseaseSpots de la hoja
/// 3. Si un spot tiene severity == -1, solo se valida la enfermedad (severidad ignorada)
/// 4. Proporciona feedback visual inmediato (correcto/incorrecto)
/// 5. Permite un margen de error configurable para la severidad
/// 6. Integra con el sistema de progreso de niveles por GameEventBus
/// 7. Aparece y desaparece solo, según haya o no una hoja agarrada
/// Flujo de trabajo:
/// 1. El jugador agarra una hoja → SyncPanelWithSelection() en LateUpdate lo muestra
/// 2. Usuario selecciona enfermedad y severidad mediante Toggles
/// 3. Usuario presiona boton de submit
/// 4. Sistema verifica hoja agarrada y compara con TODOS sus DiseaseSpots
/// 5. Muestra feedback visual, registra eventos y publica al GameEventBus
/// 6. Si correcto: muestra marcadores y publica progreso de plantas
/// </summary>
public class DiseaseSelectionSystem : MonoBehaviour
{
    public static DiseaseSelectionSystem Instance { get; private set; }

    // ── Panel raiz ───────────────────────────────────────────────────────────
    [Header("Panel raiz")]
    [Tooltip("Raiz de toda la UI del selector. Se oculta al inicio y se activa al iniciar pruebas.")]
    public GameObject diseaseSelectionPanel;

    [Header("Posicionamiento en mundo")]
    [Tooltip("Desplazamiento relativo al ancla de la mano/hoja. X = lateral, Y = altura, Z = profundidad")]
    public Vector3 positionOffset = new Vector3(0.6f, 0.1f, 0f);

    [Header("Sonidos de feedback")]
    public AudioClip correctSelectionSound;
    public AudioClip incorrectSelectionSound;

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

    private int _correctSelectionsThisLevel = 0;
    private int _plantsRequiredThisLevel = 1;

    private readonly HashSet<int> _identifiedLeavesThisLevel = new HashSet<int>();

    private Vector3 _originalScale = Vector3.one;
    private bool _panelVisible = false;
    private bool _panelAvailable = true;
    private Coroutine _feedbackCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        GameEventBus.OnLevelStarted += HandleLevelStarted;
    }

    void LateUpdate()
    {
        SyncPanelWithSelection();
    }

#if UNITY_EDITOR
    // Atajo de desarrollo: la tecla C diagnostica la hoja agarrada, para poder
    // recorrer el flujo entero en el editor sin ponerse el casco. Solo compila en
    // editor, nunca llega a la build.
    void Update()
    {
        if (Keyboard.current?.cKey.wasPressedThisFrame != true || !_panelAvailable)
            return;

        Leaf currentLeaf = GrabbableLeafListener.Instance?.ActualLeafGrabbed
            ?? FindFirstObjectByType<Leaf>();
        if (currentLeaf == null || currentLeaf.diseaseSpots == null
            || currentLeaf.diseaseSpots.Count == 0 || currentLeaf.diseaseSpots[0] == null)
            return;

        DiseaseSpot spot = currentLeaf.diseaseSpots[0];
        int diseaseIndex = diseaseNames.IndexOf(spot.diseaseName);
        int severityIndex = Mathf.Clamp(spot.severity, 1, 5) - 1;
        if (diseaseIndex < 0 || diseaseIndex >= diseaseToggles.Count || severityIndex >= severityToggles.Count)
            return;

        GrabbableLeafListener.Instance?.SetActiveSelection(
            currentLeaf, GrabbableLeafListener.SelectionHand.Unknown, currentLeaf.transform);
        diseaseToggles[diseaseIndex].isOn = true;
        severityToggles[severityIndex].isOn = true;
        OnSubmit();
    }
#endif

    void Start()
    {
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

    void OnDisable()
    {
        GameEventBus.OnLevelStarted -= HandleLevelStarted;
        if (_feedbackCoroutine != null)
        {
            StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = null;
        }
    }

    /// <summary>Muestra el panel con animacion de escala.</summary>
    public void ShowAnimated()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.transform.DOKill();
        diseaseSelectionPanel.transform.localScale = new Vector3(0f, _originalScale.y, _originalScale.z);
        diseaseSelectionPanel.SetActive(true);
        diseaseSelectionPanel.transform
            .DOScale(new Vector3(_originalScale.x, _originalScale.y, _originalScale.z), animDuration)
            .SetEase(Ease.InOutQuart);
        _panelVisible = true;
    }

    /// <summary>Oculta el panel con animacion de escala.</summary>
    public void HideAnimated()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.transform.DOKill();
        diseaseSelectionPanel.transform
            .DOScale(new Vector3(0f, _originalScale.y, _originalScale.z), animDuration)
            .SetEase(Ease.InOutQuart)
            .OnComplete(() =>
            {
                if (diseaseSelectionPanel != null)
                    diseaseSelectionPanel.SetActive(false);
            });
        _panelVisible = false;
    }

    /// <summary>Muestra instantaneamente (sin animacion).</summary>
    public void Show()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.transform.localScale = _originalScale;
        diseaseSelectionPanel.SetActive(true);
        _panelVisible = true;
    }

    /// <summary>Oculta instantaneamente (sin animacion).</summary>
    public void Hide()
    {
        if (diseaseSelectionPanel == null) return;
        diseaseSelectionPanel.transform.DOKill();
        diseaseSelectionPanel.SetActive(false);
        _panelVisible = false;
    }

    /// <summary>
    /// Controla si el selector puede abrirse automaticamente al agarrar una hoja.
    /// El tutorial lo desactiva durante agarrar/observar/pista y lo habilita solo
    /// al entrar en el acto de diagnostico.
    /// </summary>
    public void SetPanelAvailability(bool available)
    {
        _panelAvailable = available;
        if (!available)
            Hide();
    }


    private void SyncPanelWithSelection()
    {
        if (diseaseSelectionPanel == null)
            return;

        if (!_panelAvailable)
        {
            if (_panelVisible) HideAnimated();
            return;
        }

        if (GrabbableLeafListener.Instance == null)
        {
            if (_panelVisible) HideAnimated();
            return;
        }

        Leaf selectedLeaf = GrabbableLeafListener.Instance.ActualLeafGrabbed;
        if (selectedLeaf == null)
        {
            if (_panelVisible) HideAnimated();
            return;
        }

        UpdateFollowPosition();
        if (!_panelVisible)
            ShowAnimated();
    }

    private void UpdateFollowPosition()
    {
        // Aseguramos que tenemos la hoja (nuestro plan de respaldo)
        Leaf currentLeaf = GrabbableLeafListener.Instance.ActualLeafGrabbed;
        if (currentLeaf == null) return;

        // Obtenemos el ancla (mano o controlador)
        Transform anchor = GrabbableLeafListener.Instance.ActiveSelectionAnchor;

        // FIX PARA HAND TRACKING:
        // Si el ancla es nula o está atorada en el origen (0,0,0), usamos la hoja
        if (anchor == null || anchor.position.sqrMagnitude < 0.001f)
        {
            anchor = currentLeaf.transform;
        }

        // Si por alguna razón extrema sigue siendo nulo, salimos
        if (anchor == null) return;

        // Determinamos el signo lateral para ponerlo a la izquierda o derecha
        float lateralSign = 1f;
        GrabbableLeafListener.SelectionHand hand = GrabbableLeafListener.Instance.ActiveSelectionHand;
        if (hand == GrabbableLeafListener.SelectionHand.Right)
            lateralSign = -1f;

        // Movemos el objeto base (este script) exactamente a la posición de la mano/hoja
        transform.position = anchor.position;

        // Como usas Billboard, solo nos importa el desplazamiento local
        diseaseSelectionPanel.transform.localPosition = positionOffset * lateralSign;
    }

    // ── Logica de submit ─────────────────────────────────────────────────────
    void OnSubmit()
    {
        if (!_panelAvailable)
            return;

        Leaf currentLeaf = GrabbableLeafListener.Instance != null
            ? GrabbableLeafListener.Instance.ActualLeafGrabbed
            : null;

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

        int leafId = currentLeaf.GetInstanceID();

        bool isCorrect = ValidateSelection(currentLeaf, selectedDisease, selectedSeverity, out _);

        // Re-diagnosticar una hoja ya resuelta en este nivel no debe contar como fallo
        // ni como acierto: no se publica el intento (para no distorsionar las métricas)
        // y se muestra feedback positivo, ya que el diagnóstico en sí era correcto.
        if (isCorrect && _identifiedLeavesThisLevel.Contains(leafId))
        {
            Debug.Log("[DiseaseSelection] Esta hoja ya fue contabilizada en este nivel; no se registra el intento.");
            ShowFeedback(true);
            return;
        }

        GameEventBus.PublishDiagnosisAttemptEvaluated(selectedDisease, selectedSeverity, isCorrect);
        ShowFeedback(isCorrect);

        if (isCorrect)
        {
            _identifiedLeavesThisLevel.Add(leafId);

            currentLeaf.ableToShowMarkers = true;
            currentLeaf.showMarkers = true;
            currentLeaf.SetMarkersVisibility(true);

            _correctSelectionsThisLevel++;

            GameEventBus.PublishPlantSelected(true, _correctSelectionsThisLevel, _plantsRequiredThisLevel);

            if (_correctSelectionsThisLevel >= _plantsRequiredThisLevel)
                GameEventBus.PublishAllPlantsSelected();

            ResetSelection();

            // Reproducir sonido
            if (correctSelectionSound != null)
            {
                AudioSource.PlayClipAtPoint(correctSelectionSound, currentLeaf.transform.position);
            }

            // Quema y desactiva la hoja solo si la evaluación fue correcta
            currentLeaf.BurnAndDisable();
        }
        else
        {
            // Reproducir sonido
            if (incorrectSelectionSound != null)
            {
                AudioSource.PlayClipAtPoint(incorrectSelectionSound, currentLeaf.transform.position);
            }
            GameEventBus.PublishPlantSelected(false, _correctSelectionsThisLevel, _plantsRequiredThisLevel);
        }
    }

    // ── Helpers de seleccion ─────────────────────────────────────────────────
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
    bool  ValidateSelection(Leaf leaf, string selectedDisease, int selectedSeverity, out DiseaseSpot matchedSpot)
    {
        matchedSpot = null;
        if (leaf.diseaseSpots == null || leaf.diseaseSpots.Count == 0)
        {
            Debug.LogWarning("La hoja no tiene manchas configuradas");
            return false;
        }

        foreach (DiseaseSpot spot in leaf.diseaseSpots)
        {
            var candidate = new DiagnosisEvaluator.Spot(spot.diseaseName, spot.severity);
            if (DiagnosisEvaluator.Matches(candidate, selectedDisease, selectedSeverity, MarginOfError))
            {
                matchedSpot = spot;
                Debug.Log($"CORRECTO! Enfermedad: {selectedDisease}, Severidad: {selectedSeverity}");
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
            return;
        }

        if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);

        feedbackObject.SetActive(true);
        _feedbackCoroutine = StartCoroutine(HideFeedbackRoutine(feedbackObject, feedbackDuration));
    }

    private System.Collections.IEnumerator HideFeedbackRoutine(GameObject feedbackObject, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (feedbackObject != null)
            feedbackObject.SetActive(false);
    }

    public void ResetSelection()
    {
        if (diseaseToggleGroup != null) diseaseToggleGroup.SetAllTogglesOff();
        if (severityToggleGroup != null) severityToggleGroup.SetAllTogglesOff();
    }

    private void HandleLevelStarted(int levelIndex, int totalLevels, int plantsRequired)
    {
        _correctSelectionsThisLevel = 0;
        _plantsRequiredThisLevel = Mathf.Max(1, plantsRequired);
        _identifiedLeavesThisLevel.Clear();
        _panelAvailable = true;
        ResetSelection();
        Hide();
    }

    void OnDestroy()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnSubmit);
        diseaseSelectionPanel?.transform.DOKill();
        if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
    }
}
