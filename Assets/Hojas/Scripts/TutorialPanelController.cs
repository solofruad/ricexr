using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

public enum TutorialGuidanceAct
{
    None = 0,
    GrabLeaf = 1,
    ObserveLeaf = 2,
    DiagnoseFirstLeaf = 3,
    FreePracticeSecondLeaf = 4,
    Completed = 5
}

public class TutorialPanelController : MonoBehaviour
{
    public event Action TutorialStarted;
    public event Action GuidedPhaseCompleted;
    public event Action<TutorialGuidanceAct> TutorialActChanged;
    public event Action TutorialCompleted;

    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIGameListener uiGameListener;

    [Header("Actos guiados")]
    [Tooltip("Tiempo en segundos para pasar de observar a diagnosticar mientras la hoja sigue agarrada.")]
    [SerializeField] private float observeToDiagnoseDelay = 6f;

    [Header("Animacion de subida al completar tutorial")]
    [SerializeField] private float slideUpAmount = 0.25f;
    [SerializeField] private float slideUpDuration = 0.6f;

    [Header("World Space")]
    [SerializeField] private float distanceFromCamera = 1.5f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, 0f);

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

    private Tween _fadeTween;
    private Tween _slideTween;
    private Tween _observeTween;

    private bool _isVisible;
    private bool _levelStartRequested;
    private bool _guidedPhaseCompleted;
    private bool _tutorialFullyCompleted;
    private int _plantsSelected;
    private int _plantsRequired = 2;
    private TutorialGuidanceAct _currentAct = TutorialGuidanceAct.None;

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
        GrabbableObjectListener.SelectionUpdated += HandleLeafSelected;
        GrabbableObjectListener.SelectionCleared += HandleLeafReleased;
        GameEventBus.OnPlantSelected += HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected += HandleAllPlantsSelected;
    }

    private void OnDisable()
    {
        GrabbableObjectListener.SelectionUpdated -= HandleLeafSelected;
        GrabbableObjectListener.SelectionCleared -= HandleLeafReleased;
        GameEventBus.OnPlantSelected -= HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected -= HandleAllPlantsSelected;

        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

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
            StartTutorialGameplayIfNeeded();
            EnterAct(TutorialGuidanceAct.GrabLeaf, true);
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
            _currentAct = TutorialGuidanceAct.None;
        });
    }

    private void StartTutorialGameplayIfNeeded()
    {
        if (_levelStartRequested) return;

        _levelStartRequested = true;
        uiGameListener?.OnTutorialCompleted();
    }

    private void HandleLeafSelected(Leaf leaf, GrabbableObjectListener.SelectionHand hand, Transform anchor)
    {
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;
        if (leaf == null) return;

        if (_currentAct == TutorialGuidanceAct.GrabLeaf)
        {
            EnterAct(TutorialGuidanceAct.ObserveLeaf);
            return;
        }

        if (_currentAct == TutorialGuidanceAct.ObserveLeaf)
        {
            StartObserveTimer();
        }
    }

    private void HandleLeafReleased(Leaf leaf)
    {
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;

        if (_currentAct == TutorialGuidanceAct.ObserveLeaf || _currentAct == TutorialGuidanceAct.DiagnoseFirstLeaf)
        {
            EnterAct(TutorialGuidanceAct.GrabLeaf, true);
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
                    EnterAct(TutorialGuidanceAct.FreePracticeSecondLeaf, true);
                }
                else
                {
                    EnterAct(TutorialGuidanceAct.Completed, true);
                    CompleteTutorial();
                }

                return;
            }

            EnterAct(TutorialGuidanceAct.DiagnoseFirstLeaf, true);
            return;
        }

        if (isCorrect)
        {
            int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
            if (remaining > 0)
            {
                EnterAct(TutorialGuidanceAct.FreePracticeSecondLeaf, true);
            }
            else
            {
                EnterAct(TutorialGuidanceAct.Completed, true);
                CompleteTutorial();
            }
        }
    }

    private void HandleAllPlantsSelected()
    {
        if (!_isVisible || _tutorialFullyCompleted) return;

        EnterAct(TutorialGuidanceAct.Completed, true);
        CompleteTutorial();
    }

    private void EnterAct(TutorialGuidanceAct act, bool force = false)
    {
        if (!force && _currentAct == act) return;

        _currentAct = act;
        _observeTween?.Kill();
        RenderAct(act);
        TutorialActChanged?.Invoke(act);

        if (act == TutorialGuidanceAct.ObserveLeaf)
            StartObserveTimer();
    }

    private void StartObserveTimer()
    {
        _observeTween?.Kill();

        _observeTween = DOVirtual.DelayedCall(Mathf.Max(0.5f, observeToDiagnoseDelay), () =>
        {
            if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;
            if (_currentAct != TutorialGuidanceAct.ObserveLeaf) return;
            if (GrabbableObjectListener.Instance == null) return;
            if (GrabbableObjectListener.Instance.ActualLeafGrabbed == null) return;

            EnterAct(TutorialGuidanceAct.DiagnoseFirstLeaf);
        });
    }

    private void RenderAct(TutorialGuidanceAct act)
    {
        if (_root == null) return;

        switch (act)
        {
            case TutorialGuidanceAct.GrabLeaf:
                ShowGuidedPanel();
                SetTitle("Acto 1: Toma una hoja");
                UpdateGuidedStepVisuals(0);
                SetProgress(0.33f, "Acto 1 de 3", "33%");
                break;

            case TutorialGuidanceAct.ObserveLeaf:
                ShowGuidedPanel();
                SetTitle("Acto 2: Observa los sintomas");
                UpdateGuidedStepVisuals(1);
                SetProgress(0.66f, "Acto 2 de 3", "66%");
                break;

            case TutorialGuidanceAct.DiagnoseFirstLeaf:
                ShowGuidedPanel();
                SetTitle("Acto 3: Registra tu diagnostico");
                UpdateGuidedStepVisuals(2);
                SetProgress(1f, "Acto 3 de 3", "100%");
                break;

            case TutorialGuidanceAct.FreePracticeSecondLeaf:
                ShowLightPanelForFreePractice();
                break;

            case TutorialGuidanceAct.Completed:
                ShowLightPanelForCompletion();
                break;

            default:
                ShowGuidedPanel();
                SetTitle("Tutorial");
                UpdateGuidedStepVisuals(0);
                SetProgress(0f, "Preparando tutorial...", "0%");
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
        SlideUp();
    }

    private void SlideUp()
    {
        _slideTween?.Kill();
        Vector3 target = transform.position + Vector3.up * slideUpAmount;
        _slideTween = transform.DOMove(target, slideUpDuration).SetEase(Ease.OutCubic);
    }

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
    }

    private void HideImmediate()
    {
        if (_root == null) return;
        _root.style.display = DisplayStyle.None;
        _root.style.opacity = 0f;
        _isVisible = false;
        _currentAct = TutorialGuidanceAct.None;
    }

    private void ResetSessionState()
    {
        _levelStartRequested = false;
        _guidedPhaseCompleted = false;
        _tutorialFullyCompleted = false;
        _plantsSelected = 0;
        _plantsRequired = 2;
        _currentAct = TutorialGuidanceAct.None;
    }

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
                                 + (SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up) * surfaceHeightOffset
                                 + cameraOffset;
            return;
        }

        if (Camera.main == null) return;
        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
        transform.position = cam.position + fwd.normalized * distanceFromCamera + cameraOffset;
    }
}
