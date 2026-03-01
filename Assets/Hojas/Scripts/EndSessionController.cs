using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
 [Tooltip("Campo de texto donde el jugador escribe su apodo al final")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("Botón guardar")]
    [SerializeField] private Button saveButton;
    [SerializeField] private TextMeshProUGUI saveButtonLabel;

    // Guarda el resultado pendiente hasta que el usuario ingrese el apodo
    private SessionMetricsTracker.SessionResult _pendingResult;

    // ?? Lifecycle ????????????????????????????????????????????????????????
    void Awake()
    {
     if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
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

        // Limpiar campo de apodo
  if (nicknameInput != null) nicknameInput.text = "";
        if (saveButtonLabel != null) saveButtonLabel.text = "Guardar";
    }

    // ?? Privados ?????????????????????????????????????????????????????????

    private void FillUI(SessionMetricsTracker.SessionResult result)
    {
    if (congratsLabel  != null)
            congratsLabel.text = "¡Felicitaciones!\n¡Has completado todas las pruebas!";

        if (result == null) return;

 if (totalTimeLabel  != null)
            totalTimeLabel.text  = $"Tiempo total: {FormatTime(result.totalTimeSeconds)}";

if (failuresLabel   != null)
         failuresLabel.text   = $"Total de fallos: {result.totalFailures}";

        if (avgTimeLabel    != null)
     avgTimeLabel.text    = $"Tiempo promedio por prueba: {FormatTime(result.averageTimePerLevel)}";

        if (totalLevelsLabel != null)
      totalLevelsLabel.text = $"Pruebas completadas: {result.totalLevels}";

        // Llenar filas de detalle por nivel
        FillLevelDetails(result.levels);
    }

    private void FillLevelDetails(List<SessionMetricsTracker.LevelMetrics> levels)
    {
        if (levelDetailContainer == null || levelDetailRowPrefab == null) return;

        // Limpiar filas anteriores
        foreach (Transform child in levelDetailContainer)
            Destroy(child.gameObject);

if (levels == null) return;

        foreach (var lm in levels)
      {
        GameObject row = Instantiate(levelDetailRowPrefab, levelDetailContainer);
        TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>(true);

          // [0] Nivel  [1] Tiempo  [2] Fallos
            if (texts.Length > 0) texts[0].text = $"Prueba {lm.levelIndex + 1}";
         if (texts.Length > 1) texts[1].text = FormatTime(lm.timeToComplete);
  if (texts.Length > 2) texts[2].text = $"{lm.failCount} fallo{(lm.failCount != 1 ? "s" : "")}";
        }
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
  }
        else
        {
         Debug.LogWarning("[EndSession] No se pudo guardar la sesión.");
            if (saveButtonLabel != null) saveButtonLabel.text = "Error al guardar";
        }
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
