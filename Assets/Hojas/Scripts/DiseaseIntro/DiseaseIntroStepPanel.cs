using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Lo que un panel de la presentacion necesita pintar. Lo arma
/// DiseaseIntroController a partir de PanelDiseaseData + DiseaseIntroContent,
/// para que el panel no tenga que conocer el modelo de datos del juego.
/// </summary>
public class DiseaseIntroStepView
{
    public string title;
    public string body;

    /// <summary>Rotulo pequeño sobre el titulo (la categoria, en la portada).</summary>
    public string eyebrow;

    /// <summary>Nombre cientifico, solo en la portada.</summary>
    public string scientificName;

    /// <summary>Imagen principal del panel (la grande de portada, o la hoja enferma).</summary>
    public Texture2D image;

    /// <summary>Imagen que muestra como avanza la enfermedad, solo en el panel 3.</summary>
    public Texture2D severityImage;

    /// <summary>Filas de la lista. Las que falten se ocultan.</summary>
    public DiseaseIntroRow[] rows;
}

/// <summary>
/// UNO DE LOS TRES PANELES QUE PRESENTAN LA ENFERMEDAD
///
/// Hereda de <see cref="PlayerFacingMessagePanel"/> para reutilizar tal cual el
/// anclaje frente al jugador, el billboard, el fade y las reglas de UI Toolkit
/// del proyecto (nunca desactivar el GameObject, animar volcando en
/// style.opacity, esperar un frame antes de animar). Lo unico que añade es
/// llenar los elementos propios de su UXML y la animacion de entrada escalonada.
///
/// A diferencia del tutorial, este panel NO se recoloca en cada Show: el
/// controlador fija una posicion comun para los tres, para que no salten si el
/// jugador gira la cabeza entre uno y otro.
///
/// Que espera de la escena: un UIDocument en el mismo GameObject, con su UXML, el
/// PanelSettings compartido y su tamaño ya configurados a mano (ver la nota sobre
/// pixeles y metros en PlayerFacingMessagePanel). Nada de esto se crea por codigo.
///
/// Que espera del UXML (ver Assets/Hojas/UI/DiseaseIntro/):
///   message-panel          raiz (lo exige la clase base)
///   message-title          titulo del panel
///   message-body           cuerpo del panel
///   disease-intro-category rotulo pequeño superior       (opcional)
///   disease-intro-scientific nombre cientifico           (opcional)
///   disease-intro-image    imagen principal del panel    (opcional)
///   severity-evolution     imagen de avance de la enfermedad (opcional)
///   disease-intro-row-0..2 filas, con etiquetas de clase
///                          .disease-intro__row-title / .disease-intro__row-detail
/// </summary>
[DisallowMultipleComponent]
public class DiseaseIntroStepPanel : PlayerFacingMessagePanel
{
    /// <summary>Cuantas filas como maximo puede declarar el UXML de un panel.</summary>
    public const int MaxRows = 3;

    [Header("Entrada escalonada (elementos con clase 'reveal')")]
    [Tooltip("Segundos entre el arranque de un elemento y el siguiente.")]
    [SerializeField] private float revealStagger = 0.14f;

    [Tooltip("Duracion de la entrada de cada elemento.")]
    [SerializeField] private float revealDuration = 0.45f;

    [Tooltip("Cuanto sube cada elemento al entrar, en pixeles de UI Toolkit.")]
    [SerializeField] private float revealOffsetY = 24f;

    private readonly List<Tween> _revealTweens = new List<Tween>();

    private Vector3 _anchor;
    private bool _hasAnchor;

    private DiseaseIntroStepView _pendingView;

    // ─── API publica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fija donde se coloca el panel. La calcula el controlador una sola vez al
    /// empezar, y la comparten los tres paneles y el boton.
    /// </summary>
    public void SetAnchor(Vector3 worldPosition)
    {
        _anchor = worldPosition;
        _hasAnchor = true;
    }

    /// <summary>
    /// Muestra el panel: llena el contenido, deja los elementos de entrada
    /// preparados y hace el fade de la clase base. La entrada escalonada la
    /// lanza despues el controlador con <see cref="PlayReveal"/>, cuando el
    /// layout ya esta resuelto.
    /// </summary>
    public void ShowStep(DiseaseIntroStepView view)
    {
        if (view == null) return;

        CancelReveal();
        _pendingView = view;
        Show(view.title, view.body);
    }

    /// <summary>
    /// Encadena la entrada de los elementos. Llamar tras esperar un frame desde
    /// <see cref="ShowStep"/>.
    /// </summary>
    public IEnumerator PlayReveal()
    {
        yield return UIRevealSequence.Run(
            gameObject, Root, revealStagger, revealDuration, revealOffsetY, _revealTweens);
    }

    /// <summary>Corta la entrada en curso. La usa el controlador al cancelar.</summary>
    public void CancelReveal()
    {
        for (int i = 0; i < _revealTweens.Count; i++)
            _revealTweens[i]?.Kill();
        _revealTweens.Clear();
    }

    // ─── Colocacion ───────────────────────────────────────────────────────────

    /// <summary>
    /// Posicion fija en vez de "frente a donde mire ahora el jugador". Si nadie
    /// la asigno todavia, se cae al comportamiento de la clase base.
    /// </summary>
    protected override Vector3 ResolvePlacementPosition()
    {
        return _hasAnchor ? _anchor : base.ResolvePlacementPosition();
    }

    // ─── Contenido ────────────────────────────────────────────────────────────

    protected override void ApplyExtraContent()
    {
        if (Root == null || _pendingView == null) return;

        SetLabel("disease-intro-category", _pendingView.eyebrow);
        SetLabel("disease-intro-scientific", _pendingView.scientificName);

        SetImage("disease-intro-image", _pendingView.image);
        SetImage("severity-evolution", _pendingView.severityImage);

        ApplyRows(_pendingView.rows);

        // Estado inicial de la entrada. Se hace aqui porque esto corre dentro de
        // Show(), antes de que el panel se vuelva visible, y asi nunca hay un
        // frame con todo ya colocado.
        UIRevealSequence.Prepare(Root, revealOffsetY);
    }

    /// <summary>
    /// Llena las filas que declara el UXML y oculta las que no tienen datos, para
    /// que un panel con dos filas no deje un hueco donde iba la tercera.
    /// </summary>
    private void ApplyRows(DiseaseIntroRow[] rows)
    {
        for (int i = 0; i < MaxRows; i++)
        {
            VisualElement rowElement = Root.Q<VisualElement>($"disease-intro-row-{i}");
            if (rowElement == null) continue;

            DiseaseIntroRow row = rows != null && i < rows.Length ? rows[i] : null;
            bool hasRow = row != null &&
                          (!string.IsNullOrWhiteSpace(row.title) || !string.IsNullOrWhiteSpace(row.detail));

            rowElement.style.display = hasRow ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasRow) continue;

            Label titleLabel = rowElement.Q<Label>(className: "disease-intro__row-title");
            Label detailLabel = rowElement.Q<Label>(className: "disease-intro__row-detail");

            if (titleLabel != null) titleLabel.text = row.title;
            if (detailLabel != null) detailLabel.text = row.detail;
        }
    }

    /// <summary>Pone el texto y oculta el elemento si viene vacio.</summary>
    private void SetLabel(string elementName, string text)
    {
        Label label = Root.Q<Label>(elementName);
        if (label == null) return;

        bool hasText = !string.IsNullOrWhiteSpace(text);
        label.text = hasText ? text : string.Empty;
        label.style.display = hasText ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Pone la imagen y oculta el elemento si no hay ninguna. Se limpia
    /// backgroundImage por si el UXML trae una imagen de ejemplo puesta en el
    /// atributo style: si no se quita, gana sobre la que asigna el codigo.
    ///
    /// El modo de escalado NO se toca aqui: lo declara cada UXML con scale-mode,
    /// porque la portada recorta para llenar y la imagen de avance de la
    /// enfermedad tiene que caber entera.
    /// </summary>
    private void SetImage(string elementName, Texture2D texture)
    {
        Image image = Root.Q<Image>(elementName);
        if (image == null) return;

        image.style.backgroundImage = StyleKeyword.None;
        image.image = texture;
        image.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ─── Limpieza ─────────────────────────────────────────────────────────────

    protected override void OnDisable()
    {
        base.OnDisable();
        CancelReveal();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CancelReveal();
    }
}
