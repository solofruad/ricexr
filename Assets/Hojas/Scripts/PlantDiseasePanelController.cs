using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

/// <summary>
/// Controlador del panel de información de enfermedad (PlantDiseasePanel.uxml).
///
/// FLUJO POR NIVEL:
/// 1. Show(data) → panel aparece frente al usuario con info de la enfermedad
/// 2. StartProgressBar() → barra de análisis corre
/// 3. Al completarse: panel sube (SlideUp), publica DiseaseAnalysisCompleted
/// 4. Panel permanece visible como referencia durante todo el nivel
/// 5. Hide() al cambiar de nivel → aparece limpio con la nueva enfermedad
///
/// Nivel de evaluación final: no se llama Show() — el panel no aparece.
/// </summary>
public class PlantDiseasePanelController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Texture2D[] carouselImages;

    [Header("Carrusel")]
    [SerializeField] private float carouselInterval = 3f;
    [SerializeField] private float carouselFadeDuration = 0.4f;

    [Header("Barra de progreso")]
    [SerializeField] private float defaultProgressDuration = 5f;

    [Header("Animación de subida al completar análisis")]
    [SerializeField] private float slideUpAmount = 0.25f;
    [SerializeField] private float slideUpDuration = 0.6f;

    [Header("Anclaje sobre superficie")]
    [SerializeField] private float surfaceHeightOffset = 0.18f;

    private VisualElement _root;
    private VisualElement _carouselImage;
    private VisualElement[] _dots;
    private Label _diseaseTitle;
    private Label _scientificName;
    private Label _diseaseDescription;
    private VisualElement _progressFill;
    private Label _progressPercent;
    private Label _progressLabel;

    private int _currentImageIndex;
    private Coroutine _carouselCoroutine;
    private Tween _progressTween;
    private Tween _fadeTween;
    private Tween _slideTween;
    private Tween _carouselFadeOutTween;
    private Tween _carouselFadeInTween;
    private bool _isVisible = false;
    private Vector3 _basePosition;

    // ─────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        CacheElements();
        HideImmediate();
    }

    private void OnDestroy()
    {
        _progressTween?.Kill();
        _fadeTween?.Kill();
        _slideTween?.Kill();
        _carouselFadeOutTween?.Kill();
        _carouselFadeInTween?.Kill();
        StopCarousel();
    }



    /// <summary>
    /// Muestra el panel con los datos de la enfermedad del nivel actual.
    /// Llamar al inicio de cada nivel de aprendizaje (no en el nivel final de evaluación).
    /// </summary>
    public void Show(PanelDiseaseData data)
    {
        if (_root == null || data == null) return;

        RepositionPanel();

        _diseaseTitle.text = data.title;
        _scientificName.text = data.scientificName;
        _diseaseDescription.text = data.description;

        if (data.images != null && data.images.Length > 0)
            carouselImages = data.images;

        ResetProgressBar();
        StartCarousel();
        FadeIn();
        _isVisible = true;
    }

    /// <summary>
    /// Inicia la barra de progreso. Al completarse el panel sube automáticamente
    /// y publica GameEventBus.OnDiseaseAnalysisCompleted para que el cultivo aparezca.
    /// </summary>
    public void StartProgressBar(float duration = -1f)
    {
        if (_root == null) return;
        if (duration <= 0f) duration = defaultProgressDuration;

        ResetProgressBar();
        _progressLabel.text = "Analizando muestra...";

        float progress = 0f;
        _progressTween = DOTween.To(
            () => progress,
            x =>
            {
                progress = x;
                _progressFill.style.width = Length.Percent(x * 100f);
                _progressPercent.text = $"{(int)(x * 100f)}%";
            },
            1f, duration
        )
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            _progressLabel.text = "Listo";
            GameEventBus.PublishDiseaseAnalysisCompleted();
            SlideUp();
        });
    }

    /// <summary>
    /// Sube el panel para dejar espacio al cultivo que aparece debajo.
    /// Se llama automáticamente al completarse la barra.
    /// </summary>
    public void SlideUp()
    {
        _slideTween?.Kill();
        Vector3 target = transform.position + Vector3.up * slideUpAmount;
        _slideTween = transform.DOMove(target, slideUpDuration).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// Oculta el panel. Llamar al avanzar de nivel para mostrar la nueva enfermedad.
    /// </summary>
    public void Hide()
    {
        if (_root == null || !_isVisible) return;
        _isVisible = false;

        StopCarousel();
        _carouselFadeOutTween?.Kill();
        _carouselFadeInTween?.Kill();
        _progressTween?.Kill();
        _slideTween?.Kill();

        _fadeTween?.Kill();
        float opacity = _root.resolvedStyle.opacity;
        _fadeTween = DOTween.To(
            () => opacity,
            v => { opacity = v; _root.style.opacity = v; },
            0f, 0.25f
        )
        .SetEase(Ease.InQuad)
        .OnComplete(() => _root.style.display = DisplayStyle.None);
    }

    // ─────────────────────────────────────────────
    // Carrusel
    // ─────────────────────────────────────────────

    public void StartCarousel()
    {
        StopCarousel();
        if (carouselImages == null || carouselImages.Length == 0) return;
        _carouselCoroutine = StartCoroutine(CarouselRoutine());
    }

    public void StopCarousel()
    {
        if (_carouselCoroutine == null) return;
        StopCoroutine(_carouselCoroutine);
        _carouselCoroutine = null;
    }

    private IEnumerator CarouselRoutine()
    {
        _currentImageIndex = 0;
        SetCarouselFrame(_currentImageIndex);
        while (true)
        {
            yield return new WaitForSeconds(carouselInterval);
            int next = (_currentImageIndex + 1) % carouselImages.Length;
            yield return StartCoroutine(CrossfadeToFrame(next));
        }
    }

    private IEnumerator CrossfadeToFrame(int index)
    {
        bool done = false;
        float alphaOut = 1f;

        _carouselFadeOutTween?.Kill();
        _carouselFadeInTween?.Kill();

        _carouselFadeOutTween = DOTween.To(() => alphaOut, a =>
        {
            alphaOut = a;
            _carouselImage.style.backgroundColor = new Color(0f, 0f, 0f, 1f - a);
        }, 0f, carouselFadeDuration * 0.5f)
        .SetEase(Ease.InQuad)
        .OnComplete(() =>
        {
            SetCarouselFrame(index);
            float alphaIn = 0f;
            _carouselFadeInTween = DOTween.To(() => alphaIn, a =>
            {
                alphaIn = a;
                _carouselImage.style.backgroundColor = new Color(0f, 0f, 0f, 1f - a);
            }, 1f, carouselFadeDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => done = true);
        });

        yield return new WaitUntil(() => done);
    }

    private void SetCarouselFrame(int index)
    {
        _currentImageIndex = index;
        if (carouselImages != null && index < carouselImages.Length)
            _carouselImage.style.backgroundImage = new StyleBackground(carouselImages[index]);

        for (int i = 0; i < _dots.Length; i++)
        {
            if (_dots[i] == null) continue;
            bool active = i == index;
            _dots[i].style.width = active ? 20 : 5;
            _dots[i].style.backgroundColor = active
                ? new Color(1f, 1f, 1f, 0.95f)
                : new Color(1f, 1f, 1f, 0.28f);
        }
    }

    // ─────────────────────────────────────────────
    // Helpers privados
    // ─────────────────────────────────────────────

    private void CacheElements()
    {
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        _carouselImage = _root.Q<VisualElement>("carousel-image");
        _diseaseTitle = _root.Q<Label>("disease-title");
        _scientificName = _root.Q<Label>("scientific-name");
        _diseaseDescription = _root.Q<Label>("disease-description");
        _progressFill = _root.Q<VisualElement>("progress-fill");
        _progressPercent = _root.Q<Label>("progress-percent");
        _progressLabel = _root.Q<Label>("progress-label");
        _dots = new[]
        {
            _root.Q<VisualElement>("dot-0"), 
            _root.Q<VisualElement>("dot-1"),
            _root.Q<VisualElement>("dot-2"), 
            _root.Q<VisualElement>("dot-3"),
        };
    }

    private void ResetProgressBar()
    {
        _progressTween?.Kill();
        if (_progressFill != null) _progressFill.style.width = Length.Percent(0f);
        if (_progressPercent != null) _progressPercent.text = "0%";
        if (_progressLabel != null) _progressLabel.text = "Analizando muestra...";
    }

    private void FadeIn()
    {
        _fadeTween?.Kill();
        _root.style.display = DisplayStyle.Flex;
        _root.style.opacity = 0f;
        float opacity = 0f;
        _fadeTween = DOTween.To(
            () => opacity,
            v => { opacity = v; _root.style.opacity = v; },
            1f, 0.3f
        ).SetEase(Ease.OutCubic);
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
                                 + (SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up) * surfaceHeightOffset;
            _basePosition = transform.position;
            return;
        }

        if (Camera.main == null) return;
        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
        transform.position = cam.position + fwd.normalized;
        _basePosition = transform.position;
    }
}