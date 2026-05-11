using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

public enum TutorialGuidanceAct
{
    NONE = 0,
    GRAB_LEAF = 1,
    OBSERVE_LEAF = 2,
    DIAGNOSE_FIRST_LEAF = 3,
    FREE_PRACTICE_SECOND_LEAF = 4,
    COMPLETED = 5
}

public class TutorialPanelController : MonoBehaviour
{
    public event Action TutorialStarted;
    public event Action GuidedPhaseCompleted;
    public event Action<TutorialGuidanceAct> TutorialActChanged;
    public event Action TutorialCompleted;

    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Actos UXML (opcional)")]
    [SerializeField] private VisualTreeAsset tutorialAct1Template;
    [SerializeField] private VisualTreeAsset tutorialAct2Template;
    [SerializeField] private VisualTreeAsset tutorialAct3Template;
    [SerializeField] private bool verboseSplitActsLogs = true;

    [Header("Actos guiados")]
    [Tooltip("Tiempo en segundos para pasar de observar a diagnosticar mientras la hoja sigue agarrada.")]
    [SerializeField] private float observeToDiagnoseDelay = 6f;

    [Header("Animacion de subida al completar tutorial")]
    [SerializeField] private float slideUpAmount = 0.25f;
    [SerializeField] private float slideUpDuration = 0.6f;


    [Header("Anclaje sobre superficie")]
    [SerializeField] private float surfaceHeightOffset = 0.28f;

    private VisualElement _root;
    private Label _titleLabel;
    private VisualElement _stepsContainer;
    private Label _step1Title;
    private Label _step1Body;
    private Label _step2Title;
    private Label _step2Body;
    private Label _step3Title;
    private Label _step3Body;
    private VisualElement _lightPanel;
    private Label _lightTitle;
    private Label _lightBody;
    private VisualElement _progressFill;
    private Label _progressLabel;
    private Label _progressPercent;
    private VisualElement _tutorialAct1Root;
    private VisualElement _tutorialAct2Root;
    private VisualElement _tutorialAct3Root;
    private bool _splitActsReady;
    private readonly List<VisualElement> _legacyStepBlocks = new List<VisualElement>();

    private Tween _fadeTween;
    private Tween _slideTween;
    private Tween _observeTween;

    private bool _isVisible;
    private bool _levelStartRequested;
    private bool _guidedPhaseCompleted;
    private bool _tutorialFullyCompleted;
    private int _plantsSelected;
    private int _plantsRequired = 2;
    private TutorialGuidanceAct _currentAct = TutorialGuidanceAct.NONE;

    private static readonly Color ActiveStepTitleColor = new Color(1f, 1f, 1f, 0.96f);
    private static readonly Color CompletedStepTitleColor = new Color(0.67f, 0.9f, 0.73f, 0.96f);
    private static readonly Color InactiveStepTitleColor = new Color(1f, 1f, 1f, 0.58f);
    private static readonly Color ActiveStepBodyColor = new Color(1f, 1f, 1f, 0.62f);
    private static readonly Color InactiveStepBodyColor = new Color(1f, 1f, 1f, 0.42f);

    private void Awake()
    {
        CacheElements();
        HideImmediate();
    }

    private void OnEnable()
    {
        GrabbableLeafListener.SelectionUpdated += HandleLeafSelected;
        GrabbableLeafListener.SelectionCleared += HandleLeafReleased;
        GameEventBus.OnPlantSelected += HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected += HandleAllPlantsSelected;
    }

    private void OnDisable()
    {
        GrabbableLeafListener.SelectionUpdated -= HandleLeafSelected;
        GrabbableLeafListener.SelectionCleared -= HandleLeafReleased;
        GameEventBus.OnPlantSelected -= HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected -= HandleAllPlantsSelected;

        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }
    /// <summary>
    /// Muestra el panel y comienza el tutorial desde el primer acto. Si el panel ya esta visible,
    /// simplemente reinicia el estado del tutorial y muestra el primer acto.
    /// </summary>
    public void ShowAndStart()
    {
        if (_root == null) return;

        KillTweens();
        ResetSessionState();

        RepositionPanel();
        _isVisible = true;

        _fadeTween?.Kill();
        _root.style.display = DisplayStyle.Flex;
        _root.style.opacity = 0f;
        float opacity = 0f;
        _fadeTween = DOTween.To(
            () => opacity,
            v => { opacity = v; _root.style.opacity = v; },
            1f, 0.4f
        )
        .SetEase(Ease.OutCubic)
        .OnComplete(() =>
        {
            TutorialStarted?.Invoke();
            GameEventBus.PublishTutorialStarted();
            StartTutorialGameplayIfNeeded();
            EnterAct(TutorialGuidanceAct.GRAB_LEAF, true);
        });
    }

    public void Hide()
    {
        if (_root == null || !_isVisible) return;

        _observeTween?.Kill();
        _slideTween?.Kill();

        float opacity = _root.resolvedStyle.opacity;
        _fadeTween?.Kill();
        _fadeTween = DOTween.To(
            () => opacity,
            v => { opacity = v; _root.style.opacity = v; },
            0f, 0.25f
        )
        .SetEase(Ease.InQuad)
        .OnComplete(() =>
        {
            _root.style.display = DisplayStyle.None;
            _isVisible = false;
            _currentAct = TutorialGuidanceAct.NONE;
        });
    }

    private void StartTutorialGameplayIfNeeded()
    {
        if (_levelStartRequested) return;
        _levelStartRequested = true;

        // El flujo de iniciar el primer nivel es responsabilidad de GameFlowController,
        // que escucha OnTutorialCompleted del bus. Aquí solo notificamos que el
        // gameplay del tutorial está listo para comenzar.
    }

    /// <summary>
    /// 1. Maneja la logica de transicion entre actos del tutorial basada en las interacciones del usuario con las hojas y plantas.
    /// </summary>
    /// <param name="leaf"></param>
    /// <param name="hand"></param>
    /// <param name="anchor"></param>
    private void HandleLeafSelected(Leaf leaf, GrabbableLeafListener.SelectionHand hand, Transform anchor)
    {
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;
        if (leaf == null) return;

        if (_currentAct == TutorialGuidanceAct.GRAB_LEAF)
        {
            EnterAct(TutorialGuidanceAct.OBSERVE_LEAF);
            return;
        }

        if (_currentAct == TutorialGuidanceAct.OBSERVE_LEAF)
        {
            StartObserveTimer();
        }
    }

    private void HandleLeafReleased(Leaf leaf)
    {
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;

        if (_currentAct == TutorialGuidanceAct.OBSERVE_LEAF || _currentAct == TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF)
        {
            EnterAct(TutorialGuidanceAct.GRAB_LEAF, true);
        }
    }

    private void HandlePlantSelected(bool isCorrect, int plantsSelected, int plantsRequired)
    {
        if (!_isVisible || _tutorialFullyCompleted) return;

        _plantsSelected = Mathf.Max(0, plantsSelected);
        _plantsRequired = Mathf.Max(1, plantsRequired);

        if (!_guidedPhaseCompleted)
        {
            if (isCorrect)
            {
                _guidedPhaseCompleted = true;
                _observeTween?.Kill();
                GuidedPhaseCompleted?.Invoke();

                int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
                if (remaining > 0)
                {
                    EnterAct(TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF, true);
                }
                else
                {
                    EnterAct(TutorialGuidanceAct.COMPLETED, true);
                    CompleteTutorial();
                }

                return;
            }

            EnterAct(TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF, true);
            return;
        }

        if (isCorrect)
        {
            int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
            if (remaining > 0)
            {
                EnterAct(TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF, true);
            }
            else
            {
                EnterAct(TutorialGuidanceAct.COMPLETED, true);
                CompleteTutorial();
            }
        }
    }

    private void HandleAllPlantsSelected()
    {
        if (!_isVisible || _tutorialFullyCompleted) return;

        EnterAct(TutorialGuidanceAct.COMPLETED, true);
        CompleteTutorial();
    }

    private void EnterAct(TutorialGuidanceAct act, bool force = false)
    {
        if (!force && _currentAct == act) return;

        _currentAct = act;
        _observeTween?.Kill();
        RenderAct(act);
        TutorialActChanged?.Invoke(act);

        if (act == TutorialGuidanceAct.OBSERVE_LEAF)
            StartObserveTimer();
    }

    private void StartObserveTimer()
    {
        _observeTween?.Kill();

        _observeTween = DOVirtual.DelayedCall(Mathf.Max(0.5f, observeToDiagnoseDelay), () =>
        {
            if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;
            if (_currentAct != TutorialGuidanceAct.OBSERVE_LEAF) return;
            if (GrabbableLeafListener.Instance == null) return;
            if (GrabbableLeafListener.Instance.ActualLeafGrabbed == null) return;

            EnterAct(TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF);
        });
    }

    private void RenderAct(TutorialGuidanceAct act)
    {
        if (_root == null) return;

        switch (act)
        {
            case TutorialGuidanceAct.GRAB_LEAF:
                ShowGuidedActPanel(1);
                SetTitle("Acto 1: Toma una hoja");
                SetProgress(0.33f, "Acto 1 de 3", "33%");
                break;

            case TutorialGuidanceAct.OBSERVE_LEAF:
                ShowGuidedActPanel(2);
                SetTitle("Acto 2: Observa los sintomas");
                SetProgress(0.66f, "Acto 2 de 3", "66%");
                break;

            case TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF:
                ShowGuidedActPanel(3);
                SetTitle("Acto 3: Registra tu diagnostico");
                SetProgress(1f, "Acto 3 de 3", "100%");
                break;

            case TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF:
                ShowLightPanelForFreePractice();
                break;

            case TutorialGuidanceAct.COMPLETED:
                ShowLightPanelForCompletion();
                break;

            default:
                ShowGuidedActPanel(1);
                SetTitle("Tutorial");
                SetProgress(0f, "Preparando tutorial...", "0%");
                break;
        }
    }

    private void ShowGuidedActPanel(int actIndex)
    {
        ShowGuidedPanel();

        if (!_splitActsReady)
        {
            UpdateGuidedStepVisuals(Mathf.Clamp(actIndex - 1, 0, 2));
            return;
        }

        switch (actIndex)
        {
            case 1:
                ShowSingleActPanel(_tutorialAct1Root);
                break;

            case 2:
                ShowSingleActPanel(_tutorialAct2Root);
                break;

            case 3:
                ShowSingleActPanel(_tutorialAct3Root);
                break;

            default:
                ShowSingleActPanel(_tutorialAct1Root);
                break;
        }
    }

    private void ShowGuidedPanel()
    {
        if (_stepsContainer != null)
            _stepsContainer.style.display = DisplayStyle.Flex;

        if (_lightPanel != null)
            _lightPanel.style.display = DisplayStyle.None;
    }

    private void ShowLightPanelForFreePractice()
    {
        if (_splitActsReady)
            ShowSingleActPanel(null);

        if (_stepsContainer != null)
            _stepsContainer.style.display = DisplayStyle.None;

        if (_lightPanel != null)
            _lightPanel.style.display = DisplayStyle.Flex;

        int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
        SetTitle("Practica libre");

        if (_lightTitle != null)
            _lightTitle.text = "Primer diagnostico correcto.";

        if (_lightBody != null)
        {
            if (remaining == 1)
                _lightBody.text = "Te falta una hoja. Diagnosticala por tu cuenta para completar el nivel tutorial.";
            else
                _lightBody.text = "Continua sin guia paso a paso y completa las hojas restantes del tutorial.";
        }

        float ratio = _plantsRequired > 0 ? (float)_plantsSelected / _plantsRequired : 0f;
        ratio = Mathf.Clamp01(ratio);
        string progressText = remaining == 1
            ? "Te falta una hoja"
            : $"Te faltan {remaining} hojas";
        SetProgress(ratio, "Practica libre", progressText);
    }

    private void ShowLightPanelForCompletion()
    {
        if (_splitActsReady)
            ShowSingleActPanel(null);

        if (_stepsContainer != null)
            _stepsContainer.style.display = DisplayStyle.None;

        if (_lightPanel != null)
            _lightPanel.style.display = DisplayStyle.Flex;

        SetTitle("Tutorial completado");

        if (_lightTitle != null)
            _lightTitle.text = "Excelente trabajo.";

        if (_lightBody != null)
            _lightBody.text = "Completaste las 2 hojas del tutorial. Preparando el siguiente nivel.";

        SetProgress(1f, "Tutorial practico completado", "100%");
    }

    private void UpdateGuidedStepVisuals(int activeIndex)
    {
        ApplyStepVisual(_step1Title, _step1Body, activeIndex == 0, activeIndex > 0);
        ApplyStepVisual(_step2Title, _step2Body, activeIndex == 1, activeIndex > 1);
        ApplyStepVisual(_step3Title, _step3Body, activeIndex == 2, activeIndex > 2);
    }

    private void ApplyStepVisual(Label title, Label body, bool active, bool completed)
    {
        if (title != null)
        {
            Color titleColor = completed ? CompletedStepTitleColor : active ? ActiveStepTitleColor : InactiveStepTitleColor;
            title.style.color = titleColor;
            title.style.opacity = active ? 1f : 0.85f;
        }

        if (body != null)
        {
            body.style.color = active ? ActiveStepBodyColor : InactiveStepBodyColor;
            body.style.opacity = active ? 1f : 0.8f;
        }
    }

    private void SetTitle(string title)
    {
        if (_titleLabel != null)
            _titleLabel.text = title;
    }

    private void SetProgress(float normalized, string labelText, string percentText)
    {
        if (_progressFill != null)
            _progressFill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);

        if (_progressLabel != null)
            _progressLabel.text = labelText;

        if (_progressPercent != null)
            _progressPercent.text = percentText;
    }

    private void CompleteTutorial()
    {
        if (_tutorialFullyCompleted) return;

        _tutorialFullyCompleted = true;
        TutorialCompleted?.Invoke();
        GameEventBus.PublishTutorialCompleted();
        SlideUp();
    }

    private void SlideUp()
    {
        _slideTween?.Kill();
        Vector3 target = transform.position + Vector3.up * slideUpAmount;
        _slideTween = transform.DOMove(target, slideUpDuration).SetEase(Ease.OutCubic);
    }

    // Cachea referencias a elementos del UI para manipularlos luego. Tambien intenta configurar el sistema de split acts si los templates estan disponibles.
    private void CacheElements()
    {
        if (uiDocument == null) return;

        _root = uiDocument.rootVisualElement.Q<VisualElement>("panel-tutorial");
        _titleLabel = _root?.Q<Label>("tutorial-title");
        _stepsContainer = _root?.Q<VisualElement>("steps-container");
        _step1Title = _root?.Q<Label>("tutorial-step1-title");
        _step1Body = _root?.Q<Label>("tutorial-step1-body");
        _step2Title = _root?.Q<Label>("tutorial-step2-title");
        _step2Body = _root?.Q<Label>("tutorial-step2-body");
        _step3Title = _root?.Q<Label>("tutorial-step3-title");
        _step3Body = _root?.Q<Label>("tutorial-step3-body");
        _lightPanel = _root?.Q<VisualElement>("tutorial-light-panel");
        _lightTitle = _root?.Q<Label>("tutorial-light-title");
        _lightBody = _root?.Q<Label>("tutorial-light-body");
        _progressFill = _root?.Q<VisualElement>("tutorial-progress-fill");
        _progressLabel = _root?.Q<Label>("tutorial-progress-label");
        _progressPercent = _root?.Q<Label>("tutorial-progress-percent");

        ConfigureSplitActPanelsIfAvailable();
    }

    private void ConfigureSplitActPanelsIfAvailable()
    {
        if (_stepsContainer == null || _splitActsReady)
        {
            if (_stepsContainer == null && verboseSplitActsLogs)
                Debug.LogWarning("[TutorialPanel] No se encontro 'steps-container'. Se usara panel legacy.");
            return;
        }

        VisualTreeAsset act1Template = tutorialAct1Template;
        VisualTreeAsset act2Template = tutorialAct2Template;
        VisualTreeAsset act3Template = tutorialAct3Template;

        if (act1Template == null || act2Template == null || act3Template == null)
        {
            if (verboseSplitActsLogs)
            {
                Debug.LogWarning(
                    $"[TutorialPanel] Split acts incompleto. A1:{(act1Template != null)} A2:{(act2Template != null)} A3:{(act3Template != null)}. " +
                    "Se usara panel legacy.");
            }
            return;
        }

        _legacyStepBlocks.Clear();
        for (int i = 0; i < _stepsContainer.childCount; i++)
            _legacyStepBlocks.Add(_stepsContainer.ElementAt(i));

        var host = new VisualElement { name = "tutorial-acts-host" };
        host.style.flexGrow = 1f;
        host.style.flexDirection = FlexDirection.Column;
        _stepsContainer.Add(host);

        _tutorialAct1Root = InstantiateActRoot(act1Template, host, "tutorial-act-1");
        _tutorialAct2Root = InstantiateActRoot(act2Template, host, "tutorial-act-2");
        _tutorialAct3Root = InstantiateActRoot(act3Template, host, "tutorial-act-3");

        if (_tutorialAct1Root == null || _tutorialAct2Root == null || _tutorialAct3Root == null)
        {
            host.RemoveFromHierarchy();
            _tutorialAct1Root = null;
            _tutorialAct2Root = null;
            _tutorialAct3Root = null;
            if (verboseSplitActsLogs)
                Debug.LogWarning("[TutorialPanel] Fallo al instanciar roots de split acts. Se usara panel legacy.");
            return;
        }

        for (int i = 0; i < _legacyStepBlocks.Count; i++)
            _legacyStepBlocks[i].style.display = DisplayStyle.None;

        _splitActsReady = true;
        if (verboseSplitActsLogs)
            Debug.Log("[TutorialPanel] Split acts cargados correctamente (Act1/Act2/Act3).");
        ShowSingleActPanel(_tutorialAct1Root);
    }

    private static VisualElement InstantiateActRoot(VisualTreeAsset template, VisualElement host, string rootName)
    {
        if (template == null || host == null) return null;

        TemplateContainer container = template.CloneTree();
        host.Add(container);

        VisualElement root = container.Q<VisualElement>(rootName);
        if (root == null && container.childCount > 0)
            root = container[0] as VisualElement;

        if (root == null)
        {
            Debug.LogWarning($"[TutorialPanel] El template no contiene root '{rootName}'.");
            return null;
        }

        root.style.display = DisplayStyle.None;
        root.style.flexGrow = 1f;
        return root;
    }

    private void ShowSingleActPanel(VisualElement activePanel)
    {
        SetActDisplay(_tutorialAct1Root, _tutorialAct1Root == activePanel);
        SetActDisplay(_tutorialAct2Root, _tutorialAct2Root == activePanel);
        SetActDisplay(_tutorialAct3Root, _tutorialAct3Root == activePanel);
    }

    private static void SetActDisplay(VisualElement panel, bool visible)
    {
        if (panel == null) return;
        panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void HideImmediate()
    {
        if (_root == null) return;
        _root.style.display = DisplayStyle.None;
        _root.style.opacity = 0f;
        _isVisible = false;
        _currentAct = TutorialGuidanceAct.NONE;
    }

    /// <summary>
    /// Reinicia el estado interno del tutorial para permitir reiniciar el 
    /// tutorial desde el principio sin necesidad de recargar la escena o crear una nueva instancia del panel.
    /// </summary>
    private void ResetSessionState()
    {
        _levelStartRequested = false;
        _guidedPhaseCompleted = false;
        _tutorialFullyCompleted = false;
        _plantsSelected = 0;
        _plantsRequired = 2;
        _currentAct = TutorialGuidanceAct.NONE;
    }
    /// <summary>
    /// Mata cualquier tween activo para evitar que se sigan ejecutando callbacks o animaciones luego de que el panel se oculte o destruya.
    /// </summary>
    private void KillTweens()
    {
        _fadeTween?.Kill();
        _slideTween?.Kill();
        _observeTween?.Kill();
    }

    private void RepositionPanel()
    {
        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            transform.position = SceneInteractionManager.Instance.SelectedPlanePosition
                                 + (SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up) * surfaceHeightOffset;
            return;
        }

        if (Camera.main == null) return;
        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
        transform.position = cam.position + fwd.normalized;
    }
}
