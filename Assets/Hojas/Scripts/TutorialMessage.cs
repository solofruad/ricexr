using System;

/// <summary>
/// Un mensaje de guia del tutorial. Se muestran de a uno, frente al jugador,
/// en lugar de paneles apilados sobre la mesa.
/// </summary>
[Serializable]
public class TutorialMessage
{
    [Tooltip("Titulo corto del mensaje. Una frase como maximo.")]
    public string title;

    [Tooltip("Una sola linea de detalle. En VR, menos texto = mas claro.")]
    public string body;

    [Tooltip("Nombre del elemento de icono dentro del UXML (icon-grab, icon-observe, icon-diagnose). Vacio = sin icono.")]
    public string iconElement;
}
