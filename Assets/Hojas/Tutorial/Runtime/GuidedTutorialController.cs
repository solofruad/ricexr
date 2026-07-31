using System;
using UnityEngine;

public enum GuidedTutorialStage
{
    None, InitialDemonstration, WaitingForGrab, Observing, HintVisible,
    DemonstratingDiagnosis, GuidedDiagnosis, FreePractice, Celebrating, Completed
}

/// <summary>
/// The single authority for the interactive tutorial. It only advances from real
/// grab, acknowledgement, diagnosis and animation-complete events.
/// </summary>
[DisallowMultipleComponent]
public class GuidedTutorialController : MonoBehaviour
{
    public static GuidedTutorialController Instance { get; private set; }

    [SerializeField] private TutorialAvatarController avatar;
    [SerializeField] private TutorialHintPopupController hintPopup;
    [SerializeField, Min(1f)] private float continuousObserveSeconds = 5f;

    public event Action<GuidedTutorialStage> StageChanged;
    public GuidedTutorialStage Stage { get; private set; } = GuidedTutorialStage.None;
    public bool IsRunning => Stage != GuidedTutorialStage.None && Stage != GuidedTutorialStage.Completed;
    public bool IsAwaitingCelebration => Stage == GuidedTutorialStage.Celebrating;

    private TutorialLeafSequenceSpawner _sequence;
    private float _observeStartedAt = -1f;
    private bool _completed;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        ResolveDependencies();
    }

    private void OnEnable()
    {
        GrabbableLeafListener.SelectionUpdated += HandleSelectionUpdated;
        GrabbableLeafListener.SelectionCleared += HandleSelectionCleared;
        GameEventBus.OnDiagnosisAttemptEvaluated += HandleDiagnosisAttempt;
        if (avatar != null)
        {
            avatar.GrabObserveFinished += HandleGrabObserveFinished;
            avatar.ExplainDiagnosisFinished += HandleExplanationFinished;
            avatar.ThumbsUpFinished += HandleThumbsUpFinished;
        }
        if (hintPopup != null) hintPopup.Acknowledged += HandleHintAcknowledged;
    }

    private void OnDisable()
    {
        GrabbableLeafListener.SelectionUpdated -= HandleSelectionUpdated;
        GrabbableLeafListener.SelectionCleared -= HandleSelectionCleared;
        GameEventBus.OnDiagnosisAttemptEvaluated -= HandleDiagnosisAttempt;
        if (avatar != null)
        {
            avatar.GrabObserveFinished -= HandleGrabObserveFinished;
            avatar.ExplainDiagnosisFinished -= HandleExplanationFinished;
            avatar.ThumbsUpFinished -= HandleThumbsUpFinished;
        }
        if (hintPopup != null) hintPopup.Acknowledged -= HandleHintAcknowledged;
    }

    private void Update()
    {
        if (Stage != GuidedTutorialStage.Observing || _sequence == null) return;
        bool holdingGuidedLeaf = GrabbableLeafListener.Instance != null &&
                                 GrabbableLeafListener.Instance.ActualLeafGrabbed == _sequence.GuidedLeaf;
        if (!holdingGuidedLeaf)
        {
            _observeStartedAt = -1f;
            return;
        }
        if (_observeStartedAt < 0f) _observeStartedAt = Time.time;
        if (Time.time - _observeStartedAt >= continuousObserveSeconds)
            ShowHint();
    }

    /// <summary>Called by GameFlow only after the first deterministic leaf exists.</summary>
    public void StartTutorial()
    {
        ResetTutorial();
        ResolveDependencies();
        _sequence = FindObjectOfType<TutorialLeafSequenceSpawner>(true);
        if (_sequence == null || _sequence.GuidedLeaf == null)
        {
            Debug.LogError("[GuidedTutorial] Falta TutorialLeafSequenceSpawner o su primera hoja.");
            return;
        }

        DiseaseSelectionSystem.Instance?.ConfigureTutorialInteractionGate(true, false);
        SetStage(GuidedTutorialStage.InitialDemonstration);
        GameEventBus.PublishTutorialStarted();
        if (avatar != null) avatar.PlayGrabObserve();
        else HandleGrabObserveFinished();
    }

    /// <summary>Safe reset when returning to the menu or beginning another session.</summary>
    public void ResetTutorial()
    {
        _completed = false;
        _observeStartedAt = -1f;
        hintPopup?.Hide();
        avatar?.Reset();
        DiseaseSelectionSystem.Instance?.ConfigureTutorialInteractionGate(false, true);
        SetStage(GuidedTutorialStage.None);
    }

    private void HandleGrabObserveFinished()
    {
        if (Stage != GuidedTutorialStage.InitialDemonstration) return;
        SetStage(GuidedTutorialStage.WaitingForGrab);
    }

    private void HandleSelectionUpdated(Leaf leaf, GrabbableLeafListener.SelectionHand hand, Transform anchor)
    {
        if (_sequence == null || leaf != _sequence.GuidedLeaf) return;
        if (Stage == GuidedTutorialStage.WaitingForGrab)
        {
            _observeStartedAt = Time.time;
            SetStage(GuidedTutorialStage.Observing);
        }
    }

    private void HandleSelectionCleared(Leaf leaf)
    {
        if (_sequence != null && leaf == _sequence.GuidedLeaf && Stage == GuidedTutorialStage.Observing)
            _observeStartedAt = -1f;
    }

    private void ShowHint()
    {
        if (Stage != GuidedTutorialStage.Observing) return;
        SetStage(GuidedTutorialStage.HintVisible);
        DiseaseSpot target = _sequence.GuidedTarget;
        if (hintPopup == null || !hintPopup.IsConfigured)
        {
            Debug.LogWarning("[GuidedTutorial] No hay popup Entendido configurado; se continúa con la demostración.");
            HandleHintAcknowledged();
            return;
        }
        hintPopup.Show(target);
    }

    private void HandleHintAcknowledged()
    {
        if (Stage != GuidedTutorialStage.HintVisible) return;
        SetStage(GuidedTutorialStage.DemonstratingDiagnosis);
        if (avatar != null) avatar.PlayExplainDiagnosis();
        else HandleExplanationFinished();
    }

    private void HandleExplanationFinished()
    {
        if (Stage != GuidedTutorialStage.DemonstratingDiagnosis) return;
        DiseaseSelectionSystem.Instance?.ConfigureTutorialInteractionGate(true, true);
        SetStage(GuidedTutorialStage.GuidedDiagnosis);
    }

    private void HandleDiagnosisAttempt(string disease, int severity, bool correct)
    {
        if (_sequence == null || GrabbableLeafListener.Instance == null ||
            GrabbableLeafListener.Instance.ActualLeafGrabbed != _sequence.CurrentLeaf)
            return;

        if (Stage == GuidedTutorialStage.GuidedDiagnosis)
        {
            if (!correct) return; // Hint remains and retry is intentionally allowed.
            hintPopup?.Hide();
            _sequence.SpawnPracticeLeaf();
            SetStage(GuidedTutorialStage.FreePractice);
            return;
        }

        if (Stage == GuidedTutorialStage.FreePractice && correct)
        {
            DiseaseSelectionSystem.Instance?.ConfigureTutorialInteractionGate(true, false);
            SetStage(GuidedTutorialStage.Celebrating);
            if (avatar != null) avatar.PlayThumbsUp();
            else HandleThumbsUpFinished();
        }
    }

    private void HandleThumbsUpFinished()
    {
        if (Stage != GuidedTutorialStage.Celebrating || _completed) return;
        _completed = true;
        SetStage(GuidedTutorialStage.Completed);
        GameEventBus.PublishTutorialCompleted();
    }

    private void SetStage(GuidedTutorialStage next)
    {
        if (Stage == next) return;
        Stage = next;
        StageChanged?.Invoke(next);
        GameEventBus.PublishGuidedTutorialStageChanged(next);
    }

    private void ResolveDependencies()
    {
        if (avatar == null) avatar = FindObjectOfType<TutorialAvatarController>(true);
        if (hintPopup == null) hintPopup = FindObjectOfType<TutorialHintPopupController>(true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
