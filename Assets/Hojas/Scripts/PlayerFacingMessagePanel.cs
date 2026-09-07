using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// PANEL DE MENSAJE FRENTE AL JUGADOR
///
/// Componente reutilizable para mostrar UN mensaje a la vez anclado frente a la
/// cabeza del jugador. Es la pieza de guia del tutorial y la base de los paneles
/// de la presentacion de enfermedad, pero no sabe nada de ninguno de los dos:
/// solo conoce un UXML con un titulo (message-title), un cuerpo (message-body) y
/// una lista de iconos opcionales (elementos con la clase message-icon, mostrados
/// por nombre).
///
/// EL DOCUMENTO SE PONE EN LA ESCENA, NO SE CREA POR CODIGO.
/// El UIDocument, su UXML, su PanelSettings y su tamaño se configuran a mano en el
/// Inspector, igual que en SessionIntroController y en el resto de paneles del
/// proyecto. Este componente solo lo referencia. Antes se creaba en runtime y eso
/// escondia el tamaño dentro de codigo que no lo aplicaba, ademas de dejar el
/// GameObject invisible en el Editor: no habia forma de ajustarlo ni de verlo.
///
/// COMO SE CONTROLA EL TAMAÑO (importante):
///   metros = pixeles / PanelSettings.pixelsPerUnit * escala mundial del Transform
/// Con el PanelSettings compartido del proyecto (pixelsPerUnit = 100), eso es
/// 100 px ≈ 1 m. En el UIDocument se fija el quad en pixeles
/// (World Space Size Mode = Fixed, ancho y alto) y con la escala del Transform se
/// lleva a los metros que toquen. El gizmo del quad en la vista de escena enseña
/// el resultado sin necesidad de darle a Play.
///
/// Reglas del proyecto que respeta:
/// - Nunca desactiva su propio GameObject: sin UIDocument activo no hay
///   rootVisualElement. Se oculta con display/opacity.
/// - Los fades animan una variable local que se vuelca en style.opacity.
/// - Espera un frame tras hacer visible el panel antes de animar.
/// - La rotacion continua la resuelve el BillboardUI en Y-lock del mismo GameObject.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument), typeof(BillboardUI))]
public class PlayerFacingMessagePanel : MonoBehaviour
{
    [Header("Documento")]
    [Tooltip("UIDocument de este panel, con su UXML y su tamaño ya configurados en la " +
             "escena. Vacio = se usa el del propio GameObject.")]
    [SerializeField] private UIDocument document;

    [Tooltip("Nombre del VisualElement raiz dentro del UXML.")]
    [SerializeField] private string rootElementName = "message-panel";

    [Tooltip("Clase USS que marcan los elementos de icono opcionales dentro del UXML.")]
    [SerializeField] private string iconClassName = "message-icon";

    [Header("Anclaje frente al jugador")]
    [Tooltip("Distancia en metros entre la cabeza del jugador y el panel.")]
    [SerializeField] private float distance = 1.2f;

    [Tooltip("Desplazamiento vertical respecto a la altura de la cabeza. Negativo = un poco debajo de la linea de los ojos.")]
    [SerializeField] private float heightOffset = -0.15f;

    [Header("Animacion")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    public bool IsVisible => _visible;

    /// <summary>
    /// Raiz del UXML ('message-panel'). Expuesta para que una subclase pueda
    /// bindear elementos propios ademas del titulo y el cuerpo. Es null hasta
    /// que el UIDocument resuelve su arbol (ver EnsureElements).
    /// </summary>
    protected VisualElement Root => _root;

    private VisualElement _root;
    private Label _titleLabel;
    private Label _bodyLabel;
    private readonly List<VisualElement> _icons = new List<VisualElement>();

    private Tween _fadeTween;
    private Coroutine _showRoutine;
    private Coroutine _hideRoutine;
    private float _currentOpacity;
    private bool _visible;
    private bool _warned;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Deja el panel oculto antes del primer frame visible. Va en Start y no en
    /// Awake porque el rootVisualElement de un UIDocument no existe hasta su
    /// OnEnable, y el orden entre GameObjects no esta garantizado. Para cuando
    /// corre Start ya han corrido todos los OnEnable, y todavia no se ha dibujado
    /// nada, asi que el contenido nunca parpadea.
    /// </summary>
    protected virtual void Start()
    {
        if (!_visible) HideImmediate();
    }

    // ─── API publica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Muestra un mensaje: lo ancla frente al jugador, aplica los textos y hace
    /// fade in. Cada llamada re-ancla, asi que cada mensaje nuevo se re-centra
    /// frente a donde mire el jugador en ese momento.
    /// </summary>
    public void Show(string title, string body, string iconElementName = null)
    {
        if (!EnsureElements()) return;

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

        if (!EnsureElements()) return;

        _root.style.display = DisplayStyle.None;
        _root.style.opacity = 0f;
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

    // ─── Resolucion del documento ─────────────────────────────────────────────

    /// <summary>
    /// Resuelve el UIDocument de la escena y cachea los elementos del UXML.
    /// Devuelve false, con un aviso una sola vez, si falta algo por cablear: sin
    /// panel el juego sigue, pero en silencio no se entenderia por que.
    /// </summary>
    private bool EnsureElements()
    {
        if (_root != null) return true;

        if (document == null) document = GetComponent<UIDocument>();

        if (document == null)
            return Warn("Falta el UIDocument del panel. Asignalo en el Inspector o ponlo en el mismo GameObject.");

        if (document.rootVisualElement == null)
            return false; // Todavia no ha corrido su OnEnable; se reintenta en la siguiente llamada.

        _root = document.rootVisualElement.Q<VisualElement>(rootElementName);
        if (_root == null)
            return Warn($"El UXML de '{document.name}' no contiene un elemento llamado '{rootElementName}'.");

        _titleLabel = _root.Q<Label>("message-title");
        _bodyLabel = _root.Q<Label>("message-body");

        _icons.Clear();
        _root.Query(className: iconClassName).ToList(_icons);
        foreach (VisualElement icon in _icons)
            icon.style.display = DisplayStyle.None;

        return true;
    }

    private bool Warn(string message)
    {
        if (_warned) return false;
        _warned = true;
        Debug.LogWarning($"[MessagePanel] {name}: {message}", this);
        return false;
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

        ApplyExtraContent();
    }

    /// <summary>
    /// Hook para subclases: se llama tras aplicar titulo, cuerpo e iconos, y antes
    /// de anclar el panel. Aqui una subclase bindea los elementos propios de su
    /// UXML (imagenes, filas, etiquetas extra). La base no hace nada.
    /// </summary>
    protected virtual void ApplyExtraContent() { }

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

    // Virtuales a proposito: si una subclase declarara su propio OnDisable privado,
    // Unity despacharia solo el de la subclase y el fade se quedaria vivo.
    protected virtual void OnDisable()
    {
        _fadeTween?.Kill();
        _showRoutine = null;
        _hideRoutine = null;
    }

    protected virtual void OnDestroy()
    {
        _fadeTween?.Kill();
    }
}
