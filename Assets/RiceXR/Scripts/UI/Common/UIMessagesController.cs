using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Controlador de mensajes de feedback al jugador.
/// Responsabilidad: mostrar y ocultar los paneles de mensaje con animaciones.
/// No conoce la lógica del juego — recibe órdenes de UIGameListener.
///
/// Nota: los paneles de introducción de nivel (nextLevel, finalLevel, tutorial)
/// son ahora responsabilidad de <see cref="LevelIntroController"/>. El progreso
/// del nivel lo muestra el mensaje de acierto de <see cref="DiseaseSelectionSystem"/>.
/// </summary>
public class UIMessagesController : MonoBehaviour
{
    [Header("UI Documents — uno por mensaje")]
    [SerializeField] private UIDocument docCongrats;

    [Header("Animación")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.2f;


    [Header("Anclaje sobre superficie (felicitación)")]
    [SerializeField] private float surfaceHeightOffset = 0.22f;

    [Header("Evitar el volumen de juego")]
    [Tooltip("Con un plano seleccionado, los paneles se colocan fuera del volumen que ocupan las hojas.")]
    [SerializeField] private bool avoidPlayVolume = true;

    [Tooltip("Altura en metros que alcanzan las hojas sobre la superficie.")]
    [Min(0f)] [SerializeField] private float leafHeight = 0.9f;

    [Tooltip("Holgura en metros alrededor del volumen de juego.")]
    [Min(0f)] [SerializeField] private float volumeMargin = 0.15f;

    [Header("Anclaje frente al jugador")]
    [Min(0f)] [SerializeField] private float panelDistance = 1.2f;
    [SerializeField] private float panelHeightOffset = -0.15f;

    private VisualElement _rootCongrats;

    private readonly Dictionary<VisualElement, Tween> _activeTweens = new Dictionary<VisualElement, Tween>();

    private void Awake()
    {
        _rootCongrats = docCongrats?.rootVisualElement.Q<VisualElement>("msg-congrats");
        HideAllImmediate();
    }

    private void OnDestroy()
    {
        foreach (var t in _activeTweens.Values) t?.Kill();
    }

    public void ShowCongrats(string subtitle = null)
    {
        if (subtitle != null) _rootCongrats?.Q<Label>("congrats-subtitle").SetText(subtitle);
        PlaceOverSelectedSurface(docCongrats, Vector3.zero);
        ShowPanel(_rootCongrats);
    }



    public void HideAll()
    {
        FadeOutPanel(_rootCongrats);
    }

    private void ShowPanel(VisualElement panel)
    {
        if (panel == null) return;
        KillTween(panel);

        panel.style.display = DisplayStyle.Flex;
        panel.style.opacity = 0f;
        panel.style.translate = new StyleTranslate(new Translate(0, 12, 0));

        float opacity = 0f;
        _activeTweens[panel] = DOTween.To(
            () => opacity,
            t =>
            {
                opacity = t;
                panel.style.opacity = t;
                panel.style.translate = new StyleTranslate(new Translate(0, Mathf.Lerp(12f, 0f, t), 0));
            },
            1f, fadeInDuration
        ).SetEase(Ease.OutCubic);
    }

    private void FadeOutPanel(VisualElement panel)
    {
        if (panel == null || panel.style.display == DisplayStyle.None) return;
        KillTween(panel);

        float opacity = panel.resolvedStyle.opacity;
        _activeTweens[panel] = DOTween.To(
            () => opacity,
            t => { opacity = t; panel.style.opacity = t; },
            0f, fadeOutDuration
        )
        .SetEase(Ease.InQuad)
        .OnComplete(() => panel.style.display = DisplayStyle.None);
    }

    private void HideAllImmediate()
    {
        SetHidden(_rootCongrats);
    }

    private static void SetHidden(VisualElement panel)
    {
        if (panel == null) return;
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
    }

    private void KillTween(VisualElement panel)
    {
        if (_activeTweens.TryGetValue(panel, out var t)) { t?.Kill(); _activeTweens.Remove(panel); }
    }

    private void PlaceOverSelectedSurface(UIDocument doc, Vector3 offset)
    {
        if (doc == null) return;

        if (avoidPlayVolume &&
            GuidancePanelPlacer.TryResolve(panelDistance, panelHeightOffset, leafHeight, volumeMargin, out Vector3 safePosition))
        {
            doc.gameObject.transform.position = safePosition + offset;
            FaceHead(doc.gameObject.transform);
            return;
        }

        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            Vector3 basePos = SceneInteractionManager.Instance.SelectedPlanePosition
                              + (SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up) * surfaceHeightOffset;
            doc.gameObject.transform.position = basePos + offset;
            return;
        }

        if (Camera.main == null) return;
        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
        doc.gameObject.transform.position =
            cam.position + fwd.normalized;
    }

    /// <summary>
    /// Orienta el panel hacia la cabeza del jugador manteniendolo vertical (Y-lock).
    /// </summary>
    private static void FaceHead(Transform panel)
    {
        if (Camera.main == null) return;

        Vector3 toHead = Camera.main.transform.position - panel.position;
        toHead.y = 0f;
        if (toHead.sqrMagnitude > 0.0001f)
            panel.rotation = Quaternion.LookRotation(-toHead, Vector3.up);
    }
}

internal static class LabelExtensions
{
    internal static void SetText(this Label label, string text)
    {
        if (label != null) label.text = text;
    }
}
