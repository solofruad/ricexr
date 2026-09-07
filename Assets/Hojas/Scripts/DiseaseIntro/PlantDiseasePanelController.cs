using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Serialization;
using DG.Tweening;

/// <summary>
/// PANEL DE CONSULTA DE LA ENFERMEDAD (PlantDiseasePanel.uxml)
///
/// La ficha que queda a mano durante todo el nivel: nombre, nombre cientifico,
/// descripcion, carrusel de fotos y la imagen de como avanza la enfermedad. Es
/// el recordatorio de lo que ya se explico en los tres paneles previos, por si
/// al jugador se le olvida algo mientras diagnostica.
///
/// FLUJO POR NIVEL:
/// 1. DiseaseIntroController explica la enfermedad en tres paneles
/// 2. Al continuar en el ultimo: Show(data) → esta ficha aparece flotando sobre
///    la superficie, a la altura que deja sitio a las hojas
/// 3. UIGameListener publica DiseaseAnalysisCompleted → las hojas crecen
/// 4. La ficha se queda visible todo el nivel
/// 5. Hide() al cambiar de nivel → vuelve a aparecer limpia con la siguiente
///
/// Nivel de evaluacion final: no se llama Show() — la ficha no aparece, ahi el
/// jugador va sin ayuda.
/// </summary>
public class PlantDiseasePanelController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Texture2D[] carouselImages;

    [Header("Carrusel")]
    [SerializeField] private float carouselInterval = 3f;
    [SerializeField] private float carouselFadeDuration = 0.4f;

    [Header("Colocacion sobre la superficie")]
    [Tooltip("Altura de la ficha sobre la superficie elegida, en metros.")]
    [SerializeField] private float surfaceHeightOffset = 0.18f;

    [Tooltip("Altura extra para que la ficha no tape las hojas que crecen debajo.")]
    [FormerlySerializedAs("slideUpAmount")]
    [SerializeField] private float leafClearance = 0.25f;

    private VisualElement _root;
    private VisualElement _carouselImage;
    private VisualElement[] _dots;
    private Label _diseaseTitle;
    private Label _scientificName;
    private Label _diseaseDescription;
    private Image _severityEvolutionImage;

    private int _currentImageIndex;
    private Coroutine _carouselCoroutine;
    private Tween _fadeTween;
    private Tween _carouselFadeOutTween;
    private Tween _carouselFadeInTween;
    private bool _isVisible = false;

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
        _fadeTween?.Kill();
        _carouselFadeOutTween?.Kill();
        _carouselFadeInTween?.Kill();
        StopCarousel();
    }

    // ─────────────────────────────────────────────
    // API publica
    // ─────────────────────────────────────────────

    /// <summary>
    /// Muestra la ficha de la enfermedad del nivel actual, ya colocada a la
    /// altura final: las hojas aparecen a la vez, asi que no hay nada que
    /// esperar ni que subir despues.
    ///
    /// No publica DiseaseAnalysisCompleted: de eso se encarga UIGameListener,
    /// que es quien sabe en que momento del flujo estamos.
    /// </summary>
    public void Show(PanelDiseaseData data)
    {
        if (_root == null || data == null) return;

        RepositionPanel();

        _diseaseTitle.text = data.title;
        _scientificName.text = data.scientificName;
        _diseaseDescription.text = data.description;
        SetSeverityEvolutionImage(data.severityEvolutionImage);

        if (data.images != null && data.images.Length > 0)
            carouselImages = data.images;

        StartCarousel();
        FadeIn();
        _isVisible = true;
    }

    /// <summary>
    /// Oculta la ficha. Llamar al avanzar de nivel para mostrar la siguiente
    /// enfermedad desde cero.
    /// </summary>
    public void Hide()
    {
        if (_root == null || !_isVisible) return;
        _isVisible = false;

        StopCarousel();
        _carouselFadeOutTween?.Kill();
        _carouselFadeInTween?.Kill();

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

    public void SetSeverityEvolutionImage(Texture2D image)
    {
        if (_severityEvolutionImage == null) return;
        if (_severityEvolutionImage.image == image) return;

        _severityEvolutionImage.style.backgroundImage = StyleKeyword.None;
        _severityEvolutionImage.image = image;
        _severityEvolutionImage.style.display = image != null ? DisplayStyle.Flex : DisplayStyle.None;
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
        _severityEvolutionImage = _root.Q<Image>("severity-evolution");
        _dots = new[]
        {
            _root.Q<VisualElement>("dot-0"),
            _root.Q<VisualElement>("dot-1"),
            _root.Q<VisualElement>("dot-2"),
            _root.Q<VisualElement>("dot-3"),
        };
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

    /// <summary>
    /// Coloca la ficha sobre la superficie elegida (o frente a la camara si no
    /// hay ninguna), sumando la altura extra que deja libres las hojas.
    /// </summary>
    private void RepositionPanel()
    {
        Vector3 target;

        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            target = SceneInteractionManager.Instance.SelectedPlanePosition
                     + SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up * surfaceHeightOffset;
        }
        else
        {
            if (Camera.main == null) return;
            Transform cam = Camera.main.transform;
            Vector3 fwd = cam.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
            target = cam.position + fwd.normalized;
        }

        transform.position = target + Vector3.up * leafClearance;
    }
}
