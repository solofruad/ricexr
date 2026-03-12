using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;  // solo para los labels de resumen y filas de detalle

/// <summary>
/// PANEL DE FIN DE SESIÓN
/// 
/// Se activa automáticamente cuando SceneInteractionManager llama a OnAllLevelsCompleted().
/// Flujo del panel:
///   1. Muestra un mensaje de felicitaciones animado
///   2. Muestra las métricas obtenidas en la sesión (tiempo, fallos, promedio)
/// 3. Pide al usuario que ingrese un apodo
///   4. Al presionar "Guardar", guarda todo en el JSON y cierra el panel
/// 
/// Cómo configurar en Unity:
///   - endPanel:    el panel raíz (desactivado al inicio)
///   - congratsLabel:        texto de felicitaciones
///   - totalTimeLabel: muestra el tiempo total
///- failuresLabel:      muestra el total de fallos
///   - avgTimeLabel:         muestra el tiempo promedio por nivel
///   - levelDetailContainer: padre de las filas de detalle por nivel
///   - levelDetailRowPrefab: prefab con al menos 3 TextMeshProUGUI (nivel / tiempo / fallos)
///   - nicknameInput:        TMP_InputField donde el usuario escribe su apodo
///   - saveButton:      botón de guardar
/// </summary>
public class EndSessionController : MonoBehaviour
{
    public static EndSessionController Instance { get; private set; }

    [Header("Panel raíz")]
    [SerializeField] private GameObject endPanel;

    [Header("Posicionamiento en mundo")]
    [Tooltip("Desplazamiento sobre el plano elegido. Y = altura sobre la superficie")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.3f, 0f);

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

    [Header("Botón guardar")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Text saveButtonLabel;

    [Header("Referencias post-guardado")]
    [Tooltip("Panel del menú principal para volver a mostrarlo al terminar")]
    [SerializeField] private GameObject mainMenuPanel;
    [Tooltip("LeaderboardController para refrescar la lista al volver al menú")]
    [SerializeField] private LeaderboardController leaderboardController;

    [Header("Animación de aparición")]
    [SerializeField] private float animDuration = 0.3f;

    private SessionMetricsTracker.SessionResult _pendingResult;
    private Coroutine _animCoroutine;
    private Vector3 _originalScale;
    private Vector3 _menuOriginalScale;

    // ?? Lifecycle ????????????????????????????????????????????????????????
    void Awake()
    {
     if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Guardar escalas originales antes de ocultar los paneles
        if (endPanel != null)
            _originalScale = endPanel.transform.localScale;
        else
            _originalScale = Vector3.one;

        if (mainMenuPanel != null)
            _menuOriginalScale = mainMenuPanel.transform.localScale;
        else
            _menuOriginalScale = Vector3.one;

        if (endPanel != null) endPanel.SetActive(false);
        if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
    }

    // ?? API pública ???????????????????????????????????????????????????????

    /// <summary>
    /// Llama esto desde SceneInteractionManager.OnAllLevelsCompleted().
    /// Recoge métricas, muestra el panel y espera el apodo del usuario.
    /// </summary>
    public void ShowEndPanel()
    {
        if (endPanel == null)
        {
     Debug.LogWarning("[EndSession] endPanel no asignado.");
     return;
        }

        // Recoger resultado de la sesión (sin apodo todavía)
        _pendingResult = SessionMetricsTracker.Instance != null
  ? SessionMetricsTracker.Instance.EndSession("")
    : null;

        FillUI(_pendingResult);

        endPanel.SetActive(true);
        // La posición la aplica SceneInteractionManager llamando PlaceAt() justo después

        if (nicknameInput != null) nicknameInput.text = "";
        if (saveButtonLabel != null) saveButtonLabel.text = "Guardar";
    }

    /// <summary>
    /// Posiciona el endPanel sobre el plano elegido y lo orienta hacia la cámara.
    /// Llamar justo después de ShowEndPanel() desde SceneInteractionManager.
    /// </summary>
    public void PlaceAt(Vector3 planePosition, Vector3 planeNormal)
    {
        if (endPanel == null) return;

        Vector3 worldPos = planePosition
            + Vector3.up  * positionOffset.y
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

    /// <summary>Muestra el panel de fin con animación de escala (llamar desde SceneInteractionManager).</summary>
    public void ShowAnimated()
    {
        ShowEndPanel();   // llena la UI y activa el panel
        if (endPanel == null) return;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(ScalePanel(Vector3.zero, _originalScale));
    }

    private IEnumerator ScalePanel(Vector3 from, Vector3 to)
    {
        endPanel.transform.localScale = from;
        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / animDuration));
            endPanel.transform.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }
        endPanel.transform.localScale = to;
        _animCoroutine = null;
    }

    private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    // ?? Privados ?????????????????????????????????????????????????????????

    private void FillUI(SessionMetricsTracker.SessionResult result)
    {
    if (congratsLabel  != null)
            congratsLabel.text = "¡Felicitaciones!\n¡Has completado todas las pruebas!";

        if (result == null) return;

 if (totalTimeLabel  != null)
            totalTimeLabel.text  = $"{FormatTime(result.totalTimeSeconds)}";

if (failuresLabel   != null)
         failuresLabel.text   = $"{result.totalFailures}";

        if (avgTimeLabel    != null)
     avgTimeLabel.text    = $"{FormatTime(result.averageTimePerLevel)}";

        if (totalLevelsLabel != null)
      totalLevelsLabel.text = $"{result.totalLevels}";

        // Llenar filas de detalle por nivel
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
            SetText(row, "LevelText",   $"Prueba {lm.levelIndex + 1}");
            SetText(row, "TimeText",    FormatTime(lm.timeToComplete));
            SetText(row, "FailsText",   $"{lm.failCount} fallo{(lm.failCount != 1 ? "s" : "")}");
        }
    }

    /// <summary>Busca un hijo por nombre (recursivo) y asigna el texto TMP.</summary>
    private void SetText(GameObject root, string objectName, string value)
    {
        Transform found = FindDeep(root.transform, objectName);
        if (found == null)
        {
            Debug.LogWarning($"[EndSession] No se encontró '{objectName}' en el prefab de fila.");
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

        // Asignar apodo
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(nickname)) nickname = "Anónimo";
        _pendingResult.nickname = nickname;

        // Guardar en JSON
        bool saved = SessionDataSaver.Instance != null && SessionDataSaver.Instance.SaveSession(_pendingResult);

        if (saved)
        {
            Debug.Log($"[EndSession] Datos guardados para: {nickname}");
            if (saveButtonLabel != null) saveButtonLabel.text = "¡Guardado!";
            saveButton.interactable = false;

            // Ocultar el panel y volver al menú
            StartCoroutine(ReturnToMenu());
        }
        else
        {
            Debug.LogWarning("[EndSession] No se pudo guardar la sesión.");
            if (saveButtonLabel != null) saveButtonLabel.text = "Error al guardar";
        }
    }

    private IEnumerator ReturnToMenu()
    {
        // Pequeña pausa para que el usuario vea "¡Guardado!"
        yield return new WaitForSeconds(1.2f);

        // Ocultar EndPanel con animación
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(ScalePanel(_originalScale, Vector3.zero));
        yield return new WaitForSeconds(animDuration);
        if (endPanel != null) endPanel.SetActive(false);

        // Restaurar el botón para una posible reutilización
        if (saveButton   != null) saveButton.interactable = true;
        if (saveButtonLabel != null) saveButtonLabel.text = "Guardar";
        _pendingResult = null;

        // Volver a mostrar el menú principal
        if (mainMenuPanel != null)
        {
            mainMenuPanel.transform.localScale = Vector3.zero;
            mainMenuPanel.SetActive(true);
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / animDuration), 3f);
                mainMenuPanel.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, _menuOriginalScale, t);
                yield return null;
            }
            mainMenuPanel.transform.localScale = _menuOriginalScale;
        }

        // Refrescar el leaderboard con el nuevo dato
        if (leaderboardController != null)
            leaderboardController.Populate();
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
  }
}
