using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>Adapter between the deterministic tutorial and the avatar Animator.</summary>
[DisallowMultipleComponent]
public class TutorialAvatarController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject avatarVisualRoot;
    [SerializeField] private GameObject demoLeafVisual;
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private GameObject demoMenuVisual;
    [SerializeField, Min(0.1f)] private float animationFallbackSeconds = 4f;
    [SerializeField, Min(0.05f)] private float disappearDuration = .45f;

    public event Action GrabObserveFinished;
    public event Action ExplainDiagnosisFinished;
    public event Action ThumbsUpFinished;

    private Vector3 _initialScale;
    private Coroutine _fallback;
    private Tween _disappearTween;
    private bool _grabSent;
    private bool _explainSent;
    private bool _thumbsSent;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (avatarVisualRoot == null) avatarVisualRoot = gameObject;
        _initialScale = avatarVisualRoot.transform.localScale;
        Reset();
    }

    public void PlayGrabObserve()
    {
        Play("PlayGrabObserve", () => NotifyGrabObserveFinished());
    }

    public void PlayExplainDiagnosis()
    {
        Play("PlayExplainDiagnosis", () => NotifyExplainDiagnosisFinished());
    }

    public void PlayThumbsUp()
    {
        Play("PlayThumbsUp", () => NotifyThumbsUpFinished());
    }

    public void AttachDemoLeafToHand()
    {
        if (demoLeafVisual == null || rightHandSocket == null) return;
        demoLeafVisual.transform.SetParent(rightHandSocket, false);
        demoLeafVisual.SetActive(true);
    }

    public void ShowDemoMenuVisual()
    {
        if (demoMenuVisual != null) demoMenuVisual.SetActive(true);
    }

    // Animation events. Keep these public names stable for the Animator clips.
    public void NotifyGrabObserveFinished()
    {
        if (_grabSent) return;
        _grabSent = true;
        StopFallback();
        GrabObserveFinished?.Invoke();
    }

    public void NotifyExplainDiagnosisFinished()
    {
        if (_explainSent) return;
        _explainSent = true;
        StopFallback();
        if (demoMenuVisual != null) demoMenuVisual.SetActive(false);
        ExplainDiagnosisFinished?.Invoke();
    }

    public void NotifyThumbsUpFinished()
    {
        if (_thumbsSent) return;
        _thumbsSent = true;
        StopFallback();
        if (avatarVisualRoot == null)
        {
            ThumbsUpFinished?.Invoke();
            return;
        }

        _disappearTween?.Kill();
        _disappearTween = avatarVisualRoot.transform.DOScale(Vector3.zero, disappearDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (avatarVisualRoot != null) avatarVisualRoot.SetActive(false);
                ThumbsUpFinished?.Invoke();
            });
    }

    /// <summary>Restores a reusable avatar after returning to the menu.</summary>
    public void Reset()
    {
        StopFallback();
        _disappearTween?.Kill();
        _grabSent = _explainSent = _thumbsSent = false;
        if (avatarVisualRoot != null)
        {
            avatarVisualRoot.SetActive(true);
            avatarVisualRoot.transform.localScale = _initialScale == Vector3.zero ? Vector3.one : _initialScale;
        }
        if (demoLeafVisual != null) demoLeafVisual.SetActive(false);
        if (demoMenuVisual != null) demoMenuVisual.SetActive(false);
        if (animator != null) animator.Rebind();
    }

    private void Play(string trigger, Action fallback)
    {
        if (avatarVisualRoot != null) avatarVisualRoot.SetActive(true);
        if (animator != null) animator.SetTrigger(trigger);
        StopFallback();
        _fallback = StartCoroutine(FallbackAfterClip(fallback));
    }

    private IEnumerator FallbackAfterClip(Action callback)
    {
        yield return new WaitForSeconds(animationFallbackSeconds);
        callback?.Invoke();
    }

    private void StopFallback()
    {
        if (_fallback == null) return;
        StopCoroutine(_fallback);
        _fallback = null;
    }

    private void OnDestroy()
    {
        _disappearTween?.Kill();
    }
}
