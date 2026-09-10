using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// CASCADA DE ENTRADA PARA PANELES DE UI TOOLKIT
///
/// Encadena la aparicion de los elementos marcados con la clase 'reveal' dentro
/// de un panel: cada uno entra con fade + deslizamiento vertical y el siguiente
/// arranca unos milisegundos despues, solapandose.
///
/// Regla de la clase 'reveal': no define estilos. El orden de los elementos EN
/// EL UXML es el orden de la animacion. Es la misma regla que documenta
/// SessionIntro.uss y que SessionIntroController tiene escrita a mano; este
/// helper existe para no repetirla en los paneles de la enfermedad.
///
/// Reglas de UI Toolkit que respeta (ver AGENTS.md):
/// - No hay CanvasGroup: se anima una variable local y se vuelca en style.opacity.
/// - Un Kill no dispara OnComplete, asi que la espera consulta el estado del tween.
/// - Los tweens se enlazan al GameObject dueño para no callbackear sobre objetos
///   ya destruidos o desactivados.
/// </summary>
public static class UIRevealSequence
{
    /// <summary>
    /// Deja cada elemento 'reveal' en su estado inicial: invisible y desplazado
    /// hacia abajo. Llamar ANTES de hacer visible el panel, para que no se vea
    /// un frame con todo ya colocado.
    /// </summary>
    public static void Prepare(VisualElement root, float offsetY)
    {
        if (root == null) return;

        root.Query(className: "reveal").ForEach(element =>
        {
            element.style.opacity = 0f;
            element.style.translate = new Translate(0, offsetY, 0);
        });
    }

    /// <summary>
    /// Lanza la cascada y espera a que termine el ultimo elemento. Los tweens
    /// creados se acumulan en <paramref name="createdTweens"/> para que quien
    /// llama pueda matarlos al cancelar la secuencia.
    /// </summary>
    public static IEnumerator Run(
        GameObject owner,
        VisualElement root,
        float stagger,
        float duration,
        float offsetY,
        List<Tween> createdTweens = null)
    {
        if (root == null) yield break;

        List<VisualElement> elements = root.Query(className: "reveal").ToList();
        if (elements.Count == 0) yield break;

        Tween last = null;
        for (int i = 0; i < elements.Count; i++)
        {
            Tween tween = BuildTween(owner, elements[i], i * stagger, duration, offsetY);
            createdTweens?.Add(tween);
            last = tween;
        }

        // Un Kill no dispara OnComplete: se consulta el estado o la corrutina
        // se quedaria esperando para siempre.
        while (last != null && last.IsActive() && !last.IsComplete())
            yield return null;
    }

    /// <summary>
    /// Un 'reveal' es un tween de progreso 0→1 que se vuelca a la vez en la
    /// opacidad y en el desplazamiento vertical del elemento.
    /// </summary>
    private static Tween BuildTween(GameObject owner, VisualElement element, float delay, float duration, float offsetY)
    {
        float progress = 0f;

        Tween tween = DOTween.To(
                () => progress,
                value =>
                {
                    progress = value;
                    element.style.opacity = value;
                    element.style.translate = new Translate(0, (1f - value) * offsetY, 0);
                },
                1f, duration)
            .SetUpdate(true)
            .SetEase(Ease.OutCubic)
            .SetDelay(delay);

        if (owner != null) tween.SetLink(owner, LinkBehaviour.KillOnDisable);
        return tween;
    }
}
