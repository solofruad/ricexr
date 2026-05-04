using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Secuencia de onboarding inicial mostrada despues de seleccionar el plano.
/// Metodologia: paneles preinstanciados (UIDocument) + show/hide con animaciones.
///
/// Orden:
/// 1) Intro con avance manual o automatico.
/// 2) Mensaje breve de inicio de tutorial.
/// </summary>
[DisallowMultipleComponent]
public class StartupOnboardingController : MonoBehaviour
{
    public event Action IntroPanelShown;
    public event Action IntroContinueRequested;
    public event Action TutorialStartMessageShown;

    [Header("UI Documents - uno por panel")]
    [SerializeField] private UIDocument introDocument;
    [SerializeField] private UIDocument tutorialStartDocument;
    [SerializeField] private bool autoFindDocuments = true;

    [Header("Nombres de raiz")]
    [SerializeField] private string introRootElementName = "panel-intro";
    [SerializeField] private string tutorialStartRootElementName = "msg-tutorial-start";

    [Header("Tiempos")]
    [SerializeField] private float introAutoAdvanceDelay = 14f;
    [SerializeField] private float tutorialStartMessageDuration = 2.6f;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("World Space")]
    [SerializeField] private float distanceFromCamera = 1.5f;
    [SerializeField] private Vector3 baseOffset = Vector3.zero;
    [SerializeField] private Vector3 introPanelOffset = Vector3.zero;
    [SerializeField] private Vector3 tutorialStartPanelOffset = Vector3.zero;
    [SerializeField] private float surfaceHeightOffset = 0.28f;

    private VisualElement _introRoot;
    private Button _introContinueButton;

    private VisualElement _tutorialStartRoot;

    private Tween _introFadeTween;
    private Tween _introAutoAdvanceTween;
    private Tween _tutorialStartFadeTween;
    private Tween _tutorialStartHoldTween;

    private bool _sequenceRunning;
    private bool _introAdvanced;
    private bool _completionTriggered;
    private Action _onSequenceCompleted;

    private void Awake()
    {
        ResolveDocuments();
        CacheElements();
        ResetSequenceState();
    }

    private void OnDestroy()
    {
        KillTweens();
        if (_introContinueButton != null)
            _introContinueButton.clicked -= HandleIntroContinueClicked;
    }

    public void ShowSequence(Action onCompleted)
    {
        ResolveDocuments();
        CacheElements();

        if (_introRoot == null && _tutorialStartRoot == null)
        {
            Debug.LogWarning("[StartupOnboarding] No hay paneles de onboarding listos. Se omite secuencia.");
            onCompleted?.Invoke();
            return;
        }

        if (_sequenceRunning) return;

        ResetSequenceState();

        _sequenceRunning = true;
        _onSequenceCompleted = onCompleted;
        RepositionPanels();

        ShowIntroPanel();
    }

    public void ResetSequenceState()
    {
        KillTweens();

        _sequenceRunning = false;
        _introAdvanced = false;
        _completionTriggered = false;
        _onSequenceCompleted = null;

        SetHidden(_introRoot);
        SetHidden(_tutorialStartRoot);
    }

    /// <summary>
    /// 1. Busca y asigna los UIDocuments de los paneles de onboarding si no fueron asignados manualmente.
    /// 2. Busca y asigna los elementos clave dentro de cada documento (root, botones) y conecta listeners.
    /// 3. Muestra el panel de introducción con una animación de fade-in y programa su avance automático 
    ///     después de un delay configurable.
    /// 4. Al avanzar desde el panel de introducción (ya sea por avance automático o por click en el botón), 
    ///     se muestra el mensaje de inicio de tutorial con otra animación de fade-in, se mantiene visible por 
    ///     un tiempo configurable, y luego se oculta con un fade-out.
    /// 5. Al completar la secuencia, se invoca el callback onCompleted para notificar al manager global que el 
    ///     onboarding ha terminado y se puede iniciar el nivel.
    /// </summary>
    private void ResolveDocuments()
    {
        if (!autoFindDocuments) return;
        if (introDocument != null && tutorialStartDocument != null) return;

        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
        for (int i = 0; i < docs.Length; i++)
        {
            UIDocument doc = docs[i];
            if (doc == null || doc.rootVisualElement == null) continue;

            if (introDocument == null && doc.rootVisualElement.Q<VisualElement>(introRootElementName) != null)
                introDocument = doc;

            if (tutorialStartDocument == null && doc.rootVisualElement.Q<VisualElement>(tutorialStartRootElementName) != null)
                tutorialStartDocument = doc;

            if (introDocument != null && tutorialStartDocument != null)
                break;
        }

        if (introDocument != null && tutorialStartDocument != null && introDocument == tutorialStartDocument)
            Debug.LogWarning("[StartupOnboarding] Intro y MsgTutorialStart comparten el mismo UIDocument. Para evitar solapamientos usa documentos separados.");
    }

    /// <summary>
    /// 1. Busca los elementos clave dentro de cada UIDocument (root, botones) y los asigna a variables locales.
    /// 2. Conecta listeners a los botones para manejar interacciones del usuario (ej. avanzar desde el panel de introducción).
    /// 
    /// </summary>
    private void CacheElements()
    {
        if (_introContinueButton != null)
            _introContinueButton.clicked -= HandleIntroContinueClicked;

        _introRoot = introDocument?.rootVisualElement?.Q<VisualElement>(introRootElementName);
        _tutorialStartRoot = tutorialStartDocument?.rootVisualElement?.Q<VisualElement>(tutorialStartRootElementName);

        _introContinueButton = _introRoot?.Q<Button>("intro-continue-button");
        if (_introContinueButton != null)
            _introContinueButton.clicked += HandleIntroContinueClicked;

        if (introDocument == null)
            Debug.LogWarning("[StartupOnboarding] introDocument no asignado/encontrado.");

        if (tutorialStartDocument == null)
            Debug.LogWarning("[StartupOnboarding] tutorialStartDocument no asignado/encontrado.");

        if (introDocument != null && _introRoot == null)
            Debug.LogWarning($"[StartupOnboarding] No se encontro el root '{introRootElementName}' en introDocument.");

        if (tutorialStartDocument != null && _tutorialStartRoot == null)
            Debug.LogWarning($"[StartupOnboarding] No se encontro el root '{tutorialStartRootElementName}' en tutorialStartDocument.");
    }

    private void ShowIntroPanel()
    {
        if (_introRoot == null)
        {
            ShowTutorialStartPanel();
            return;
        }

        SetVisible(_introRoot);
        IntroPanelShown?.Invoke();
        _introFadeTween?.Kill();
        _introFadeTween = FadeElement(_introRoot, 0f, 1f, fadeInDuration, Ease.OutCubic, null);

        _introAutoAdvanceTween?.Kill();
        _introAutoAdvanceTween = DOVirtual.DelayedCall(Mathf.Max(1f, introAutoAdvanceDelay), AdvanceFromIntro);
    }

    private void HandleIntroContinueClicked()
    {
        IntroContinueRequested?.Invoke();
        AdvanceFromIntro();
    }

    private void AdvanceFromIntro()
    {
        if (!_sequenceRunning || _introAdvanced) return;

        _introAdvanced = true;
        _introAutoAdvanceTween?.Kill();

        if (_introRoot == null)
        {
            ShowTutorialStartPanel();
            return;
        }

        _introFadeTween?.Kill();
        float from = _introRoot.resolvedStyle.opacity;
        _introFadeTween = FadeElement(_introRoot, from, 0f, fadeOutDuration, Ease.InQuad, () =>
        {
            SetHidden(_introRoot);
            ShowTutorialStartPanel();
        });
    }

    private void ShowTutorialStartPanel()
    {
        if (_tutorialStartRoot == null)
        {
            CompleteSequence();
            return;
        }

        SetVisible(_tutorialStartRoot);
        TutorialStartMessageShown?.Invoke();

        _tutorialStartFadeTween?.Kill();
        _tutorialStartFadeTween = FadeElement(_tutorialStartRoot, 0f, 1f, fadeInDuration, Ease.OutCubic, () =>
        {
            _tutorialStartHoldTween?.Kill();
            _tutorialStartHoldTween = DOVirtual.DelayedCall(
                Mathf.Max(0.5f, tutorialStartMessageDuration),
                HideTutorialStartAndComplete);
        });
    }

    private void HideTutorialStartAndComplete()
    {
        if (_tutorialStartRoot == null)
        {
            CompleteSequence();
            return;
        }

        _tutorialStartFadeTween?.Kill();
        float from = _tutorialStartRoot.resolvedStyle.opacity;
        _tutorialStartFadeTween = FadeElement(_tutorialStartRoot, from, 0f, fadeOutDuration, Ease.InQuad, () =>
        {
            SetHidden(_tutorialStartRoot);
            CompleteSequence();
        });
    }

    private void CompleteSequence()
    {
        if (_completionTriggered) return;

        _completionTriggered = true;
        _sequenceRunning = false;

        SetHidden(_introRoot);
        SetHidden(_tutorialStartRoot);

        Action callback = _onSequenceCompleted;
        _onSequenceCompleted = null;
        callback?.Invoke();
    }

    private void RepositionPanels()
    {
        Vector3 target;

        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            target = SceneInteractionManager.Instance.SelectedPlanePosition
                     + (SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up) * surfaceHeightOffset
                     + baseOffset;
        }
        else
        {
            if (Camera.main == null) return;

            Transform cam = Camera.main.transform;
            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;

            target = cam.position + fwd.normalized * distanceFromCamera + baseOffset;
        }

        if (introDocument != null)
            introDocument.transform.position = target + introPanelOffset;

        if (tutorialStartDocument != null)
            tutorialStartDocument.transform.position = target + tutorialStartPanelOffset;
    }

    private static void SetVisible(VisualElement element)
    {
        if (element == null) return;
        element.style.display = DisplayStyle.Flex;
        element.style.opacity = 0f;
    }

    private static void SetHidden(VisualElement element)
    {
        if (element == null) return;
        element.style.display = DisplayStyle.None;
        element.style.opacity = 0f;
    }

    private static Tween FadeElement(VisualElement element, float from, float to, float duration, Ease ease, Action onComplete)
    {
        float opacity = from;
        element.style.opacity = from;

        return DOTween.To(
                () => opacity,
                value =>
                {
                    opacity = value;
                    element.style.opacity = value;
                },
                to,
                duration)
            .SetEase(ease)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void KillTweens()
    {
        _introFadeTween?.Kill();
        _introAutoAdvanceTween?.Kill();
        _tutorialStartFadeTween?.Kill();
        _tutorialStartHoldTween?.Kill();
    }
}
