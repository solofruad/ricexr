using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

public enum TutorialGuidanceAct
{
    NONE = 0,
    GRAB_LEAF = 1,
    OBSERVE_LEAF = 2,
    DIAGNOSE_FIRST_LEAF = 3,
    FREE_PRACTICE_SECOND_LEAF = 4,
    COMPLETED = 5
}

public class TutorialPanelController : MonoBehaviour
{
    public event Action TutorialStarted;
    public event Action GuidedPhaseCompleted;
    public event Action<TutorialGuidanceAct> TutorialActChanged;
    public event Action TutorialCompleted;

    [Header("Referencias")]
    [Tooltip("GameObject que contiene los 3 pasos juntos, visible en practica libre y completado.")]
    [SerializeField] private GameObject allActsPanel;

    [Header("Paneles de acto guiado (GameObjects)")]
    [Tooltip("Panel visual del acto 1: Tomar hoja.")]
    [SerializeField] private GameObject actPanel1;
    [Tooltip("Panel visual del acto 2: Observar síntomas.")]
    [SerializeField] private GameObject actPanel2;
    [Tooltip("Panel visual del acto 3: Diagnosticar.")]
    [SerializeField] private GameObject actPanel3;

    [Header("Resaltado de acto activo")]
    [SerializeField] private Color activeBorderColor = new Color(0.27f, 0.85f, 0.55f, 1f);
    [SerializeField] private Color inactiveBorderColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private float borderWidth = 3f;

    [Header("Animacion de paneles")]
    [Tooltip("Separacion local entre cada panel cuando hay mas de uno visible.")]
    [SerializeField] private Vector3 panelOffset = new Vector3(0.32f, 0f, 0f);
    [Tooltip("Duracion en segundos de la animacion de entrada/salida/reposicionamiento de paneles.")]
    [SerializeField] private float panelAnimDuration = 0.35f;

    [Header("Actos guiados")]
    [Tooltip("Tiempo en segundos para pasar de observar a diagnosticar mientras la hoja sigue agarrada.")]
    [SerializeField] private float observeToDiagnoseDelay = 6f;

    [Header("Animacion de subida al completar tutorial")]
    [SerializeField] private float slideUpAmount = 0.25f;
    [SerializeField] private float slideUpDuration = 0.6f;

    [Header("Anclaje sobre superficie")]
    [SerializeField] private float surfaceHeightOffset = 0.28f;

    private Tween _fadeTween;
    private Tween _slideTween;
    private Tween _observeTween;

    // Un tween de movimiento por panel para poder cancelarlos individualmente.
    private readonly Tween[] _panelMoveTweens = new Tween[3];

    private bool _isVisible;
    private bool _levelStartRequested;
    private bool _guidedPhaseCompleted;
    private bool _tutorialFullyCompleted;
    private int _plantsSelected;
    private int _plantsRequired = 2;
    private TutorialGuidanceAct _currentAct = TutorialGuidanceAct.NONE;

    // Referencia ordenada a los paneles de acto para iterar facilmente.
    private List<GameObject> _actPanels;

    private void Awake()
    {
        _actPanels = new List<GameObject> { actPanel1, actPanel2, actPanel3 };

        // Ocultamos todo al inicio; ShowAndStart() se encarga de mostrar lo que corresponde.
        HideImmediate();
    }

    private void OnEnable()
    {
        GrabbableLeafListener.SelectionUpdated += HandleLeafSelected;
        GrabbableLeafListener.SelectionCleared += HandleLeafReleased;
        GameEventBus.OnPlantSelected += HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected += HandleAllPlantsSelected;
    }

    private void OnDisable()
    {
        GrabbableLeafListener.SelectionUpdated -= HandleLeafSelected;
        GrabbableLeafListener.SelectionCleared -= HandleLeafReleased;
        GameEventBus.OnPlantSelected -= HandlePlantSelected;
        GameEventBus.OnAllPlantsSelected -= HandleAllPlantsSelected;
        KillTweens();
    }

    private void OnDestroy() => KillTweens();

    // ─── API pública ──────────────────────────────────────────────────────────

    /// <summary>
    /// Muestra el panel y comienza el tutorial desde el primer acto.
    /// </summary>
    public void ShowAndStart()
    {
        KillTweens();
        ResetSessionState();
        RepositionPanel();
        _isVisible = true;

        if (allActsPanel != null) allActsPanel.SetActive(false);
        HideAllActPanels();

        TutorialStarted?.Invoke();
        GameEventBus.PublishTutorialStarted();
        StartTutorialGameplayIfNeeded();
        EnterAct(TutorialGuidanceAct.GRAB_LEAF, true);
    }

    /// <summary>
    /// Oculta todo con fade y resetea el acto actual.
    /// </summary>
    public void Hide()
    {
        if (!_isVisible) return;

        _observeTween?.Kill();
        _slideTween?.Kill();

        // Fade out de todos los paneles activos y del allActsPanel si estuviera visible.
        FadeOutAllActPanels(() =>
        {
            HideAllActPanels();
            if (allActsPanel != null) allActsPanel.SetActive(false);
            _isVisible = false;
            _currentAct = TutorialGuidanceAct.NONE;
        });
    }

    // ─── Handlers de eventos ──────────────────────────────────────────────────

    /// <summary>
    /// Avanza el acto cuando el usuario agarra una hoja durante la fase guiada.
    /// </summary>
    private void HandleLeafSelected(Leaf leaf, GrabbableLeafListener.SelectionHand hand, Transform anchor)
    {
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted || leaf == null) return;

        if (_currentAct == TutorialGuidanceAct.GRAB_LEAF)
            EnterAct(TutorialGuidanceAct.OBSERVE_LEAF);
        else if (_currentAct == TutorialGuidanceAct.OBSERVE_LEAF)
            StartObserveTimer();
    }

    /// <summary>
    /// Regresa al acto de agarrar hoja si el usuario la suelta antes de diagnosticar.
    /// El panel 1 vuelve al centro y los paneles 2 y 3 se ocultan.
    /// </summary>
    private void HandleLeafReleased(Leaf leaf)
    {
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;

        if (_currentAct == TutorialGuidanceAct.OBSERVE_LEAF || _currentAct == TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF)
            EnterAct(TutorialGuidanceAct.GRAB_LEAF, true);
    }

    /// <summary>
    /// Avanza o completa el tutorial segun el resultado de la seleccion de planta.
    /// Durante la fase guiada, cualquier seleccion correcta completa dicha fase y
    /// lanza practica libre o completado segun las plantas restantes. Si la seleccion
    /// es incorrecta, se vuelve al acto de diagnosticar. Una vez en practica libre,
    /// solo las selecciones correctas hacen avanzar el tutorial.
    /// </summary>
    private void HandlePlantSelected(bool isCorrect, int plantsSelected, int plantsRequired)
    {
        if (!_isVisible || _tutorialFullyCompleted) return;

        _plantsSelected = Mathf.Max(0, plantsSelected);
        _plantsRequired = Mathf.Max(1, plantsRequired);

        if (!_guidedPhaseCompleted)
        {
            if (isCorrect)
            {
                _guidedPhaseCompleted = true;
                _observeTween?.Kill();
                GuidedPhaseCompleted?.Invoke();

                int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
                EnterAct(remaining > 0
                    ? TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF
                    : TutorialGuidanceAct.COMPLETED, true);

                if (remaining == 0) CompleteTutorial();
            }
            else
            {
                EnterAct(TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF, true);
            }
            return;
        }

        if (isCorrect)
        {
            int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
            EnterAct(remaining > 0
                ? TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF
                : TutorialGuidanceAct.COMPLETED, true);

            if (remaining == 0) CompleteTutorial();
        }
    }

    /// <summary>
    /// Completa el tutorial cuando el usuario ha seleccionado correctamente todas las plantas requeridas.
    /// </summary>
    private void HandleAllPlantsSelected()
    {
        if (!_isVisible || _tutorialFullyCompleted) return;
        EnterAct(TutorialGuidanceAct.COMPLETED, true);
        CompleteTutorial();
    }

    // ─── Maquina de actos ─────────────────────────────────────────────────────

    /// <summary>
    /// Transiciona al acto indicado, re-renderiza la UI y dispara el timer de observacion si corresponde.
    /// El parametro force permite re-entrar al mismo acto, util para reiniciar GRAB_LEAF cuando
    /// el usuario suelta y vuelve a tomar la hoja.
    /// </summary>
    private void EnterAct(TutorialGuidanceAct act, bool force = false)
    {
        if (!force && _currentAct == act) return;

        _currentAct = act;
        _observeTween?.Kill();
        RenderAct(act);
        TutorialActChanged?.Invoke(act);

        if (act == TutorialGuidanceAct.OBSERVE_LEAF)
            StartObserveTimer();
    }

    /// <summary>
    /// Determina que mostrar segun el acto: paneles individuales animados para la fase guiada,
    /// o el allActsPanel completo para practica libre y completado.
    /// </summary>
    private void RenderAct(TutorialGuidanceAct act)
    {
        switch (act)
        {
            case TutorialGuidanceAct.GRAB_LEAF:
                ShowGuidedActPanels(1);
                break;
            case TutorialGuidanceAct.OBSERVE_LEAF:
                ShowGuidedActPanels(2);
                break;
            case TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF:
                ShowGuidedActPanels(3);
                break;
            case TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF:
            case TutorialGuidanceAct.COMPLETED:
                ShowAllActsPanel();
                break;
        }
    }

    // ─── Logica de paneles de GameObjects ─────────────────────────────────────

    /// <summary>
    /// Muestra los paneles de acto desde el primero hasta activeIndex (inclusive), animando
    /// sus posiciones locales para que queden centrados en torno al panel del medio.
    /// El layout resultante es simetrico: con 1 panel queda en el centro, con 2 hay uno a cada
    /// lado del eje, con 3 el central en origen y los extremos separados por panelOffset.
    /// Solo el panel activo (el recien aparecido) recibe borde verde; los anteriores quedan atenuados.
    /// </summary>
    private void ShowGuidedActPanels(int activeIndex)
    {
        if (allActsPanel != null) allActsPanel.SetActive(false);

        int count = activeIndex; // cuantos paneles deben estar visibles

        // Calculamos las posiciones locales centradas.
        // Con N paneles, el offset del panel i es: (i - (N-1)/2) * panelOffset
        // Esto centra automaticamente el grupo sin importar cuantos haya.
        for (int i = 0; i < _actPanels.Count; i++)
        {
            GameObject panel = _actPanels[i];
            if (panel == null) continue;

            bool shouldBeVisible = i < count;

            if (shouldBeVisible)
            {
                float t = count > 1 ? i - (count - 1) / 2f : 0f;
                Vector3 targetLocalPos = panelOffset * t;
                bool isActive = i == activeIndex - 1; // solo el ultimo panel recien aparecido

                if (!panel.activeSelf)
                {
                    // Panel nuevo: aparece desde el centro con fade in.
                    panel.SetActive(true);
                    panel.transform.localPosition = Vector3.zero;
                    SetPanelAlpha(panel, 0f);
                    AnimatePanelMove(i, targetLocalPos);
                    FadeInPanel(panel);
                }
                else
                {
                    // Panel ya visible: solo se reposiciona suavemente.
                    AnimatePanelMove(i, targetLocalPos);
                }

                ApplyBorderHighlight(panel, isActive);
            }
            else if (panel.activeSelf)
            {
                // Panel que debe desaparecer: fade out y luego desactivar.
                int capturedIndex = i;
                FadeOutPanel(panel, () => _actPanels[capturedIndex]?.SetActive(false));
            }
        }
    }

    /// <summary>
    /// Oculta los paneles individuales con fade y muestra el allActsPanel,
    /// que agrupa los 3 pasos en una vista compacta para practica libre y completado.
    /// </summary>
    private void ShowAllActsPanel()
    {
        FadeOutAllActPanels(() => HideAllActPanels());

        if (allActsPanel != null)
            allActsPanel.SetActive(true);
    }

    /// <summary>
    /// Desactiva todos los GameObjects de acto guiado sin animacion.
    /// </summary>
    private void HideAllActPanels()
    {
        foreach (var panel in _actPanels)
            panel?.SetActive(false);
    }

    /// <summary>
    /// Aplica borde verde brillante al panel activo o borde tenue a los anteriores visibles,
    /// buscando el primer UIDocument dentro del GameObject.
    /// </summary>
    private void ApplyBorderHighlight(GameObject panel, bool isActive)
    {
        var doc = panel.GetComponentInChildren<UIDocument>();
        if (doc == null) return;

        VisualElement root = doc.rootVisualElement?.ElementAt(0);
        if (root == null) return;

        Color color = isActive ? activeBorderColor : inactiveBorderColor;
        float width = isActive ? borderWidth : 1f;

        root.style.borderTopColor = color;
        root.style.borderBottomColor = color;
        root.style.borderLeftColor = color;
        root.style.borderRightColor = color;
        root.style.borderTopWidth = width;
        root.style.borderBottomWidth = width;
        root.style.borderLeftWidth = width;
        root.style.borderRightWidth = width;
    }

    // ─── Animaciones de paneles ───────────────────────────────────────────────

    /// <summary>
    /// Anima la posicion local del panel hacia targetLocalPos, cancelando cualquier
    /// tween de movimiento previo para ese indice.
    /// </summary>
    private void AnimatePanelMove(int index, Vector3 targetLocalPos)
    {
        _panelMoveTweens[index]?.Kill();
        _panelMoveTweens[index] = _actPanels[index].transform
            .DOLocalMove(targetLocalPos, panelAnimDuration)
            .SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// Hace fade in del CanvasGroup o UIDocument del panel. Si no hay CanvasGroup,
    /// usa el UIDocument para animar la opacidad del root visual.
    /// </summary>
    private void FadeInPanel(GameObject panel)
    {
        var cg = panel.GetComponentInChildren<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.DOFade(1f, panelAnimDuration).SetEase(Ease.OutCubic);
            return;
        }

        var doc = panel.GetComponentInChildren<UIDocument>();
        if (doc == null) return;

        VisualElement root = doc.rootVisualElement?.ElementAt(0);
        if (root == null) return;

        float opacity = 0f;
        DOTween.To(() => opacity, v => { opacity = v; root.style.opacity = v; }, 1f, panelAnimDuration)
            .SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// Hace fade out del panel y ejecuta onComplete al terminar.
    /// </summary>
    private void FadeOutPanel(GameObject panel, Action onComplete = null)
    {
        var cg = panel.GetComponentInChildren<CanvasGroup>();
        if (cg != null)
        {
            cg.DOFade(0f, panelAnimDuration).SetEase(Ease.InQuad).OnComplete(() => onComplete?.Invoke());
            return;
        }

        var doc = panel.GetComponentInChildren<UIDocument>();
        if (doc == null) { onComplete?.Invoke(); return; }

        VisualElement root = doc.rootVisualElement?.ElementAt(0);
        if (root == null) { onComplete?.Invoke(); return; }

        float opacity = root.resolvedStyle.opacity;
        DOTween.To(() => opacity, v => { opacity = v; root.style.opacity = v; }, 0f, panelAnimDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>
    /// Hace fade out de todos los paneles de acto guiado que esten activos,
    /// ejecutando onComplete una sola vez cuando todos terminen.
    /// </summary>
    private void FadeOutAllActPanels(Action onComplete = null)
    {
        int pending = 0;
        foreach (var panel in _actPanels)
            if (panel != null && panel.activeSelf) pending++;

        if (pending == 0) { onComplete?.Invoke(); return; }

        foreach (var panel in _actPanels)
        {
            if (panel == null || !panel.activeSelf) continue;
            FadeOutPanel(panel, () =>
            {
                pending--;
                if (pending == 0) onComplete?.Invoke();
            });
        }
    }

    /// <summary>
    /// Fuerza la opacidad de un panel a un valor inmediato sin animacion,
    /// util para preparar el estado inicial antes de un fade in.
    /// </summary>
    private void SetPanelAlpha(GameObject panel, float alpha)
    {
        var cg = panel.GetComponentInChildren<CanvasGroup>();
        if (cg != null) { cg.alpha = alpha; return; }

        var doc = panel.GetComponentInChildren<UIDocument>();
        if (doc == null) return;
        VisualElement root = doc.rootVisualElement?.ElementAt(0);
        if (root != null) root.style.opacity = alpha;
    }

    // ─── Utilidades ───────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia el timer que avanza automaticamente de OBSERVE_LEAF a DIAGNOSE_FIRST_LEAF
    /// si el usuario no diagnostica dentro del tiempo configurado en observeToDiagnoseDelay.
    /// </summary>
    private void StartObserveTimer()
    {
        _observeTween?.Kill();
        _observeTween = DOVirtual.DelayedCall(Mathf.Max(0.5f, observeToDiagnoseDelay), () =>
        {
            if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;
            if (_currentAct != TutorialGuidanceAct.OBSERVE_LEAF) return;
            if (GrabbableLeafListener.Instance?.ActualLeafGrabbed == null) return;

            EnterAct(TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF);
        });
    }

    private void CompleteTutorial()
    {
        if (_tutorialFullyCompleted) return;
        _tutorialFullyCompleted = true;
        TutorialCompleted?.Invoke();
        GameEventBus.PublishTutorialCompleted();
        SlideUp();
    }

    private void SlideUp()
    {
        _slideTween?.Kill();
        Vector3 target = transform.position + Vector3.up * slideUpAmount;
        _slideTween = transform.DOMove(target, slideUpDuration).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// Oculta todo de forma inmediata sin animaciones. Util al inicializar o reiniciar el tutorial.
    /// </summary>
    private void HideImmediate()
    {
        HideAllActPanels();
        if (allActsPanel != null) allActsPanel.SetActive(false);
        _isVisible = false;
        _currentAct = TutorialGuidanceAct.NONE;
    }

    /// <summary>
    /// Resetea el estado interno para permitir reiniciar el tutorial sin recargar la escena.
    /// </summary>
    private void ResetSessionState()
    {
        _levelStartRequested = false;
        _guidedPhaseCompleted = false;
        _tutorialFullyCompleted = false;
        _plantsSelected = 0;
        _plantsRequired = 2;
        _currentAct = TutorialGuidanceAct.NONE;
    }

    private void KillTweens()
    {
        _fadeTween?.Kill();
        _slideTween?.Kill();
        _observeTween?.Kill();
        foreach (var t in _panelMoveTweens) t?.Kill();
    }

    /// <summary>
    /// Notifica que el gameplay del tutorial esta listo. GameFlowController escucha esta señal
    /// a traves del bus para iniciar el primer nivel.
    /// </summary>
    private void StartTutorialGameplayIfNeeded()
    {
        if (_levelStartRequested) return;
        _levelStartRequested = true;
    }

    /// <summary>
    /// Ancla el panel sobre la superficie seleccionada, o frente a la camara si no hay ninguna.
    /// </summary>
    private void RepositionPanel()
    {
        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            transform.position = SceneInteractionManager.Instance.SelectedPlanePosition
                                 + SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up * surfaceHeightOffset;
            return;
        }

        if (Camera.main == null) return;
        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
        transform.position = cam.position + fwd.normalized;
    }
}