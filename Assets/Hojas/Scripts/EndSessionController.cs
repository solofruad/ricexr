using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// PANEL DE FIN DE SESION
///
/// Se activa automaticamente cuando se publica OnAllLevelsCompleted en GameEventBus.
/// Flujo del panel:
///   1. Muestra un mensaje de felicitaciones animado
///   2. Muestra las metricas obtenidas en la sesion (tiempo, fallos, promedio)
///   3. Pide al usuario que ingrese un apodo
///   4. Al presionar "Guardar", guarda todo en el JSON y cierra el panel
///
/// Como configurar en Unity:
///   - endPanel:              el panel raiz (desactivado al inicio)
///   - congratsLabel:         texto de felicitaciones
///   - totalTimeLabel:        muestra el tiempo total
///   - failuresLabel:         muestra el total de fallos
///   - avgTimeLabel:          muestra el tiempo promedio por nivel
///   - levelDetailContainer:  padre de las filas de detalle por nivel
///   - levelDetailRowPrefab:  prefab con al menos 3 TextMeshProUGUI (nivel / tiempo / fallos)
///   - nicknameInput:         InputField donde el jugador escribe su apodo
///   - saveButton:            boton de guardar
/// </summary>
public class EndSessionController : MonoBehaviour
{
    public static EndSessionController Instance { get; private set; }

    [Header("Panel raiz")]
    [SerializeField] private GameObject endPanel;

    [Header("Posicionamiento en mundo")]
    [Tooltip("Desplazamiento sobre el plano elegido. Y = altura sobre la superficie")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private float distanceFromCamera = 1.6f;

    [Header("Textos de resumen")]
    [SerializeField] private TextMeshProUGUI congratsLabel;
    [SerializeField] private TextMeshProUGUI totalTimeLabel;
    [SerializeField] private TextMeshProUGUI failuresLabel;
    [SerializeField] private TextMeshProUGUI avgTimeLabel;
    [SerializeField] private TextMeshProUGUI totalLevelsLabel;

    [Header("Detalle por nivel (opcional)")]
    [SerializeField] private Transform levelDetailContainer;
    [SerializeField] private GameObject levelDetailRowPrefab;

    [Header("Input de apodo")]
    [Tooltip("Campo de texto Legacy (InputField) donde el jugador escribe su apodo al final")]
    [SerializeField] private InputField nicknameInput;

    [Header("Boton guardar")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Text saveButtonLabel;

    [Header("Referencias post-guardado")]
    [Tooltip("Panel del menu principal para volver a mostrarlo al terminar")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("LeaderboardController para refrescar la lista al volver al menu")]
    [SerializeField] private LeaderboardController leaderboardController;

    [Header("Animacion")]
    [SerializeField] private float animDuration = 0.3f;

    private SessionMetricsTracker.SessionResult _pendingResult;
    private Vector3 _originalScale = Vector3.one;
    private Vector3 _menuOriginalScale = Vector3.one;

    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        GameEventBus.OnAllLevelsCompleted += HandleAllLevelsCompleted;
    }

    void OnDisable()
    {
        GameEventBus.OnAllLevelsCompleted -= HandleAllLevelsCompleted;
    }

    void Start()
    {
        if (endPanel != null)
        {
            _originalScale = endPanel.transform.localScale;
            endPanel.SetActive(false);
        }
        
        if (mainMenuPanel != null)
        {
            _menuOriginalScale = mainMenuPanel.transform.localScale;
        }

        if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
    }

    // ── API publica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Recoge metricas, muestra el panel y espera el apodo del usuario.
    /// </summary>
    public void ShowEndPanel()
    {
        if (endPanel == null)
        {
            Debug.LogWarning("[EndSession] endPanel no asignado.");
            return;
        }

        _pendingResult = SessionMetricsTracker.Instance != null
            ? SessionMetricsTracker.Instance.EndSession("")
            : null;

        FillUI(_pendingResult);
        endPanel.SetActive(true);

        if (nicknameInput != null) nicknameInput.text = "";
        if (saveButtonLabel != null) saveButtonLabel.text = "Guardar";
    }

    /// <summary>
    /// Posiciona el endPanel sobre el plano elegido y lo orienta hacia la camara.
    /// Llamar justo antes de ShowAnimated().
    /// </summary>
    public void PlaceAt(Vector3 planePosition, Vector3 planeNormal)
    {
        if (endPanel == null) return;

        Vector3 worldPos = planePosition
            + Vector3.up * positionOffset.y
            + planeNormal * positionOffset.z
            + new Vector3(positionOffset.x, 0f, 0f);

        endPanel.transform.position = worldPos;

        Vector3 toCam = Camera.main != null
            ? Camera.main.transform.position - worldPos
            : Vector3.forward;
        toCam.y = 0f;
        if (toCam != Vector3.zero)
            endPanel.transform.rotation = Quaternion.LookRotation(-toCam);
    }

    /// <summary>Muestra el panel de fin con animacion de escala.</summary>
    public void ShowAnimated()
    {
        ShowEndPanel();
        if (endPanel == null) return;

        endPanel.transform.DOKill();
        endPanel.transform.localScale = Vector3.zero;
        endPanel.transform.DOScale(_originalScale, animDuration).SetEase(Ease.OutBack);
    }

    // ── Privados ─────────────────────────────────────────────────────────────

    private void HandleAllLevelsCompleted()
    {
        PlaceInFrontOfCamera();
        ShowAnimated();
    }

    private void PlaceInFrontOfCamera()
    {
        if (endPanel == null || Camera.main == null) return;

        Transform cam = Camera.main.transform;
        Vector3 flatForward = cam.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = cam.forward;
        flatForward.Normalize();

        Vector3 worldPos = cam.position + flatForward * distanceFromCamera + positionOffset;
        endPanel.transform.position = worldPos;

        Vector3 toCam = cam.position - worldPos;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 0.0001f)
            endPanel.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
    }

    private void FillUI(SessionMetricsTracker.SessionResult result)
    {
        if (congratsLabel != null)
            congratsLabel.text = "¡Felicitaciones!\n¡Has completado todas las pruebas!";

        if (result == null) return;

        if (totalTimeLabel != null) totalTimeLabel.text = FormatTime(result.totalTimeSeconds);
        if (failuresLabel != null) failuresLabel.text = $"{result.totalFailures}";
        if (avgTimeLabel != null) avgTimeLabel.text = FormatTime(result.averageTimePerLevel);
        if (totalLevelsLabel != null) totalLevelsLabel.text = $"{result.totalLevels}";

        FillLevelDetails(result.levels);
    }

    private void FillLevelDetails(List<SessionMetricsTracker.LevelMetrics> levels)
    {
        if (levelDetailContainer == null || levelDetailRowPrefab == null) return;

        foreach (Transform child in levelDetailContainer)
            Destroy(child.gameObject);

        if (levels == null) return;

        foreach (var lm in levels)
        {
            GameObject row = Instantiate(levelDetailRowPrefab, levelDetailContainer);
            SetText(row, "LevelText", $"Prueba {lm.levelIndex + 1}");
            SetText(row, "TimeText", FormatTime(lm.timeToComplete));
            SetText(row, "FailsText", $"{lm.failCount} fallo{(lm.failCount != 1 ? "s" : "")}");
        }
    }

    private void SetText(GameObject root, string objectName, string value)
    {
        Transform found = FindDeep(root.transform, objectName);
        if (found == null)
        {
            Debug.LogWarning($"[EndSession] No se encontro '{objectName}' en el prefab de fila.");
            return;
        }
        TextMeshProUGUI tmp = found.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = value;
    }

    private Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void OnSaveClicked()
    {
        if (_pendingResult == null)
        {
            Debug.LogWarning("[EndSession] No hay resultado pendiente para guardar.");
            return;
        }

        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(nickname)) nickname = "Anonimo";
        _pendingResult.nickname = nickname;

        bool saved = SessionDataSaver.Instance != null && SessionDataSaver.Instance.SaveSession(_pendingResult);

        if (saved)
        {
            Debug.Log($"[EndSession] Datos guardados para: {nickname}");
            if (saveButtonLabel != null) saveButtonLabel.text = "¡Guardado!";
            saveButton.interactable = false;
            GameEventBus.PublishSessionEnded();
            ReturnToMenu();
        }
        else
        {
            Debug.LogWarning("[EndSession] No se pudo guardar la sesion.");
            if (saveButtonLabel != null) saveButtonLabel.text = "Error al guardar";
        }
    }

    private void ReturnToMenu()
    {
        // Pequeña pausa, luego escala el endPanel a cero y lo desactiva
        endPanel.transform.DOKill();
        endPanel.transform
            .DOScale(Vector3.zero, animDuration)
            .SetEase(Ease.InBack)
            .SetDelay(1.2f)
            .OnComplete(() =>
            {
                endPanel.SetActive(false);

                if (saveButton != null) saveButton.interactable = true;
                if (saveButtonLabel != null) saveButtonLabel.text = "Guardar";
                _pendingResult = null;

                SceneInteractionManager.Instance?.PrepareForNextSession();
                ShowMainMenu();
            });
    }

    private void ShowMainMenu()
    {
        if (mainMenuPanel == null) return;

        mainMenuPanel.transform.DOKill();
        mainMenuPanel.transform.localScale = Vector3.zero;
        mainMenuPanel.SetActive(true);
        mainMenuPanel.transform
            .DOScale(_menuOriginalScale, animDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                if (leaderboardController != null)
                    leaderboardController.Populate();
            });
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return m > 0 ? $"{m}m {s:00}s" : $"{s}s";
    }

    void OnDestroy()
    {
        if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveClicked);
        endPanel?.transform.DOKill();
        mainMenuPanel?.transform.DOKill();
    }
}