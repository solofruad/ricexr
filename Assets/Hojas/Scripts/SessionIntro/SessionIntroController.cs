using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Un panel de la introducción. Se configuran desde el Inspector, en orden.
/// </summary>
[Serializable]
public class SessionIntroStep
{
    [Tooltip("Solo para reconocer el paso de un vistazo en el Inspector. No se usa en runtime.")]
    public string displayName;

    [Tooltip("UIDocument que renderiza este panel. Si se deja vacío se busca por el nombre del elemento raíz.")]
    public UIDocument document;

    [Tooltip("Nombre del VisualElement raíz dentro del UXML (por ejemplo 'intro-welcome').")]
    public string rootElementName;

    [Tooltip("Segundos que el panel permanece visible. Se ignora si el paso espera al jugador.")]
    public float duration = 6f;

    [Tooltip("Id de línea de narración (ver GameNarrationLineIds). Vacío = panel sin voz.")]
    public string narrationLineId;

    [Tooltip("Si está marcado, el panel no avanza solo: espera a que el jugador pulse el botón.")]
    public bool waitForPlayer;
}

/// <summary>
/// INTRODUCCIÓN DE SESIÓN
///
/// Secuencia de paneles que se muestra una sola vez por sesión, entre el menú principal
/// y la selección de plano. Sirve para contarle al jugador qué es Rice XR antes de
/// soltarlo en el entorno.
///
/// Los paneles se encadenan solos por duración; el último espera a que el jugador pulse
/// el botón de "listo". Quien decide cuándo arranca todo esto es GameFlowController: aquí
/// solo vive la presentación.
///
/// La narración no se invoca directamente. Cada panel publica su id de línea al
/// GameEventBus y GameNarrationController decide si suena y con qué clip.
/// </summary>
[DisallowMultipleComponent]
public class SessionIntroController : MonoBehaviour
{
    [Header("Paneles (en orden de aparición)")]
    [SerializeField] private List<SessionIntroStep> steps = new List<SessionIntroStep>();

    [Header("Botón de continuar")]
    [Tooltip("UIDocument del botón. Aparece solo en el paso marcado como 'waitForPlayer'.")]
    [SerializeField] private UIDocument readyButtonDocument;
    [SerializeField] private string readyButtonElementName = "intro-ready-button";

    [Header("Comportamiento")]
    [Tooltip("Busca los UIDocument por el nombre de su elemento raíz cuando no están asignados.")]
    [SerializeField] private bool autoFindDocuments = true;

    [Header("Tiempos")]
    [Tooltip("Margen antes del primer panel. El menú principal tarda ~0.3 s en desvanecerse.")]
    [SerializeField] private float startDelay = 0.35f;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [Tooltip("Segundos máximos esperando el botón final. 0 = esperar indefinidamente.")]
    [SerializeField] private float readyTimeout = 0f;

    [Header("Posicionamiento")]
    [Tooltip("Altura sobre el plano seleccionado, cuando ya hay uno de una sesión anterior.")]
    [SerializeField] private float surfaceHeightOffset = 0.3f;
    [Tooltip("Distancia frente a la cámara cuando todavía no hay plano.")]
    [SerializeField] private float distanceFromCamera = 1f;

    // ── Estado interno ───────────────────────────────────────────────────────
    private readonly List<VisualElement> _stepRoots = new List<VisualElement>();
    private VisualElement _readyButtonRoot;

    private bool _sequenceRunning;
    private bool _completionTriggered;
    private bool _waitingForPlayer;
    private bool _readyClicked;
    private Action _onSequenceCompleted;

    private Coroutine _sequenceRoutine;
    private Tween _panelFadeTween;
    private Tween _buttonFadeTween;

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        ResolveDocuments();
        CacheElements();
        ResetState();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    // ─────────────────────────────────────────────
    // API pública
    // ─────────────────────────────────────────────

    /// <summary>Indica si la secuencia está en marcha.</summary>
    public bool IsRunning => _sequenceRunning;

    /// <summary>
    /// Muestra la introducción completa y llama a <paramref name="onCompleted"/> al terminar.
    /// Si no hay nada configurado, el callback se invoca igualmente: la intro nunca debe
    /// dejar el flujo de juego colgado.
    /// </summary>
    public void ShowSequence(Action onCompleted)
    {
        if (_sequenceRunning)
        {
            Debug.LogWarning("[SessionIntro] Ya hay una secuencia en marcha. Se ignora la petición.");
            return;
        }

        ResolveDocuments();
        CacheElements();
        ResetState();

        if (steps == null || steps.Count == 0)
        {
            Debug.LogWarning("[SessionIntro] No hay paneles configurados. Se omite la introducción.");
            onCompleted?.Invoke();
            return;
        }

        _sequenceRunning = true;
        _onSequenceCompleted = onCompleted;

        RepositionPanels();
        _sequenceRoutine = StartCoroutine(RunSequence());
    }

    /// <summary>
    /// Responde al botón "¡Estoy listo/a!". Se conecta desde el Inspector mediante el
    /// InteractableUnityEventWrapper del collider, igual que el botón de continuar del
    /// LevelIntroController.
    /// </summary>
    public void HandleReadyClicked()
    {
        if (!_sequenceRunning || !_waitingForPlayer) return;
        _readyClicked = true;
    }

    /// <summary>
    /// Deja la introducción lista para volver a usarse: corta animaciones, oculta los
    /// paneles y olvida el callback pendiente.
    /// </summary>
    public void ResetState()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        KillTweens();

        _sequenceRunning = false;
        _completionTriggered = false;
        _waitingForPlayer = false;
        _readyClicked = false;
        _onSequenceCompleted = null;

        HideAllPanels();
    }

    // ─────────────────────────────────────────────
    // Secuencia
    // ─────────────────────────────────────────────

    private IEnumerator RunSequence()
    {
        // El menú principal publica el evento de inicio antes de arrancar su propio
        // fade-out, así que esperamos un poco para no solaparnos con él.
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < steps.Count; i++)
        {
            SessionIntroStep step = steps[i];
            VisualElement root = i < _stepRoots.Count ? _stepRoots[i] : null;

            if (step == null || root == null)
            {
                Debug.LogWarning($"[SessionIntro] El paso {i} no tiene panel resoluble ('{step?.rootElementName}'). Se salta.");
                continue;
            }

            SetVisible(root, step.document);

            // UI Toolkit necesita un frame para resolver el layout antes de animar.
            yield return null;

            GameEventBus.PublishSessionIntroPanelShown(step.narrationLineId);

            yield return FadePanel(root, 0f, 1f, fadeInDuration, Ease.OutCubic);

            if (step.waitForPlayer)
                yield return WaitForPlayer(step.duration);
            else
                yield return new WaitForSeconds(Mathf.Max(0.5f, step.duration));

            yield return FadePanel(root, root.resolvedStyle.opacity, 0f, fadeOutDuration, Ease.InQuad);

            SetHidden(root);
        }

        CompleteSequence();
    }

    /// <summary>
    /// Muestra el botón final y espera a que el jugador lo pulse.
    ///
    /// Si el botón no está configurado no tiene sentido esperar un clic que nunca podrá
    /// llegar, así que el panel avanza por tiempo usando <paramref name="fallbackDuration"/>.
    /// El timeout, cuando está activado, es solo una red de seguridad para que la sesión
    /// no quede bloqueada.
    /// </summary>
    private IEnumerator WaitForPlayer(float fallbackDuration)
    {
        if (_readyButtonRoot == null)
        {
            Debug.LogWarning("[SessionIntro] No hay botón de continuar configurado. El último panel avanzará por tiempo.");
            yield return new WaitForSeconds(Mathf.Max(0.5f, fallbackDuration));
            yield break;
        }

        _waitingForPlayer = true;
        _readyClicked = false;

        SetVisible(_readyButtonRoot, readyButtonDocument);
        yield return null;
        yield return FadeButton(0f, 1f, fadeInDuration, Ease.OutCubic);

        float waited = 0f;
        while (!_readyClicked)
        {
            if (readyTimeout > 0f && waited >= readyTimeout)
            {
                Debug.LogWarning("[SessionIntro] Se agotó la espera del botón. Se continúa automáticamente.");
                break;
            }

            waited += Time.deltaTime;
            yield return null;
        }

        _waitingForPlayer = false;

        yield return FadeButton(_readyButtonRoot.resolvedStyle.opacity, 0f, fadeOutDuration, Ease.InQuad);
        SetHidden(_readyButtonRoot);
    }

    private void CompleteSequence()
    {
        if (_completionTriggered) return;

        _completionTriggered = true;
        _sequenceRunning = false;
        _sequenceRoutine = null;

        HideAllPanels();

        // Se limpia la referencia antes de invocar para que un reinicio disparado desde
        // el propio callback no vuelva a ejecutarlo.
        Action callback = _onSequenceCompleted;
        _onSequenceCompleted = null;
        callback?.Invoke();
    }

    // ─────────────────────────────────────────────
    // Resolución de documentos
    // ─────────────────────────────────────────────

    private void ResolveDocuments()
    {
        if (!autoFindDocuments) return;

        bool needsSearch = readyButtonDocument == null && !string.IsNullOrEmpty(readyButtonElementName);
        if (steps != null)
        {
            foreach (SessionIntroStep step in steps)
            {
                if (step != null && step.document == null && !string.IsNullOrEmpty(step.rootElementName))
                {
                    needsSearch = true;
                    break;
                }
            }
        }

        if (!needsSearch) return;

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UIDocument document in documents)
        {
            if (document == null || document.rootVisualElement == null) continue;

            if (steps != null)
            {
                foreach (SessionIntroStep step in steps)
                {
                    if (step == null || step.document != null || string.IsNullOrEmpty(step.rootElementName)) continue;
                    if (document.rootVisualElement.Q<VisualElement>(step.rootElementName) != null)
                        step.document = document;
                }
            }

            if (readyButtonDocument == null
                && !string.IsNullOrEmpty(readyButtonElementName)
                && document.rootVisualElement.Q<VisualElement>(readyButtonElementName) != null)
            {
                readyButtonDocument = document;
            }
        }
    }

    private void CacheElements()
    {
        _stepRoots.Clear();

        if (steps != null)
        {
            foreach (SessionIntroStep step in steps)
            {
                if (step == null)
                {
                    _stepRoots.Add(null);
                    continue;
                }

                ActivateDocumentForCache(step.document);
                _stepRoots.Add(step.document?.rootVisualElement?.Q<VisualElement>(step.rootElementName));
            }
        }

        ActivateDocumentForCache(readyButtonDocument);
        _readyButtonRoot = readyButtonDocument?.rootVisualElement?.Q<VisualElement>(readyButtonElementName);
    }

    /// <summary>
    /// El rootVisualElement de un UIDocument solo existe si su GameObject está activo.
    /// Por eso los paneles se ocultan con display/opacity y nunca desactivando el objeto.
    /// </summary>
    private static void ActivateDocumentForCache(UIDocument document)
    {
        if (document == null) return;
        if (!document.gameObject.activeSelf) document.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────
    // Posicionamiento
    // ─────────────────────────────────────────────

    /// <summary>
    /// Coloca el contenedor una vez al arrancar la secuencia. La orientación la resuelve
    /// el componente BillboardUI del mismo GameObject.
    /// </summary>
    private void RepositionPanels()
    {
        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            transform.position = SceneInteractionManager.Instance.SelectedPlanePosition
                                 + SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up * surfaceHeightOffset;
            return;
        }

        if (Camera.main == null)
        {
            Debug.LogWarning("[SessionIntro] No hay Camera.main. Los paneles se quedan donde estén.");
            return;
        }

        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward;
        forward.y = 0f;

        // Mirando recto arriba o abajo el forward proyectado se anula; en ese caso
        // usamos el original para no dividir por cero.
        if (forward.sqrMagnitude < 0.0001f) forward = cam.forward;

        transform.position = cam.position + forward.normalized * distanceFromCamera;
    }

    // ─────────────────────────────────────────────
    // Visibilidad y fades
    // ─────────────────────────────────────────────

    private static void SetVisible(VisualElement element, UIDocument document)
    {
        if (element == null) return;
        if (document != null && !document.gameObject.activeSelf)
            document.gameObject.SetActive(true);

        element.style.display = DisplayStyle.Flex;
        element.style.opacity = 0f;
    }

    private static void SetHidden(VisualElement element)
    {
        if (element == null) return;
        element.style.display = DisplayStyle.None;
        element.style.opacity = 0f;
    }

    private void HideAllPanels()
    {
        foreach (VisualElement root in _stepRoots)
            SetHidden(root);

        SetHidden(_readyButtonRoot);
    }

    private IEnumerator FadePanel(VisualElement element, float from, float to, float duration, Ease ease)
    {
        _panelFadeTween?.Kill();
        _panelFadeTween = BuildFadeTween(element, from, to, duration, ease);
        yield return WaitForTween(_panelFadeTween);
    }

    private IEnumerator FadeButton(float from, float to, float duration, Ease ease)
    {
        _buttonFadeTween?.Kill();
        _buttonFadeTween = BuildFadeTween(_readyButtonRoot, from, to, duration, ease);
        yield return WaitForTween(_buttonFadeTween);
    }

    /// <summary>
    /// UI Toolkit no tiene CanvasGroup, así que se anima una variable local y se vuelca
    /// en style.opacity en cada paso del tween.
    /// </summary>
    private Tween BuildFadeTween(VisualElement element, float from, float to, float duration, Ease ease)
    {
        if (element == null) return null;

        float opacity = from;
        element.style.opacity = from;

        return DOTween.To(
                () => opacity,
                value => { opacity = value; element.style.opacity = value; },
                to, duration)
            .SetUpdate(true)
            .SetEase(ease)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    /// <summary>
    /// Espera a que el tween termine. Se comprueba IsActive porque un Kill no dispara
    /// OnComplete y dejaría la corrutina esperando para siempre.
    /// </summary>
    private static IEnumerator WaitForTween(Tween tween)
    {
        if (tween == null) yield break;
        while (tween.IsActive() && !tween.IsComplete())
            yield return null;
    }

    private void KillTweens()
    {
        _panelFadeTween?.Kill();
        _buttonFadeTween?.Kill();
        _panelFadeTween = null;
        _buttonFadeTween = null;
    }
}
