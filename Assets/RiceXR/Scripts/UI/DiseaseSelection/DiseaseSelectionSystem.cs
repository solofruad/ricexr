using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using RiceXR.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class DiseaseSelectionSystem : MonoBehaviour
{
    public static DiseaseSelectionSystem Instance { get; private set; }

    public DiseaseCatalog catalog;
    public DiseaseSelectionView view;
    public GameObject diseaseSelectionPanel;
    public BodyLeashedPanel panelPlacement;

    public AudioClip correctSelectionSound, incorrectSelectionSound;

    [Header("Feedback UI Toolkit")]
    [SerializeField] private UIDocument correctFeedbackDocument;
    [SerializeField] private UIDocument incorrectFeedbackDocument;
    [Min(0f)] public float feedbackDuration = 3f;
    [Min(0f)] [SerializeField] private float feedbackFadeInDuration = 0.25f;
    [Min(0f)] [SerializeField] private float feedbackFadeOutDuration = 0.2f;
    [Min(0f)] [SerializeField] private float feedbackLiftPixels = 12f;

    [Min(0)] public int MarginOfError;
    public float animDuration = 0.25f;
    public bool PanelAvailable => _panelAvailable;

    private bool _panelAvailable = true;
    private bool _panelVisible;
    private bool _submitting;
    private bool _ready;
    private bool _feedbackInProgress;
    private bool _feedbackSuspended;
    private bool _feedbackWasCorrect;
    private bool _warnedCorrectFeedback;
    private bool _warnedIncorrectFeedback;

    private Leaf _leaf;
    private GrabbableLeafListener.SelectionHand _hand;
    private DiseaseDefinition _disease;
    private int _severity = -1;
    private int _correctSelectionsThisLevel;
    private int _plantsRequiredThisLevel = 1;
    private Vector3 _originalScale;

    private VisualElement _correctFeedbackRoot;
    private VisualElement _incorrectFeedbackRoot;
    private Coroutine _feedbackRoutine;
    private Tween _feedbackTween;

    private readonly HashSet<int> _identifiedLeavesThisLevel = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (panelPlacement == null)
            panelPlacement = GetComponent<BodyLeashedPanel>();
        if (diseaseSelectionPanel != null)
            _originalScale = diseaseSelectionPanel.transform.localScale;
    }

    private void OnEnable()
    {
        GameEventBus.OnLevelStarted += HandleLevelStarted;
        GameEventBus.OnReturningToMenu += HandleReturningToMenu;

        if (view == null) return;
        view.DiseaseChosen += ChooseDisease;
        view.SeverityChosen += ChooseSeverity;
        view.BackRequested += HandleBackRequested;
        view.ConfirmRequested += OnSubmit;
    }

    private void Start()
    {
        string error = "Falta el catálogo o la vista del selector.";
        _ready = catalog != null
            && view != null
            && diseaseSelectionPanel != null
            && panelPlacement != null
            && catalog.IsValid(out error);
        if (!_ready) Debug.LogError($"[DiseaseSelection] {error}", this);

        CacheFeedbackRoots(false);
        HideFeedbackImmediate();
        Hide();

        if (_ready) ResetSelectionState();
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStarted -= HandleLevelStarted;
        GameEventBus.OnReturningToMenu -= HandleReturningToMenu;

        if (view != null)
        {
            view.DiseaseChosen -= ChooseDisease;
            view.SeverityChosen -= ChooseSeverity;
            view.BackRequested -= HandleBackRequested;
            view.ConfirmRequested -= OnSubmit;
        }

        CancelFeedback();
        Hide();
    }

    private void LateUpdate()
    {
        if (!_ready) return;

        var listener = GrabbableLeafListener.Instance;
        Leaf selected = listener != null ? listener.ActualLeafGrabbed : null;

        if (_feedbackInProgress)
        {
            if (!_feedbackWasCorrect && selected != _leaf)
                FinishFeedbackNow();
            else
                return;
        }

        if (_feedbackSuspended) return;

        if (selected != _leaf)
        {
            _leaf = selected;
            ResetSelectionState();
            _submitting = false;
            if (_leaf != null) AttachPanel();
        }

        if (selected == null || !_panelAvailable)
        {
            if (_panelVisible) Hide();
            return;
        }

        if (listener.ActiveSelectionHand != _hand) AttachPanel();
        if (!_panelVisible)
        {
            AttachPanel();
            ShowAnimated();
        }
    }

    private void AttachPanel()
    {
        var listener = GrabbableLeafListener.Instance;
        if (listener == null || _leaf == null) return;

        _hand = listener.ActiveSelectionHand;
        panelPlacement?.Attach(_hand);
    }

    public void ShowAnimated()
    {
        if (diseaseSelectionPanel == null || _feedbackInProgress || _feedbackSuspended) return;

        Transform panel = diseaseSelectionPanel.transform;
        panel.DOKill();
        panel.localScale = _originalScale * 0.92f;
        diseaseSelectionPanel.SetActive(true);
        panel.DOScale(_originalScale, animDuration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        _panelVisible = true;
    }

    public void HideAnimated() => Hide();

    public void Show()
    {
        if (diseaseSelectionPanel == null || _feedbackInProgress || _feedbackSuspended) return;

        diseaseSelectionPanel.transform.DOKill();
        diseaseSelectionPanel.transform.localScale = _originalScale;
        diseaseSelectionPanel.SetActive(true);
        _panelVisible = true;
    }

    public void Hide()
    {
        panelPlacement?.Detach();
        if (diseaseSelectionPanel == null) return;

        diseaseSelectionPanel.transform.DOKill();
        diseaseSelectionPanel.SetActive(false);
        _panelVisible = false;
    }

    public void SetPanelAvailability(bool available)
    {
        _panelAvailable = available;

        if (!available)
        {
            SuspendFeedback();
            Hide();
            return;
        }

        if (_feedbackSuspended)
        {
            bool wasCorrect = _feedbackWasCorrect;
            _feedbackSuspended = false;
            CompleteFeedback(wasCorrect);
        }
    }

    private void ChooseDisease(DiseaseDefinition disease)
    {
        if (_submitting || !_panelAvailable || !catalog.Contains(disease)) return;

        _disease = disease;
        _severity = -1;
        view.ShowSeverities(disease);
    }

    private void ChooseSeverity(int severity)
    {
        if (_submitting || !_panelAvailable || _disease == null || !_disease.AllowsSeverity(severity)) return;

        _severity = severity;
        view.ShowSeverities(_disease, severity);
    }

    private void HandleBackRequested()
    {
        if (_submitting || !_panelAvailable) return;
        ResetSelectionState();
    }

    public void ResetSelection()
    {
        CancelFeedback();
        ResetSelectionState();
    }

    private void ResetSelectionState()
    {
        _disease = null;
        _severity = -1;
        if (view != null && catalog != null)
            view.ShowDiseases(catalog);
    }

    public void OnSubmit()
    {
        if (!_ready || !_panelAvailable || _submitting) return;

        Leaf leaf = GrabbableLeafListener.Instance?.ActualLeafGrabbed;
        if (leaf == null || leaf != _leaf || _disease == null || !_disease.AllowsSeverity(_severity)) return;

        int leafId = leaf.GetInstanceID();
        if (_identifiedLeavesThisLevel.Contains(leafId)) return;

        _submitting = true;
        view.SetInteractionEnabled(false);

        bool correct = leaf.diseaseSpots != null && leaf.diseaseSpots.Any(spot =>
            spot != null
            && spot.disease != null
            && DiagnosisEvaluator.MatchesAvailable(
                new DiagnosisEvaluator.Spot(spot.disease.id, spot.severity),
                _disease.id,
                _severity,
                MarginOfError,
                _disease.AvailableSeverities.Select(s => s.value)));

        GameEventBus.PublishDiagnosisAttemptEvaluated(_disease.id, _severity, correct);

        AudioClip clip = correct ? correctSelectionSound : incorrectSelectionSound;
        if (clip != null) AudioSource.PlayClipAtPoint(clip, leaf.transform.position);

        if (correct)
        {
            _identifiedLeavesThisLevel.Add(leafId);
            _correctSelectionsThisLevel++;
            leaf.BurnAndDisable();
        }
        else
        {
            GameEventBus.PublishPlantSelected(false, _correctSelectionsThisLevel, _plantsRequiredThisLevel);
        }

        if (!BeginFeedback(correct))
            CompleteFeedback(correct);
    }

    private bool BeginFeedback(bool correct)
    {
        VisualElement root = ResolveFeedbackRoot(correct, true);
        if (root == null) return false;

        HideFeedbackImmediate();
        _feedbackWasCorrect = correct;
        _feedbackInProgress = true;
        _feedbackSuspended = false;

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 0f;
        root.style.translate = new StyleTranslate(new Translate(0, feedbackLiftPixels, 0));

        _feedbackRoutine = StartCoroutine(FeedbackRoutine(root, correct));
        return true;
    }

    private IEnumerator FeedbackRoutine(VisualElement root, bool correct)
    {
        yield return null;
        if (!_feedbackInProgress) yield break;

        float opacity = 0f;
        _feedbackTween = DOTween.To(
                () => opacity,
                value =>
                {
                    opacity = value;
                    root.style.opacity = value;
                    root.style.translate = new StyleTranslate(new Translate(
                        0,
                        Mathf.Lerp(feedbackLiftPixels, 0f, value),
                        0));
                },
                1f,
                feedbackFadeInDuration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        yield return new WaitForSeconds(feedbackDuration);
        if (!_feedbackInProgress) yield break;

        _feedbackTween?.Kill();
        float from = root.resolvedStyle.opacity;
        Tween fadeOut = DOTween.To(
                () => from,
                value =>
                {
                    from = value;
                    root.style.opacity = value;
                },
                0f,
                feedbackFadeOutDuration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        _feedbackTween = fadeOut;

        while (fadeOut.IsActive() && !fadeOut.IsComplete())
            yield return null;

        root.style.display = DisplayStyle.None;
        _feedbackTween = null;
        _feedbackRoutine = null;
        _feedbackInProgress = false;
        CompleteFeedback(correct);
    }

    private void CompleteFeedback(bool correct)
    {
        _submitting = false;
        view?.SetInteractionEnabled(true);

        if (!correct)
        {
            ResetSelectionState();
            return;
        }

        Hide();
        GameEventBus.PublishPlantSelected(true, _correctSelectionsThisLevel, _plantsRequiredThisLevel);

        if (_correctSelectionsThisLevel >= _plantsRequiredThisLevel)
            GameEventBus.PublishAllPlantsSelected();
    }

    private void FinishFeedbackNow()
    {
        if (!_feedbackInProgress) return;

        bool wasCorrect = _feedbackWasCorrect;
        StopFeedbackAnimation();
        HideFeedbackImmediate();
        _feedbackInProgress = false;
        CompleteFeedback(wasCorrect);
    }

    private void SuspendFeedback()
    {
        if (!_feedbackInProgress) return;

        StopFeedbackAnimation();
        HideFeedbackImmediate();
        _feedbackInProgress = false;
        _feedbackSuspended = true;
    }

    private void CancelFeedback()
    {
        StopFeedbackAnimation();
        HideFeedbackImmediate();
        _feedbackInProgress = false;
        _feedbackSuspended = false;
        _submitting = false;
        view?.SetInteractionEnabled(true);
    }

    private void StopFeedbackAnimation()
    {
        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
        }

        _feedbackTween?.Kill();
        _feedbackTween = null;
    }

    private void CacheFeedbackRoots(bool warn)
    {
        if (_correctFeedbackRoot == null || _correctFeedbackRoot.panel == null)
            _correctFeedbackRoot = ResolveFeedbackRoot(correctFeedbackDocument, "msg-correct", warn, ref _warnedCorrectFeedback);

        if (_incorrectFeedbackRoot == null || _incorrectFeedbackRoot.panel == null)
            _incorrectFeedbackRoot = ResolveFeedbackRoot(incorrectFeedbackDocument, "msg-incorrect", warn, ref _warnedIncorrectFeedback);
    }

    private VisualElement ResolveFeedbackRoot(bool correct, bool warn)
    {
        CacheFeedbackRoots(warn);
        return correct ? _correctFeedbackRoot : _incorrectFeedbackRoot;
    }

    private VisualElement ResolveFeedbackRoot(
        UIDocument document,
        string rootName,
        bool warn,
        ref bool warned)
    {
        if (document == null)
        {
            if (warn && !warned)
            {
                warned = true;
                Debug.LogWarning($"[DiseaseSelection] Falta asignar el UIDocument '{rootName}'.", this);
            }
            return null;
        }

        if (!document.gameObject.activeSelf)
            document.gameObject.SetActive(true);

        VisualElement root = document.rootVisualElement?.Q<VisualElement>(rootName);
        if (root != null) return root;

        if (warn && !warned)
        {
            warned = true;
            Debug.LogWarning(
                $"[DiseaseSelection] El UIDocument '{document.name}' no contiene la raíz '{rootName}' o todavía no está activo.",
                this);
        }

        return null;
    }

    private void HideFeedbackImmediate()
    {
        SetFeedbackHidden(_correctFeedbackRoot);
        SetFeedbackHidden(_incorrectFeedbackRoot);
    }

    private static void SetFeedbackHidden(VisualElement root)
    {
        if (root == null) return;
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;
    }

    private void HandleLevelStarted(int index, int total, int required)
    {
        CancelFeedback();
        _correctSelectionsThisLevel = 0;
        _plantsRequiredThisLevel = Mathf.Max(1, required);
        _identifiedLeavesThisLevel.Clear();
        _leaf = null;
        _panelAvailable = true;
        ResetSelectionState();
        Hide();
    }

    private void HandleReturningToMenu()
    {
        CancelFeedback();
        _leaf = null;
        ResetSelectionState();
        Hide();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!_ready || !_panelAvailable || _submitting || Keyboard.current?.cKey.wasPressedThisFrame != true)
            return;

        Leaf leaf = GrabbableLeafListener.Instance?.ActualLeafGrabbed;
        if (leaf == null || !leaf.IsDiagnosable(catalog)) return;

        _leaf = leaf;
        DiseaseSpot spot = leaf.diseaseSpots.First(s =>
            s != null
            && catalog.Contains(s.disease)
            && (s.severity == -1 || s.disease.AllowsSeverity(s.severity)));
        ChooseDisease(spot.disease);
        ChooseSeverity(spot.severity == -1
            ? spot.disease.AvailableSeverities.First().value
            : spot.severity);
        OnSubmit();
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        panelPlacement?.Detach();
        diseaseSelectionPanel?.transform.DOKill();
        _feedbackTween?.Kill();
    }
}
