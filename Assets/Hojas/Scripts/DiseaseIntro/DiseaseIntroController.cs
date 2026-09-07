using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// PRESENTACION DE LA ENFERMEDAD, EN TRES PANELES
///
/// Antes de que las hojas crezcan, explica al jugador la enfermedad del nivel
/// en tres paneles frente a el, que avanzan con el boton "Continuar":
///
///   1) Portada       — que enfermedad toca (nombre, nombre cientifico, imagen)
///   2) La enfermedad — que es, como se propaga, que buscar en la hoja
///   3) El daño       — como se mide de 1 a 5
///
/// DONDE ENCAJA EN EL FLUJO:
///   GameFlowController publica LevelStarted y deja el flujo esperando en
///   FlowState.WaitingForDiseaseAnalysis. UIGameListener oye ese evento y
///   arranca esta presentacion. Al pulsar "Continuar" en el tercer panel,
///   UIGameListener muestra el panel de consulta y publica
///   DiseaseAnalysisCompleted, que es lo que hace crecer las hojas.
///
///   A proposito NO hay un FlowState nuevo: esto es presentacion, y
///   WaitingForDiseaseAnalysis ya describe el estado. GameFlowController no
///   conoce esta clase (ver AGENTS.md).
///
/// REGLAS DEL PROYECTO QUE RESPETA:
/// - Los UIDocument se ponen en la escena, no se crean por codigo: cada panel trae
///   el suyo con su UXML, su PanelSettings y su tamaño ya configurados, y aqui
///   solo se referencia. Igual que SessionIntroController.
/// - Los tiempos van en corrutinas, nunca en DOVirtual.DelayedCall: eso falla en
///   silencio en Android/IL2CPP, que es la plataforma objetivo (Meta Quest).
/// - El boton no es un ui:Button ni usa 'clicked +='. En este proyecto UI Toolkit
///   solo dibuja: el toque llega por un PokeInteractable +
///   InteractableUnityEventWrapper cableado en el Inspector a
///   HandleContinueClicked, igual que la introduccion de sesion.
/// - Nunca desactiva el GameObject de un UIDocument: sin el no hay
///   rootVisualElement. Se oculta con display/opacity.
/// - El callback se llama SIEMPRE, incluso si falta configuracion: si se lo
///   tragara, el nivel no arrancaria nunca.
/// </summary>
[DisallowMultipleComponent]
public class DiseaseIntroController : MonoBehaviour
{
    // ─── Inspector — Paneles ──────────────────────────────────────────────────

    [Header("Los tres paneles")]
    [Tooltip("Cada panel lleva su propio UIDocument en la escena, con su UXML y su " +
             "tamaño ya configurados. Aqui solo se referencian.")]
    [SerializeField] private DiseaseIntroStepPanel coverPanel;
    [SerializeField] private DiseaseIntroStepPanel infoPanel;
    [SerializeField] private DiseaseIntroStepPanel severityPanel;

    // ─── Inspector — Boton continuar ──────────────────────────────────────────

    [Header("Boton continuar")]
    [SerializeField] private UIDocument continueButtonDocument;
    [SerializeField] private string continueButtonElementName = "disease-intro-continue-button";

    [Tooltip("Objeto que se mueve para colocar el boton. Debe ser el que lleva el " +
             "PokeInteractable, para que el dibujo y la zona que se toca viajen juntos. " +
             "Vacio = se mueve el propio UIDocument.")]
    [SerializeField] private Transform continueButtonRoot;

    [Tooltip("Cuanto baja el boton respecto al centro del panel, en metros.")]
    [SerializeField] private float continueButtonDrop = 0.28f;

    [Tooltip("Segundos maximos esperando el boton. 0 = esperar indefinidamente.")]
    [SerializeField] private float continueTimeout = 0f;

    // ─── Inspector — Colocacion ───────────────────────────────────────────────

    [Header("Colocacion frente al jugador")]
    [Tooltip("Distancia en metros entre la cabeza del jugador y los paneles.")]
    [SerializeField] private float distance = 1.2f;

    [Tooltip("Desplazamiento vertical respecto a la altura de la cabeza.")]
    [SerializeField] private float heightOffset = -0.15f;

    // ─── Inspector — Tiempos ──────────────────────────────────────────────────

    [Header("Tiempos")]
    [SerializeField] private float startDelay = 0.35f;
    [SerializeField] private float panelFadeOutDuration = 0.25f;
    [SerializeField] private float buttonFadeInDuration = 0.3f;
    [SerializeField] private float buttonFadeOutDuration = 0.2f;

    // ─── Estado ───────────────────────────────────────────────────────────────

    public bool IsRunning => _sequenceRunning;

    private VisualElement _continueButtonRoot;
    private Tween _buttonFadeTween;
    private Coroutine _sequenceRoutine;

    private bool _sequenceRunning;
    private bool _completionTriggered;
    private bool _waitingForPlayer;
    private bool _continueClicked;
    private Action _onCompleted;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        _buttonFadeTween?.Kill();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API publica
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Muestra los tres paneles de la enfermedad indicada y llama a
    /// <paramref name="onCompleted"/> cuando el jugador continua en el ultimo.
    /// Si no hay nada configurado, llama al callback igualmente para no dejar el
    /// flujo del juego colgado.
    /// </summary>
    public void ShowIntro(PanelDiseaseData data, Action onCompleted)
    {
        if (_sequenceRunning)
        {
            Debug.LogWarning("[DiseaseIntro] Ya hay una presentacion en curso. Se ignora la peticion.");
            return;
        }

        CacheContinueButton();

        List<Step> steps = BuildSteps(data);
        if (steps.Count == 0)
        {
            Debug.LogWarning("[DiseaseIntro] No hay textos de presentacion para esta enfermedad. Se omite.");
            onCompleted?.Invoke();
            return;
        }

        ResetState();

        _sequenceRunning = true;
        _onCompleted = onCompleted;

        Vector3 anchor = ResolveAnchor();
        PlaceContinueButton(anchor);

        _sequenceRoutine = StartCoroutine(RunSequence(steps, anchor));
    }

    /// <summary>
    /// Responde al boton de continuar. Se cablea desde el Inspector con
    /// InteractableUnityEventWrapper._whenSelect del PokeInteractable.
    /// </summary>
    public void HandleContinueClicked()
    {
        if (!_sequenceRunning || !_waitingForPlayer) return;
        _continueClicked = true;
    }

    /// <summary>
    /// Corta la secuencia y oculta todo. Seguro de llamar en cualquier momento
    /// (cambio de nivel, vuelta al menu a media presentacion).
    /// </summary>
    public void ResetState()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        _buttonFadeTween?.Kill();

        _sequenceRunning = false;
        _completionTriggered = false;
        _waitingForPlayer = false;
        _continueClicked = false;
        _onCompleted = null;

        HideAll();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Secuencia
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator RunSequence(List<Step> steps, Vector3 anchor)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < steps.Count; i++)
        {
            Step step = steps[i];

            step.panel.SetAnchor(anchor);
            step.panel.ShowStep(step.view);

            // UI Toolkit necesita un frame para resolver el layout tras volver
            // visible un panel; hasta entonces la entrada animaria sobre nada.
            yield return null;

            GameEventBus.PublishDiseaseIntroPanelShown(step.narrationLineId);

            yield return step.panel.PlayReveal();
            yield return WaitForContinue();

            step.panel.Hide();
            yield return new WaitForSeconds(panelFadeOutDuration);
        }

        CompleteSequence();
    }

    /// <summary>
    /// Muestra el boton y bloquea hasta que el jugador lo pulsa. El timeout es
    /// una red de seguridad opcional: con 0 se espera indefinidamente, que es lo
    /// deseable en una experiencia guiada.
    /// </summary>
    private IEnumerator WaitForContinue()
    {
        if (_continueButtonRoot == null)
        {
            Debug.LogWarning("[DiseaseIntro] No hay boton de continuar configurado. El panel avanza solo.");
            yield return new WaitForSeconds(2f);
            yield break;
        }

        _waitingForPlayer = true;
        _continueClicked = false;

        SetVisible(_continueButtonRoot, continueButtonDocument);
        yield return null;
        yield return FadeButton(0f, 1f, buttonFadeInDuration, Ease.OutCubic);

        float waited = 0f;
        while (!_continueClicked)
        {
            if (continueTimeout > 0f && waited >= continueTimeout)
            {
                Debug.LogWarning("[DiseaseIntro] Se agoto la espera del boton de continuar. Se avanza igual.");
                break;
            }

            waited += Time.deltaTime;
            yield return null;
        }

        _waitingForPlayer = false;

        yield return FadeButton(_continueButtonRoot.resolvedStyle.opacity, 0f, buttonFadeOutDuration, Ease.InQuad);
        SetHidden(_continueButtonRoot);
    }

    private void CompleteSequence()
    {
        if (_completionTriggered) return;

        _completionTriggered = true;
        _sequenceRunning = false;
        _sequenceRoutine = null;

        HideAll();
        // El callback se limpia ANTES de llamarlo: si desde dentro se arranca
        // otra presentacion, esta no debe volver a dispararse. Mismo patron que
        // SessionIntroController.CompleteSequence.
        Action callback = _onCompleted;
        _onCompleted = null;
        callback?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Armado de los paneles
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Un panel ya resuelto: cual es, que pinta y que linea narra.</summary>
    private class Step
    {
        public DiseaseIntroStepPanel panel;
        public DiseaseIntroStepView view;
        public string narrationLineId;
    }

    /// <summary>
    /// Traduce los datos de la enfermedad a los tres paneles. Un panel sin
    /// componente asignado se omite en vez de romper la secuencia.
    /// </summary>
    private List<Step> BuildSteps(PanelDiseaseData data)
    {
        var steps = new List<Step>(3);
        if (data == null) return steps;

        DiseaseIntroContent content = data.intro;
        if (content == null || !content.HasContent) return steps;

        // Panel 1 — Portada.
        if (coverPanel != null)
        {
            Texture2D hero = content.coverImage != null
                ? content.coverImage
                : (data.images != null && data.images.Length > 0 ? data.images[0] : null);

            steps.Add(new Step
            {
                panel = coverPanel,
                narrationLineId = content.coverNarrationLineId,
                view = new DiseaseIntroStepView
                {
                    eyebrow        = data.category,
                    title          = data.title,
                    scientificName = data.scientificName,
                    body           = content.coverTagline,
                    image          = hero
                }
            });
        }

        // Panel 2 — La enfermedad. Si no hay cuerpo propio se usa la descripcion
        // del panel de consulta, para que un asset a medio llenar siga sirviendo.
        if (infoPanel != null)
        {
            steps.Add(new Step
            {
                panel = infoPanel,
                narrationLineId = content.infoNarrationLineId,
                view = new DiseaseIntroStepView
                {
                    title = content.infoHeadline,
                    body  = string.IsNullOrWhiteSpace(content.infoBody) ? data.description : content.infoBody,
                    image = content.infoImage,
                    rows  = content.infoRows
                }
            });
        }

        // Panel 3 — El daño.
        if (severityPanel != null)
        {
            steps.Add(new Step
            {
                panel = severityPanel,
                narrationLineId = content.severityNarrationLineId,
                view = new DiseaseIntroStepView
                {
                    title         = content.severityHeadline,
                    body          = content.severityBody,
                    severityImage = data.severityEvolutionImage,
                    rows          = content.severityRows
                }
            });
        }

        return steps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Colocacion y documentos
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Posicion comun de los tres paneles: frente a la cabeza del jugador, a la
    /// distancia configurada. Se calcula UNA vez para que no salten entre uno y
    /// otro.
    /// </summary>
    private Vector3 ResolveAnchor()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;

        // Mirando recto arriba o abajo el forward proyectado se anula; se usa el
        // original para no normalizar un vector cero.
        if (forward.sqrMagnitude < 0.0001f) forward = cam.transform.forward;

        Vector3 position = cam.transform.position + forward.normalized * distance;
        position.y += heightOffset;
        return position;
    }

    /// <summary>
    /// Coloca el boton bajo el panel. Se mueve continueButtonRoot y no el
    /// UIDocument, porque lo que el jugador toca es el PokeInteractable: si solo
    /// se moviera el documento, el dibujo y la zona sensible acabarian en sitios
    /// distintos.
    /// </summary>
    private void PlaceContinueButton(Vector3 anchor)
    {
        Transform buttonTransform = continueButtonRoot != null
            ? continueButtonRoot
            : continueButtonDocument != null ? continueButtonDocument.transform : null;

        if (buttonTransform == null) return;

        buttonTransform.position = anchor + Vector3.down * continueButtonDrop;

        // Se deja mirando ya a la cabeza para que no entre girandose. Misma
        // formula que PlayerFacingMessagePanel y BillboardUI en Y-lock.
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 toHead = cam.transform.position - buttonTransform.position;
        toHead.y = 0f;
        if (toHead.sqrMagnitude > 0.0001f)
            buttonTransform.rotation = Quaternion.LookRotation(-toHead, Vector3.up);
    }

    private void CacheContinueButton()
    {
        if (_continueButtonRoot != null || continueButtonDocument == null) return;

        // El rootVisualElement solo existe si el GameObject esta activo, por eso
        // los paneles se ocultan con display/opacity y nunca desactivandolos.
        if (!continueButtonDocument.gameObject.activeSelf)
            continueButtonDocument.gameObject.SetActive(true);

        _continueButtonRoot = continueButtonDocument.rootVisualElement?
            .Q<VisualElement>(continueButtonElementName);

        if (_continueButtonRoot == null)
            Debug.LogWarning($"[DiseaseIntro] No se encontro el boton '{continueButtonElementName}' en su UIDocument.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Visibilidad
    // ─────────────────────────────────────────────────────────────────────────

    private void HideAll()
    {
        coverPanel?.HideImmediate();
        infoPanel?.HideImmediate();
        severityPanel?.HideImmediate();

        coverPanel?.CancelReveal();
        infoPanel?.CancelReveal();
        severityPanel?.CancelReveal();

        SetHidden(_continueButtonRoot);
    }

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

    /// <summary>
    /// UI Toolkit no tiene CanvasGroup: se anima una variable local y se vuelca
    /// en style.opacity. La espera consulta el estado del tween porque un Kill no
    /// dispara OnComplete y dejaria la corrutina colgada.
    /// </summary>
    private IEnumerator FadeButton(float from, float to, float duration, Ease ease)
    {
        _buttonFadeTween?.Kill();
        if (_continueButtonRoot == null) yield break;

        float opacity = from;
        _continueButtonRoot.style.opacity = from;

        _buttonFadeTween = DOTween.To(
                () => opacity,
                value => { opacity = value; _continueButtonRoot.style.opacity = value; },
                to, duration)
            .SetUpdate(true)
            .SetEase(ease)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        Tween tween = _buttonFadeTween;
        while (tween.IsActive() && !tween.IsComplete())
            yield return null;
    }
}
