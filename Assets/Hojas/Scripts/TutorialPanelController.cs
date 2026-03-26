using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

public class TutorialPanelController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIGameListener uiGameListener;

    [Header("Duración del tutorial (segundos)")]
    [Tooltip("Tiempo que el usuario tiene para leer las instrucciones antes de que inicie el juego.")]
    [SerializeField] private float tutorialDuration = 12f;

    [Header("Animación de subida al completar tutorial")]
    [SerializeField] private float slideUpAmount = 0.25f;
    [SerializeField] private float slideUpDuration = 0.6f;

    [Header("World Space")]
    [SerializeField] private float distanceFromCamera = 1.5f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, 0f);

    [Header("Anclaje sobre superficie")]
    [SerializeField] private float surfaceHeightOffset = 0.28f;

    private VisualElement _root;
    private VisualElement _progressFill;
    private Label _progressLabel;
    private Label _progressPercent;

    private Tween _progressTween;
    private Tween _fadeTween;
    private Tween _slideTween;
    private bool _isVisible = false;

    private void Awake()
    {
        CacheElements();
        HideImmediate();
    }

    private void LateUpdate()
    {
    }

    private void OnDestroy()
    {
        _progressTween?.Kill();
        _fadeTween?.Kill();
        _slideTween?.Kill();
    }

    public void ShowAndStart()
    {
        if (_root == null) return;

        _progressTween?.Kill();
        _slideTween?.Kill();

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
        .OnComplete(StartProgressBar);
    }

    public void Hide()
    {
        if (_root == null || !_isVisible) return;

        _progressTween?.Kill();
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
        });
    }

    private void StartProgressBar()
    {
        _progressLabel.text = "Lee las instrucciones...";
        _progressPercent.text = "0%";
        _progressFill.style.width = Length.Percent(0f);

        float progress = 0f;
        _progressTween = DOTween.To(
            () => progress,
            x =>
            {
                progress = x;
                _progressFill.style.width = Length.Percent(x * 100f);
                _progressPercent.text = $"{(int)(x * 100f)}%";
            },
            1f, tutorialDuration
        )
        .SetEase(Ease.Linear)
        .OnComplete(OnTutorialFinished);
    }

    private void OnTutorialFinished()
    {
        _progressLabel.text = "Tutorial listo";
        uiGameListener?.OnTutorialCompleted();
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
