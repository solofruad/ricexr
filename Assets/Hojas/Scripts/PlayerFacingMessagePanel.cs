using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// PANEL DE MENSAJE FRENTE AL JUGADOR
///
/// Componente reutilizable para mostrar UN mensaje a la vez anclado frente a la
/// cabeza del jugador, en vez de paneles apilados sobre la mesa. Es la pieza de
/// guia del tutorial reformado, pero no sabe nada del tutorial: solo conoce un
/// UXML con un titulo (message-title), un cuerpo (message-body) y una lista de
/// iconos opcionales (elementos con la clase message-icon, mostrados por nombre).
///
/// Reglas del proyecto que respeta:
/// - Nunca desactiva su propio GameObject: sin UIDocument activo no hay
///   rootVisualElement. Se oculta con display/opacity.
/// - Los fades animan una variable local que se vuelca en style.opacity.
/// - Espera un frame tras hacer visible el panel antes de animar.
/// - La rotacion continua la resuelve un BillboardUI en Y-lock del mismo GameObject.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFacingMessagePanel : MonoBehaviour
{
    [Header("Anclaje frente al jugador")]
    [Tooltip("Distancia en metros entre la cabeza del jugador y el panel.")]
    [SerializeField] private float distance = 1.2f;

    [Tooltip("Desplazamiento vertical respecto a la altura de la cabeza. Negativo = un poco debajo de la linea de los ojos.")]
    [SerializeField] private float heightOffset = -0.15f;

    [Header("Tamano del panel (metros)")]
    [Tooltip("Tamano del quad world-space del UIDocument. El contenido del UXML se escala a este tamano.")]
    [SerializeField] private Vector2 worldSize = new Vector2(0.45f, 0.3f);

    [Header("Animacion")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("Documentos (para auto-creacion)")]
    [Tooltip("PanelSettings compartido del proyecto. Solo se usa si el panel crea su propio UIDocument.")]
    [SerializeField] private PanelSettings panelSettings;

    [Tooltip("UXML del mensaje. Solo se usa si el panel crea su propio UIDocument.")]
    [SerializeField] private VisualTreeAsset messageUxml;

    [Tooltip("Clase USS que marcan los elementos de icono opcionales dentro del UXML.")]
    [SerializeField] private string iconClassName = "message-icon";

    public bool IsVisible => _visible;

    private UIDocument _document;
    private VisualElement _root;
    private Label _titleLabel;
    private Label _bodyLabel;
    private readonly List<VisualElement> _icons = new List<VisualElement>();
    private BillboardUI _billboard;

    private Tween _fadeTween;
    private Coroutine _showRoutine;
    private Coroutine _hideRoutine;
    private float _currentOpacity;
    private bool _visible;

    // ─── Configuracion ────────────────────────────────────────────────────────

    /// <summary>
    /// Asigna los assets del panel y crea el UIDocument si hace falta. Lo llama
    /// quien crea este panel por codigo (p. ej. TutorialPanelController).
    /// </summary>
    public void Initialize(PanelSettings settings, VisualTreeAsset uxml)
    {
        panelSettings = settings;
        messageUxml = uxml;
        EnsureDocument();
    }

    // ─── API publica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Muestra un mensaje: lo ancla frente al jugador, aplica los textos y hace
    /// fade in. Cada llamada re-ancla, asi que cada mensaje nuevo se re-centra
    /// frente a donde mire el jugador en ese momento.
    /// </summary>
    public void Show(string title, string body, string iconElementName = null)
    {
        EnsureDocument();
        if (_root == null)
        {
            Debug.LogWarning("[MessagePanel] No se pudo resolver el root del UXML; el mensaje no se muestra.");
            return;
        }

        ApplyContent(title, body, iconElementName);
        PlaceInFrontOfPlayer();

        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        _visible = true;
        _root.style.display = DisplayStyle.Flex;
        _root.style.opacity = 0f;
        _currentOpacity = 0f;

        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(FadeInRoutine());
    }

    /// <summary>
    /// Cambia solo el cuerpo del mensaje visible, sin re-anclar ni re-animar.
    /// </summary>
    public void UpdateBody(string body)
    {
        if (_bodyLabel != null) _bodyLabel.text = body;
    }

    /// <summary>
    /// Oculta el mensaje con fade out y luego lo saca del layout.
    /// </summary>
    public void Hide()
    {
        if (!_visible || _root == null) return;

        _visible = false;
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(FadeOutRoutine());
    }

    /// <summary>
    /// Oculta el mensaje sin animacion. Util al inicializar o reiniciar.
    /// </summary>
    public void HideImmediate()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        _fadeTween?.Kill();
        _visible = false;
        _currentOpacity = 0f;
        if (_root != null)
        {
            _root.style.display = DisplayStyle.None;
            _root.style.opacity = 0f;
        }
    }

    // ─── Colocacion ───────────────────────────────────────────────────────────

    /// <summary>
    /// Posicion del panel: frente a la cabeza del jugador, a la distancia
    /// configurada y con el offset vertical. Hook virtual para que otro sistema
    /// pueda heredar y anclar donde le convenga.
    /// </summary>
    protected virtual Vector3 ResolvePlacementPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;

        // Mirando recto arriba o abajo el forward proyectado se anula; se usa el
        // original para no dividir por cero.
        if (forward.sqrMagnitude < 0.0001f) forward = cam.transform.forward;

        Vector3 position = cam.transform.position + forward.normalized * distance;
        position.y += heightOffset;
        return position;
    }

    private void PlaceInFrontOfPlayer()
    {
        transform.position = ResolvePlacementPosition();

        // Se deja mirando ya a la cabeza para que no entre girandose: BillboardUI
        // solo suaviza a partir de aqui. Misma formula que BillboardUI en Y-lock.
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 toHead = cam.transform.position - transform.position;
        toHead.y = 0f;
        if (toHead.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(-toHead, Vector3.up);
    }

    // ─── Construccion del documento ───────────────────────────────────────────

    private void EnsureDocument()
    {
        if (_document == null) _document = GetComponent<UIDocument>();

        if (_document == null)
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.visualTreeAsset = messageUxml;

            // Misma configuracion que el resto de paneles world-space del proyecto.
            _document.position = Position.Relative;
            _document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            _document.worldSpaceSize = worldSize;
        }

        if (_root == null) TryCacheElements();

        if (_billboard == null)
        {
            _billboard = GetComponent<BillboardUI>();
            if (_billboard == null) _billboard = gameObject.AddComponent<BillboardUI>();
        }
    }

    /// <summary>
    /// El rootVisualElement puede no estar listo en el mismo frame en que se crea
    /// el UIDocument, asi que el cacheo reintenta desde Update hasta que exista.
    /// Importante: tambien oculta el panel apenas lo encuentra, para que el
    /// contenido nunca parpadee visible antes de su primer Show.
    /// </summary>
    private void TryCacheElements()
    {
        if (_document == null || _document.rootVisualElement == null) return;

        _root = _document.rootVisualElement.Q<VisualElement>("message-panel");
        if (_root == null)
        {
            Debug.LogWarning("[MessagePanel] El UXML no contiene un elemento llamado 'message-panel'.");
            return;
        }

        _titleLabel = _root.Q<Label>("message-title");
        _bodyLabel = _root.Q<Label>("message-body");

        _icons.Clear();
        _root.Query(className: iconClassName).ToList(_icons);
        foreach (VisualElement icon in _icons)
            icon.style.display = DisplayStyle.None;

        // Estado inicial: presente en la jerarquia pero fuera del layout.
        _root.style.display = DisplayStyle.None;
        _root.style.opacity = 0f;
    }

    private void Update()
    {
        // Reintento de cacheo hasta que el rootVisualElement exista (ver TryCacheElements).
        if (_root == null && _document != null) TryCacheElements();
    }

    // ─── Contenido ────────────────────────────────────────────────────────────

    private void ApplyContent(string title, string body, string iconElementName)
    {
        if (_titleLabel != null) _titleLabel.text = title;
        if (_bodyLabel != null) _bodyLabel.text = body;

        foreach (VisualElement icon in _icons)
        {
            bool showIcon = !string.IsNullOrEmpty(iconElementName) && icon.name == iconElementName;
            icon.style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    // ─── Animacion ────────────────────────────────────────────────────────────

    private IEnumerator FadeInRoutine()
    {
        // UI Toolkit necesita un frame para resolver el layout tras volver visible
        // un panel, asi que se espera antes de animar.
        yield return null;

        _fadeTween?.Kill();
        _currentOpacity = 0f;
        _fadeTween = DOTween.To(
                () => _currentOpacity,
                value => { _currentOpacity = value; _root.style.opacity = value; },
                1f, fadeInDuration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        _showRoutine = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        _fadeTween?.Kill();
        float from = _currentOpacity;
        Tween tween = DOTween.To(
                () => from,
                value => { from = value; _currentOpacity = value; _root.style.opacity = value; },
                0f, fadeOutDuration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _fadeTween = tween;

        // Un Kill no dispara OnComplete: se consulta el estado del tween, mismo
        // patron que WaitForTween en SessionIntroController.
        while (tween.IsActive() && !tween.IsComplete())
            yield return null;

        // Si durante el fade out se llamo a Show, el panel ya no debe ocultarse.
        if (!_visible && _root != null)
            _root.style.display = DisplayStyle.None;

        _hideRoutine = null;
    }

    private void OnDisable()
    {
        _fadeTween?.Kill();
        _showRoutine = null;
        _hideRoutine = null;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }
}
