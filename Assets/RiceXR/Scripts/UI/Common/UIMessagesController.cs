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
/// son ahora responsabilidad de <see cref="LevelIntroController"/>.
/// </summary>
public class UIMessagesController : MonoBehaviour
{
    [Header("UI Documents — uno por mensaje")]
    [SerializeField] private UIDocument docCongrats;
    [SerializeField] private UIDocument docProgress;

    [Header("Animación")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float progressVisibleDuration = 2.5f;


    [Header("Anclaje sobre superficie")]
    [SerializeField] private float surfaceHeightOffset = 0.22f;

    private VisualElement _rootCongrats;
    private VisualElement _rootProgress;

    private VisualElement _progressDotsContainer;
    private readonly List<VisualElement> _dynamicProgressDots = new List<VisualElement>();
    private readonly Dictionary<VisualElement, Tween> _activeTweens = new Dictionary<VisualElement, Tween>();
    private Tween _progressAutoHideTween;

    private void Awake()
    {
        _rootCongrats = docCongrats?.rootVisualElement.Q<VisualElement>("msg-congrats");
        _rootProgress = docProgress?.rootVisualElement.Q<VisualElement>("msg-progress");

        _progressDotsContainer = _rootProgress?.Q<VisualElement>("progress-dots");
        HideAllImmediate();
    }

    private void OnDestroy()
    {
        _progressAutoHideTween?.Kill();
        foreach (var t in _activeTweens.Values) t?.Kill();
    }

    public void ShowProgress(int current, int total)
    {
        if (_rootProgress == null) return;
        total = Mathf.Max(1, total);
        current = Mathf.Clamp(current, 0, total);

        if (total < 2 || current <= 0)
        {
            _progressAutoHideTween?.Kill();
            FadeOutPanel(_rootProgress);
            return;
        }

        int remaining = total - current;

        _rootProgress.Q<Label>("progress-main").SetText($"{current} de {total} plantas");
        _rootProgress.Q<Label>("progress-sub").SetText(
            remaining > 0
                ? $"Te falta{(remaining == 1 ? "" : "n")} {remaining} {(remaining == 1 ? "planta" : "plantas")} por seleccionar."
                : "¡Seleccionaste todas!");

        EnsureProgressDots(total);
        UpdateProgressDots(current);

        PlaceDoc(docProgress, Vector3.zero);
        ShowPanel(_rootProgress);

        _progressAutoHideTween?.Kill();
        _progressAutoHideTween = DOVirtual.DelayedCall(progressVisibleDuration, () =>
        {
            FadeOutPanel(_rootProgress);
        });
    }

    public void ShowCongrats(string subtitle = null)
    {
        if (subtitle != null) _rootCongrats?.Q<Label>("congrats-subtitle").SetText(subtitle);
        PlaceDoc(docCongrats, Vector3.zero);
        ShowPanel(_rootCongrats);
    }



    public void HideAll()
    {
        _progressAutoHideTween?.Kill();
        FadeOutPanel(_rootCongrats);
        FadeOutPanel(_rootProgress);
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
        SetHidden(_rootProgress);
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

    private void EnsureProgressDots(int total)
    {
        if (_progressDotsContainer == null) return;

        while (_dynamicProgressDots.Count < total)
        {
            var dot = new VisualElement();
            dot.style.width = dot.style.height = 36;
            dot.style.borderTopWidth = dot.style.borderRightWidth =
            dot.style.borderBottomWidth = dot.style.borderLeftWidth = 2;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
            dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 18;
            dot.style.alignItems = Align.Center;
            dot.style.justifyContent = Justify.Center;
            dot.style.marginLeft = dot.style.marginRight = 6;

            var lbl = new Label("?");
            lbl.style.fontSize = 16;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            dot.Add(lbl);

            _progressDotsContainer.Add(dot);
            _dynamicProgressDots.Add(dot);
        }

        for (int i = 0; i < _dynamicProgressDots.Count; i++)
            _dynamicProgressDots[i].style.display = i < total ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdateProgressDots(int completedCount)
    {
        for (int i = 0; i < _dynamicProgressDots.Count; i++)
        {
            var dot = _dynamicProgressDots[i];
            bool done = i < completedCount;
            var lbl = dot.Q<Label>();

            lbl.SetText(done ? "✓" : "?");
            lbl.style.color = done ? new Color(0.33f, 0.78f, 0.43f) : new Color(1f, 1f, 1f, 0.3f);

            var bc = done ? new Color(0.33f, 0.78f, 0.43f) : new Color(1f, 1f, 1f, 0.2f);
            dot.style.borderTopColor = dot.style.borderRightColor =
            dot.style.borderBottomColor = dot.style.borderLeftColor = bc;
            dot.style.backgroundColor = done
                ? new Color(0.33f, 0.78f, 0.43f, 0.2f)
                : new Color(1f, 1f, 1f, 0.05f);
        }
    }

    private void PlaceDoc(UIDocument doc, Vector3 offset)
    {
        if (doc == null) return;

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
}

internal static class LabelExtensions
{
    internal static void SetText(this Label label, string text)
    {
        if (label != null) label.text = text;
    }
}