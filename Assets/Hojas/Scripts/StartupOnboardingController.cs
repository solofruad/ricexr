using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Secuencia de onboarding inicial mostrada despues de seleccionar el plano:
/// 1) Intro con avance manual o automatico.
/// 2) Mensaje breve de inicio de tutorial.
///
/// Este controlador solo gestiona UI Toolkit y callbacks de transicion.
/// No conoce la logica de niveles.
/// </summary>
[DisallowMultipleComponent]
public class StartupOnboardingController : MonoBehaviour
{
    public event Action IntroPanelShown;
    public event Action IntroContinueRequested;
    public event Action TutorialStartMessageShown;

    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Plantillas (UI Toolkit)")]
    [Tooltip("Template del panel Intro. Si no se asigna, se intentara cargar desde Resources.")]
    [SerializeField] private VisualTreeAsset introPanelTemplate;
    [Tooltip("Template del panel de inicio de tutorial. Si no se asigna, se intentara cargar desde Resources.")]
    [SerializeField] private VisualTreeAsset tutorialStartPanelTemplate;

    [Header("Fallback Resources (UI Toolkit)")]
    [SerializeField] private string introPanelResourcePath = "UIToolkit/IntroPanel";
    [SerializeField] private string tutorialStartPanelResourcePath = "UIToolkit/MsgTutorialStart";

    [Header("Contenido Intro")]
    [SerializeField] private string introTitle = "Bienvenido al entrenamiento";
    [TextArea(4, 8)]
    [SerializeField] private string introBody =
        "Esta aplicacion te ensena a reconocer Pyricularia oryzae y Rynchosporium en hojas de arroz.\n\n" +
        "Flujo:\n" +
        "1) Nivel 1: tutorial de uso de la herramienta VR.\n" +
        "2) Nivel 2: Pyricularia.\n" +
        "3) Nivel 3: Rynchosporium.\n" +
        "4) Nivel 4: evaluacion sin ayudas.\n\n" +
        "En niveles 2, 3 y 4 diagnosticaras 3 hojas por nivel para medir tu avance.";
    [SerializeField] private string introContinueButtonText = "Continuar";

    [Header("Contenido Aviso")]
    [SerializeField] private string tutorialStartTitle = "Inicia el Nivel 1: Tutorial";
    [SerializeField] private string tutorialStartSubtitle = "Primero aprenderas a usar la herramienta VR.";

    [Header("Tiempos")]
    [SerializeField] private float introAutoAdvanceDelay = 14f;
    [SerializeField] private float tutorialStartMessageDuration = 2.6f;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("World Space")]
    [SerializeField] private float distanceFromCamera = 1.5f;
    [SerializeField] private Vector3 cameraOffset = Vector3.zero;
    [SerializeField] private float surfaceHeightOffset = 0.7f;

    private VisualElement _documentRoot;
    private VisualElement _tutorialPanelRoot;

    private VisualElement _introRoot;
    private Label _introTitleLabel;
    private Label _introBodyLabel;
    private Button _introContinueButton;

    private VisualElement _tutorialStartRoot;
    private Label _tutorialStartTitleLabel;
    private Label _tutorialStartSubtitleLabel;

    private Tween _introFadeTween;
    private Tween _introAutoAdvanceTween;
    private Tween _tutorialStartFadeTween;
    private Tween _tutorialStartHoldTween;

    private bool _uiBuilt;
    private bool _sequenceRunning;
    private bool _introAdvanced;
    private bool _completionTriggered;
    private Action _onSequenceCompleted;

    private void Awake()
    {
        BuildUIIfNeeded();
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
        if (!BuildUIIfNeeded())
        {
            onCompleted?.Invoke();
            return;
        }

        if (_sequenceRunning) return;

        _sequenceRunning = true;
        _introAdvanced = false;
        _completionTriggered = false;
        _onSequenceCompleted = onCompleted;

        ApplyContent();
        RepositionPanel();

        if (_tutorialPanelRoot != null)
            _tutorialPanelRoot.style.display = DisplayStyle.None;

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

    private bool BuildUIIfNeeded()
    {
        if (_uiBuilt) return true;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogWarning("[StartupOnboarding] UIDocument no asignado/encontrado.");
            return false;
        }

        _documentRoot = uiDocument.rootVisualElement;
        if (_documentRoot == null)
        {
            Debug.LogWarning("[StartupOnboarding] rootVisualElement no disponible.");
            return false;
        }

        _tutorialPanelRoot = _documentRoot.Q<VisualElement>("panel-tutorial");

        _introRoot = InstantiatePanel(introPanelTemplate, introPanelResourcePath, "panel-intro");
        _tutorialStartRoot = InstantiatePanel(tutorialStartPanelTemplate, tutorialStartPanelResourcePath, "msg-tutorial-start");

        if (_introRoot != null)
        {
            _introTitleLabel = _introRoot.Q<Label>("intro-title");
            _introBodyLabel = _introRoot.Q<Label>("intro-body");
            _introContinueButton = _introRoot.Q<Button>("intro-continue-button");
            if (_introContinueButton != null)
                _introContinueButton.clicked += HandleIntroContinueClicked;
        }

        if (_tutorialStartRoot != null)
        {
            _tutorialStartTitleLabel = _tutorialStartRoot.Q<Label>("tutorialstart-title");
            _tutorialStartSubtitleLabel = _tutorialStartRoot.Q<Label>("tutorialstart-sub");
        }

        _uiBuilt = _introRoot != null && _tutorialStartRoot != null;
        if (!_uiBuilt)
            Debug.LogWarning("[StartupOnboarding] No se pudieron cargar todos los paneles de onboarding desde Resources.");

        return _uiBuilt;
    }

    private VisualElement InstantiatePanel(VisualTreeAsset templateFromInspector, string fallbackResourcePath, string rootName)
    {
        VisualTreeAsset template = templateFromInspector;
        if (template == null && !string.IsNullOrWhiteSpace(fallbackResourcePath))
            template = Resources.Load<VisualTreeAsset>(fallbackResourcePath);

        if (template == null)
        {
            Debug.LogWarning($"[StartupOnboarding] No se encontro template para '{rootName}'. Asigna el VisualTreeAsset en inspector o revisa el fallback de Resources.");
            return null;
        }

        TemplateContainer container = template.CloneTree();
        _documentRoot.Add(container);

        VisualElement root = container.Q<VisualElement>(rootName);
        if (root == null)
            Debug.LogWarning($"[StartupOnboarding] El template para '{rootName}' no contiene el elemento esperado '{rootName}'.");

        return root;
    }

    private void ApplyContent()
    {
        if (_introTitleLabel != null) _introTitleLabel.text = introTitle;
        if (_introBodyLabel != null) _introBodyLabel.text = introBody;
        if (_introContinueButton != null) _introContinueButton.text = introContinueButtonText;

        if (_tutorialStartTitleLabel != null) _tutorialStartTitleLabel.text = tutorialStartTitle;
        if (_tutorialStartSubtitleLabel != null) _tutorialStartSubtitleLabel.text = tutorialStartSubtitle;
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

        Action callback = _onSequenceCompleted;
        _onSequenceCompleted = null;
        callback?.Invoke();
    }

    private void RepositionPanel()
    {
        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            transform.position = SceneInteractionManager.Instance.SelectedPlanePosition
                                 + (SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up) * surfaceHeightOffset
                                 + cameraOffset;
            return;
        }

        if (Camera.main == null) return;

        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;

        transform.position = cam.position + fwd.normalized * distanceFromCamera + cameraOffset;
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
