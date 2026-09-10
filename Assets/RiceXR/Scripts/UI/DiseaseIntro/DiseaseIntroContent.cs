using System;
using UnityEngine;

/// <summary>
/// Una fila de la lista de un panel de presentacion: un rotulo corto y su
/// detalle. El panel 2 las usa para "agente causal / como se propaga / que
/// buscar" y el panel 3 para explicar que separa un nivel de daño del siguiente.
/// </summary>
[Serializable]
public class DiseaseIntroRow
{
    [Tooltip("Rotulo corto de la fila. Dos o tres palabras.")]
    public string title;

    [Tooltip("Detalle de la fila. Una linea; en VR menos texto se lee mejor.")]
    public string detail;
}

/// <summary>
/// TEXTOS DE LA PRESENTACION DE UNA ENFERMEDAD
///
/// Lo que se cuenta en los tres paneles que aparecen antes de que las hojas
/// crezcan:
///   1) Portada        — que enfermedad toca
///   2) La enfermedad  — que es y como se reconoce
///   3) El daño        — como se mide de 1 a 5
///
/// Vive dentro de <see cref="PanelDiseaseData"/> a proposito: asi sigue habiendo
/// UN solo asset por enfermedad (el que ya esta puesto en
/// UIGameListener.diseaseDataPerLevel) y no hay dos sitios que mantener
/// sincronizados.
///
/// Lo que NO se repite aqui porque ya vive en PanelDiseaseData: titulo, nombre
/// cientifico, categoria, imagenes del carrusel y la imagen que muestra como
/// avanza la enfermedad. Los paneles las leen de ahi.
/// </summary>
[Serializable]
public class DiseaseIntroContent
{
    // ─── Panel 1 — Portada ────────────────────────────────────────────────────

    [Header("Panel 1 — Portada")]
    [Tooltip("Una linea de gancho bajo el nombre de la enfermedad.")]
    [TextArea(2, 4)]
    public string coverTagline;

    [Tooltip("Imagen grande de la portada. Si se deja vacia se usa la primera del carrusel (PanelDiseaseData.images).")]
    public Texture2D coverImage;

    [Tooltip("Id de linea de narracion del panel 1 (ver GameNarrationLineIds). Vacio = sin audio.")]
    public string coverNarrationLineId;

    // ─── Panel 2 — La enfermedad ──────────────────────────────────────────────

    [Header("Panel 2 — La enfermedad")]
    [Tooltip("Titular del panel 2. Por ejemplo: '¿Que es esta enfermedad?'.")]
    public string infoHeadline;

    [Tooltip("Descripcion larga. Aqui cabe entera.")]
    [TextArea(3, 8)]
    public string infoBody;

    [Tooltip("Hasta 3 filas. Las que se dejen vacias no se muestran.")]
    public DiseaseIntroRow[] infoRows;

    [Tooltip("Imagen de una hoja enferma para el panel 2.")]
    public Texture2D infoImage;

    [Tooltip("Id de linea de narracion del panel 2. Vacio = sin audio.")]
    public string infoNarrationLineId;

    // ─── Panel 3 — El daño ────────────────────────────────────────────────────

    [Header("Panel 3 — El daño (severidad)")]
    [Tooltip("Titular del panel 3. Por ejemplo: 'Como se mide el daño'.")]
    public string severityHeadline;

    [Tooltip("Por que importa la severidad y como se lee la escala del 1 al 5.")]
    [TextArea(3, 8)]
    public string severityBody;

    [Tooltip("Hasta 3 filas explicando que separa un nivel del siguiente.")]
    public DiseaseIntroRow[] severityRows;

    [Tooltip("Id de linea de narracion del panel 3. Vacio = sin audio.")]
    public string severityNarrationLineId;

    /// <summary>
    /// Sin titulares no hay nada util que enseñar. Lo consulta UIGameListener
    /// para no encadenar tres paneles vacios.
    /// </summary>
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(coverTagline) ||
        !string.IsNullOrWhiteSpace(infoHeadline) ||
        !string.IsNullOrWhiteSpace(severityHeadline);
}
