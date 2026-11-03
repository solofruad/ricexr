using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// FEEDBACK DE HOVER Y PULSACION PARA BOTONES DE UI TOOLKIT
///
/// Los botones del proyecto son un VisualElement dibujado por un UIDocument y un
/// PokeInteractable del Interaction SDK que recibe el toque. Como UI Toolkit solo
/// dibuja, el boton no sabe si el dedo se acerca o lo pulsa. Este componente lee el
/// estado del PokeInteractable y lo traslada al elemento:
/// - Hover: clase USS <c>poke-hover</c> y un progreso de acercamiento (0 al entrar en
///   hover, 1 sobre la superficie) que encoge un poco el boton y refuerza su borde.
/// - Select: clase USS <c>poke-pressed</c> y el boton hundido del todo.
/// - Normal: se retiran las clases y los estilos en linea.
///
/// El sonido de hover y de pulsacion es opcional: sin AudioSource o sin clip no se
/// reproduce nada. Las clases viven en RiceXRUI.uss junto a <c>quest-button</c>.
/// </summary>
[DisallowMultipleComponent]
public class PokeButtonFeedback : MonoBehaviour
{
    private const string HoverClass = "poke-hover";
    private const string PressedClass = "poke-pressed";

    [Header("Referencias")]
    [Tooltip("PokeInteractable que recibe el toque de este boton.")]
    [SerializeField] private PokeInteractable interactable;

    [Tooltip("UIDocument que dibuja el boton.")]
    [SerializeField] private UIDocument document;

    [Tooltip("Nombre del VisualElement del boton dentro del UXML.")]
    [SerializeField] private string buttonElementName = "intro-continue-button";

    [Header("Escala")]
    [Tooltip("Escala del boton al llegar a la superficie en hover.")]
    [Range(0.5f, 1f)] [SerializeField] private float hoverMinScale = 0.94f;

    [Tooltip("Escala del boton mientras esta pulsado.")]
    [Range(0.5f, 1f)] [SerializeField] private float pressedScale = 0.9f;

    [Header("Borde")]
    [Tooltip("Color del borde resaltado; su alfa crece con el acercamiento.")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Audio (opcional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip pressClip;

    private VisualElement _button;
    private InteractableState _state = InteractableState.Normal;
    private bool _warned;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (interactable == null) return;

        interactable.WhenStateChanged += HandleStateChanged;
        ApplyState(interactable.State, false);
    }

    private void OnDisable()
    {
        if (interactable != null) interactable.WhenStateChanged -= HandleStateChanged;

        _state = InteractableState.Normal;
        ClearVisuals();
    }

    private void Update()
    {
        if (_state != InteractableState.Hover || !EnsureButton()) return;

        ApplyApproach(ComputeApproachProgress());
    }

    // ─── Estado ───────────────────────────────────────────────────────────────

    private void HandleStateChanged(InteractableStateChangeArgs args)
    {
        ApplyState(args.NewState, true);
    }

    private void ApplyState(InteractableState state, bool playSounds)
    {
        InteractableState previous = _state;
        _state = state;

        if (playSounds)
        {
            if (state == InteractableState.Hover && previous == InteractableState.Normal) Play(hoverClip);
            else if (state == InteractableState.Select) Play(pressClip);
        }

        if (!EnsureButton()) return;

        switch (state)
        {
            case InteractableState.Select:
                _button.RemoveFromClassList(HoverClass);
                _button.AddToClassList(PressedClass);
                SetScale(pressedScale);
                SetBorderAlpha(1f);
                break;

            case InteractableState.Hover:
                _button.RemoveFromClassList(PressedClass);
                _button.AddToClassList(HoverClass);
                ApplyApproach(ComputeApproachProgress());
                break;

            default:
                ClearVisuals();
                break;
        }
    }

    // ─── Acercamiento ─────────────────────────────────────────────────────────

    /// <summary>
    /// Progreso del dedo mas cercano: 0 a la distancia en que empezo el hover
    /// (EnterHoverNormal) y 1 sobre la superficie del boton.
    /// </summary>
    private float ComputeApproachProgress()
    {
        if (interactable == null) return 0f;

        float enter = Mathf.Max(interactable.EnterHoverNormal, 0.0001f);
        float best = 0f;

        foreach (PokeInteractor interactor in interactable.Interactors)
        {
            if (!interactable.ClosestBackingSurfaceHit(interactor.Origin, out SurfaceHit hit)) continue;

            float distance = Vector3.Dot(interactor.Origin - hit.Point, hit.Normal);
            best = Mathf.Max(best, Mathf.Clamp01(1f - distance / enter));
        }

        return best;
    }

    private void ApplyApproach(float progress)
    {
        SetScale(Mathf.Lerp(1f, hoverMinScale, progress));
        SetBorderAlpha(progress);
    }

    // ─── Estilos ──────────────────────────────────────────────────────────────

    private void SetScale(float scale)
    {
        _button.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
    }

    private void SetBorderAlpha(float alpha)
    {
        Color color = highlightColor;
        color.a *= Mathf.Clamp01(alpha);

        _button.style.borderTopColor = color;
        _button.style.borderRightColor = color;
        _button.style.borderBottomColor = color;
        _button.style.borderLeftColor = color;
    }

    /// <summary>
    /// Retira las clases de estado y los estilos en linea para que el boton vuelva al aspecto de su USS.
    /// </summary>
    private void ClearVisuals()
    {
        if (_button == null) return;

        _button.RemoveFromClassList(HoverClass);
        _button.RemoveFromClassList(PressedClass);
        _button.style.scale = StyleKeyword.Null;
        _button.style.borderTopColor = StyleKeyword.Null;
        _button.style.borderRightColor = StyleKeyword.Null;
        _button.style.borderBottomColor = StyleKeyword.Null;
        _button.style.borderLeftColor = StyleKeyword.Null;
    }

    /// <summary>
    /// Resuelve el elemento del boton. El rootVisualElement puede no existir todavia
    /// cuando se recibe el primer cambio de estado, por lo que se reintenta en cada uso.
    /// </summary>
    private bool EnsureButton()
    {
        if (_button != null) return true;
        if (document == null || document.rootVisualElement == null) return false;

        _button = document.rootVisualElement.Q<VisualElement>(buttonElementName);
        if (_button == null && !_warned)
        {
            _warned = true;
            Debug.LogWarning($"[PokeButtonFeedback] {name}: el UXML no contiene un elemento llamado '{buttonElementName}'.", this);
        }

        return _button != null;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
