using System;
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
/// Todos los eventos se publican al GameEventBus para que los listeners
/// (narración, analytics, etc.) los escuchen sin acoplamiento directo.
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
    /// <summary>
    /// Referencia unica del controlador (patron singleton).
    /// La usamos para acceder a este script desde cualquier lado sin buscar en la escena.
    /// </summary>
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

    /// <summary>Resultado de la sesion que estamos esperando guardar (con el apodo incluido).</summary>
    private SessionMetricsTracker.SessionResult _pendingResult;
    /// <summary>Escala original del panel de fin, la guardamos para poder animarla despues.</summary>
    private Vector3 _originalScale = Vector3.one;
    /// <summary>Escala original del menu principal, para animarlo de vuelta al salir.</summary>
    private Vector3 _menuOriginalScale = Vector3.one;
    /// <summary>Referencia a la corrutina de regreso al menu, por si hay que cancelarla.</summary>
    private Coroutine _returnToMenuRoutine;

    
    /// <summary>
    /// Se ejecuta al despertar el objeto. Si ya existe otra copia en la escena,
    /// destruimos la nueva para no romper el singleton.
    /// </summary>
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Cuando el objeto se activa, nos suscribimos al evento que avisa
    /// que todos los niveles fueron completados.
    /// </summary>
    void OnEnable()
    {
        GameEventBus.OnAllLevelsCompleted += HandleAllLevelsCompleted;
    }

    /// <summary>
    /// Al desactivarse nos desuscribimos del evento y, si la corrutina de volver
    /// al menu estaba en curso, la cortamos para no dejar nada colgado.
    /// </summary>
    void OnDisable()
    {
        GameEventBus.OnAllLevelsCompleted -= HandleAllLevelsCompleted;
        if (_returnToMenuRoutine != null)
        {
            StopCoroutine(_returnToMenuRoutine);
            _returnToMenuRoutine = null;
        }
    }

    /// <summary>
    /// Preparacion inicial: guardamos la escala original del panel (para poder
    /// animarla mas adelante), escondemos el panel y conectamos el boton de guardar.
    /// </summary>
    void Start()
    {
        if (endPanel != null)
        {
            // Guardamos la escala normal antes de esconderlo, asi la animacion sabe a que tamano volver
            _originalScale = endPanel.transform.localScale;
            endPanel.SetActive(false);
        }
        
        if (mainMenuPanel != null)
        {
            // Igual que arriba, pero para el menu principal
            _menuOriginalScale = mainMenuPanel.transform.localScale;
        }

        if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
    }

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
        GameEventBus.PublishEndPanelShown();

        if (nicknameInput != null)
        {
            nicknameInput.text = "";

            // Abre el teclado virtual de Meta Quest automaticamente al llegar al panel,
            // sin necesidad de que el jugador presione el campo para que aparezca.
            nicknameInput.Select();
            nicknameInput.ActivateInputField();
        }
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


    /// <summary>
    /// Respuesta al evento de "todos los niveles completados":
    /// colocamos el panel delante de la camara y lo mostramos con animacion.
    /// </summary>
    private void HandleAllLevelsCompleted()
    {
        PlaceInFrontOfCamera();
        ShowAnimated();
    }

    /// <summary>
    /// Pone el panel flotando frente a la camara, a la distancia configurada,
    /// y lo gira para que siempre nos mire de frente.
    /// </summary>
    private void PlaceInFrontOfCamera()
    {
        if (endPanel == null || Camera.main == null) return;

        Transform cam = Camera.main.transform;

        // Tomamos la direccion hacia donde mira la camara pero "aplanada"
        // (sin componente vertical) para que el panel no quede inclinado
        Vector3 flatForward = cam.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = cam.forward;
        flatForward.Normalize();

        Vector3 worldPos = cam.position + flatForward * distanceFromCamera + positionOffset;
        endPanel.transform.position = worldPos;

        // El panel mira hacia la camara (por eso invertimos la direccion)
        Vector3 toCam = cam.position - worldPos;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 0.0001f)
            endPanel.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
    }

    /// <summary>
    /// Rellena todos los textos del panel con los datos de la sesion:
    /// tiempo total, fallos, promedio y el detalle por nivel.
    /// </summary>
    private void FillUI(SessionMetricsTracker.SessionResult result)
    {
        if (congratsLabel != null)
            congratsLabel.text = "¡Felicitaciones!\n¡Has completado todas las pruebas!";

        // Si no hay resultado (por ejemplo, si no hay tracker), mostramos solo la felicitacion
        if (result == null) return;

        if (totalTimeLabel != null) totalTimeLabel.text = FormatTime(result.totalTimeSeconds);
        if (failuresLabel != null) failuresLabel.text = $"{result.totalFailures}";
        if (avgTimeLabel != null) avgTimeLabel.text = FormatTime(result.averageTimePerLevel);
        if (totalLevelsLabel != null) totalLevelsLabel.text = $"{result.totalLevels}";

        FillLevelDetails(result.levels);
    }

    /// <summary>
    /// Genera una fila por nivel dentro del contenedor de detalle.
    /// Primero limpia las filas viejas y luego crea una nueva por cada nivel.
    /// </summary>
    private void FillLevelDetails(List<SessionMetricsTracker.LevelMetrics> levels)
    {
        if (levelDetailContainer == null || levelDetailRowPrefab == null) return;

        // Borramos las filas anteriores para no duplicar contenido
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

    /// <summary>
    /// Busca un TextMeshProUGUI por nombre (donde sea dentro del prefab de la fila)
    /// y le asigna el texto. Si no lo encuentra, avisa por consola pero no rompe nada.
    /// </summary>
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

    /// <summary>
    /// Busqueda recursiva: recorre el arbol de hijos hasta encontrar un objeto
    /// con el nombre indicado. Devuelve null si no existe.
    /// </summary>
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

    /// <summary>
    /// Al tocar "Guardar": toma el apodo, lo junta con el resultado de la sesion
    /// y lo manda a guardar. Si sale bien, avisamos y volvemos al menu;
    /// si falla, mostramos un mensaje de error y el jugador puede reintentar.
    /// </summary>
    private void OnSaveClicked()
    {
        if (_pendingResult == null)
        {
            Debug.LogWarning("[EndSession] No hay resultado pendiente para guardar.");
            return;
        }

        // Si el jugador no escribio nada, usamos un nombre por defecto
        string nickname = nicknameInput != null ? nicknameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(nickname)) nickname = "Anonimo";
        _pendingResult.nickname = nickname;

        bool saved = SessionDataSaver.Instance != null && SessionDataSaver.Instance.SaveSession(_pendingResult);

        if (saved)
        {
            Debug.Log($"[EndSession] Datos guardados para: {nickname}");
            if (saveButtonLabel != null) saveButtonLabel.text = "¡Guardado!";
            saveButton.interactable = false;
            GameEventBus.PublishSaveCompleted(true);
            GameEventBus.PublishSessionEnded();
            ReturnToMenu();
        }
        else
        {
            Debug.LogWarning("[EndSession] No se pudo guardar la sesion.");
            if (saveButtonLabel != null) saveButtonLabel.text = "Error al guardar";
            GameEventBus.PublishSaveCompleted(false);
        }
    }

    /// <summary>
    /// Inicia el proceso de regreso al menu: avisa por el bus de eventos
    /// y lanza la corrutina que hace la transicion animada.
    /// </summary>
    private void ReturnToMenu()
    {
        GameEventBus.PublishReturningToMenu();

        // Por si ya habia una corrutina en marcha, la reiniciamos
        if (_returnToMenuRoutine != null) StopCoroutine(_returnToMenuRoutine);
        _returnToMenuRoutine = StartCoroutine(ReturnToMenuRoutine());
    }

    /// <summary>
    /// Corrutina que anima el cierre: espera un segundo y medio para que el
    /// jugador vea el "¡Guardado!", encoge el panel y muestra el menu principal.
    /// </summary>
    private System.Collections.IEnumerator ReturnToMenuRoutine()
    {
        // Pequeña pausa para que se aprecie el mensaje de exito
        yield return new WaitForSeconds(1.2f);

        // Encogemos el endPanel hasta cero y lo desactivamos al terminar
        endPanel.transform.DOKill();
        endPanel.transform
            .DOScale(Vector3.zero, animDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                endPanel.SetActive(false);

                // Dejamos todo listo para la proxima sesion
                if (saveButton != null) saveButton.interactable = true;
                if (saveButtonLabel != null) saveButtonLabel.text = "Guardar";
                _pendingResult = null;

                GameEventBus.PublishReturnToMenuRequested();
                ShowMainMenu();
            });
    }

    /// <summary>
    /// Muestra el menu principal con la misma animacion de escala que el panel
    /// de fin y, cuando termina, refresca la tabla de posiciones.
    /// </summary>
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
                // Refrescamos el leaderboard para que aparezca la partida recien guardada
                if (leaderboardController != null)
                    leaderboardController.Populate();
            });
    }

    /// <summary>
    /// Convierte segundos en texto legible: "1m 30s" si hay mas de un minuto,
    /// o simplemente "45s" si no llega al minuto.
    /// </summary>
    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return m > 0 ? $"{m}m {s:00}s" : $"{s}s";
    }

    /// <summary>
    /// desenganchamos el boton y cortamos cualquier animacion
    /// de DOTween que estuviera corriendo para evitar errores al destruir.
    /// </summary>
    void OnDestroy()
    {
        if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveClicked);
        endPanel?.transform.DOKill();
        mainMenuPanel?.transform.DOKill();
    }
}