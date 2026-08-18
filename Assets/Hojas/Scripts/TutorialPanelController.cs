using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

public enum TutorialGuidanceAct
{
    NONE = 0,
    GRAB_LEAF = 1,
    OBSERVE_LEAF = 2,
    REVEAL_HINT = 3,
    DIAGNOSE_FIRST_LEAF = 4,
    FREE_PRACTICE_SECOND_LEAF = 5,
    COMPLETED = 6
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
    [Tooltip("Tiempo en segundos de observacion antes de revelar la pista.")]
    [SerializeField] private float observeToDiagnoseDelay = 3f;
    [Tooltip("Tiempo en segundos que la pista permanece visible antes de abrir el menu.")]
    [SerializeField] private float hintToDiagnoseDelay = 3f;

    [Header("Avatar del tutorial")]
    [Tooltip("FBX o prefab del granjero. Se instancia cuando empieza el tutorial.")]
    [SerializeField] private GameObject avatarModel;
    [SerializeField] private RuntimeAnimatorController avatarAnimatorController;
    [SerializeField] private Vector3 avatarScale = new Vector3(0.25f, 0.25f, 0.25f);
    [SerializeField] private float avatarFrontGap = 0.12f;
    [SerializeField] private float avatarSurfaceHeightOffset = 0f;
    [SerializeField] private float avatarFacingOffsetY = 0f;
    [SerializeField] private float avatarFallbackDistance = 1.1f;
    [SerializeField] private float avatarFallbackHeight = 0f;

    [Header("Animacion de subida al completar tutorial")]
    [SerializeField] private float slideUpAmount = 0.25f;
    [SerializeField] private float slideUpDuration = 0.6f;

    [Header("Anclaje sobre superficie")]
    [SerializeField] private float surfaceHeightOffset = 0.28f;

    private Tween _fadeTween;
    private Tween _slideTween;
    private Tween _observeTween;
    private Tween _allActsSlideTween;

    // Un tween de movimiento por panel para poder cancelarlos individualmente.
    private readonly Tween[] _panelMoveTweens = new Tween[3];

    private bool _isVisible;
    private bool _levelStartRequested;
    private bool _guidedPhaseCompleted;
    private bool _tutorialFullyCompleted;
    private int _plantsSelected;
    private int _plantsRequired = 2;
    private TutorialGuidanceAct _currentAct = TutorialGuidanceAct.NONE;
    private TutorialGuidanceAct _resumeActAfterGrab = TutorialGuidanceAct.NONE;
    private Leaf _guidedLeaf;
    private GameObject _avatarObject;
    private TutorialAvatarController _avatarController;
    private Coroutine _avatarDestroyRoutine;

    // Referencia ordenada a los paneles de acto para iterar facilmente.
    private List<GameObject> _actPanels;
    private Vector3 _allActsBaseLocalPos;

    private void Awake()
    {
        _actPanels = new List<GameObject> { actPanel1, actPanel2, actPanel3 };
        if (allActsPanel != null)
            _allActsBaseLocalPos = allActsPanel.transform.localPosition;

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
        SpawnAvatar();
        DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
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
        _allActsSlideTween?.Kill();
        if (_avatarDestroyRoutine != null)
        {
            StopCoroutine(_avatarDestroyRoutine);
            _avatarDestroyRoutine = null;
        }

        // Fade out de todos los paneles activos y del allActsPanel si estuviera visible.
        FadeOutAllActPanels(() =>
        {
            HideAllActPanels();
            if (allActsPanel != null) allActsPanel.SetActive(false);
            _isVisible = false;
            _currentAct = TutorialGuidanceAct.NONE;
            DiseaseSelectionSystem.Instance?.SetPanelAvailability(true);
            DestroyAvatar();
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
        {
            _guidedLeaf = leaf;
            leaf.SetMarkersAvailability(false);
            TutorialGuidanceAct nextAct = _resumeActAfterGrab == TutorialGuidanceAct.NONE
                ? TutorialGuidanceAct.OBSERVE_LEAF
                : _resumeActAfterGrab;
            _resumeActAfterGrab = TutorialGuidanceAct.NONE;
            EnterAct(nextAct, true);
        }
        else if ((_currentAct == TutorialGuidanceAct.OBSERVE_LEAF
                  || _currentAct == TutorialGuidanceAct.REVEAL_HINT
                  || _currentAct == TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF)
                 && _guidedLeaf != leaf)
        {
            // Blindaje para hand/controller tracking que cambie de hoja sin
            // emitir primero el release de la anterior.
            _guidedLeaf?.SetMarkersAvailability(false);
            _guidedLeaf?.HideMarkersImmediate();
            _guidedLeaf = leaf;
            leaf.SetMarkersAvailability(false);
            EnterAct(_currentAct, true);
        }
    }

    /// <summary>
    /// Regresa al acto de agarrar hoja si el usuario la suelta antes de diagnosticar.
    /// El panel 1 vuelve al centro y los paneles 2 y 3 se ocultan.
    /// </summary>
    private void HandleLeafReleased(Leaf leaf)
    {
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;

        if (_currentAct == TutorialGuidanceAct.OBSERVE_LEAF
            || _currentAct == TutorialGuidanceAct.REVEAL_HINT
            || _currentAct == TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF)
        {
            _resumeActAfterGrab = _currentAct;
            if (_guidedLeaf != null)
            {
                _guidedLeaf.SetMarkersAvailability(false);
                _guidedLeaf.HideMarkersImmediate();
            }

            DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
            EnterAct(TutorialGuidanceAct.GRAB_LEAF, true);
        }
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
                _avatarController?.PlayThumbsUp();
                GuidedPhaseCompleted?.Invoke();

                int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
                EnterAct(remaining > 0
                    ? TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF
                    : TutorialGuidanceAct.COMPLETED, true);

                if (remaining == 0) CompleteTutorial();
            }
            else
            {
                // El menu ya esta abierto en este punto. No se vuelve a narrar ni
                // a reiniciar la etapa por cada intento incorrecto.
                if (_currentAct != TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF)
                    EnterAct(TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF, true);
            }
            return;
        }

        if (isCorrect)
        {
            _avatarController?.PlayThumbsUp();
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
        ApplyActPresentation(act);
        TutorialActChanged?.Invoke(act);

        if (act == TutorialGuidanceAct.OBSERVE_LEAF)
        {
            _guidedLeaf?.SetMarkersAvailability(false);
            _guidedLeaf?.HideMarkersImmediate();
            DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
            _avatarController?.PlayObserve();
            StartActTimer(TutorialGuidanceAct.REVEAL_HINT, observeToDiagnoseDelay);
        }
        else if (act == TutorialGuidanceAct.REVEAL_HINT)
        {
            if (_guidedLeaf != null)
            {
                _guidedLeaf.SetMarkersAvailability(true);
                _guidedLeaf.SetMarkersVisibility(true);
            }

            DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
            StartActTimer(TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF, hintToDiagnoseDelay);
        }
        else if (act == TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF)
        {
            _guidedLeaf?.SetMarkersAvailability(true);
            _guidedLeaf?.SetMarkersVisibility(true);
            DiseaseSelectionSystem.Instance?.SetPanelAvailability(true);
            _avatarController?.PlayDiagnosis();
        }
        else if (act == TutorialGuidanceAct.GRAB_LEAF)
        {
            DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
            _avatarController?.PlayGrab();
        }
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
            case TutorialGuidanceAct.REVEAL_HINT:
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

        if (allActsPanel == null) return;

        allActsPanel.SetActive(true);
        _allActsSlideTween?.Kill();
        allActsPanel.transform.localPosition = _allActsBaseLocalPos;
        Vector3 target = _allActsBaseLocalPos + Vector3.up * slideUpAmount;
        _allActsSlideTween = allActsPanel.transform
            .DOLocalMove(target, slideUpDuration)
            .SetEase(Ease.OutCubic);
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
            cg.DOFade(1f, panelAnimDuration).SetEase(Ease.OutCubic).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            return;
        }

        var doc = panel.GetComponentInChildren<UIDocument>();
        if (doc == null) return;

        VisualElement root = doc.rootVisualElement?.ElementAt(0);
        if (root == null) return;

        float opacity = 0f;
        DOTween.To(() => opacity, v => { opacity = v; root.style.opacity = v; }, 1f, panelAnimDuration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    /// <summary>
    /// Hace fade out del panel y ejecuta onComplete al terminar.
    /// </summary>
    private void FadeOutPanel(GameObject panel, Action onComplete = null)
    {
        var cg = panel.GetComponentInChildren<CanvasGroup>();
        if (cg != null)
        {
            cg.DOFade(0f, panelAnimDuration).SetEase(Ease.InQuad).SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(() => onComplete?.Invoke());
            return;
        }

        var doc = panel.GetComponentInChildren<UIDocument>();
        if (doc == null) { onComplete?.Invoke(); return; }

        VisualElement root = doc.rootVisualElement?.ElementAt(0);
        if (root == null) { onComplete?.Invoke(); return; }

        float opacity = root.resolvedStyle.opacity;
        DOTween.To(() => opacity, v => { opacity = v; root.style.opacity = v; }, 0f, panelAnimDuration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
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

    private void StartActTimer(TutorialGuidanceAct nextAct, float delay)
    {
        _observeTween?.Kill();
        _observeTween = DOVirtual.DelayedCall(Mathf.Max(0.5f, delay), () =>
        {
            if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) return;
            if (_currentAct == TutorialGuidanceAct.NONE || _currentAct == TutorialGuidanceAct.GRAB_LEAF) return;
            if (GrabbableLeafListener.Instance?.ActualLeafGrabbed == null) return;

            if (_currentAct == TutorialGuidanceAct.OBSERVE_LEAF && nextAct != TutorialGuidanceAct.REVEAL_HINT) return;
            if (_currentAct == TutorialGuidanceAct.REVEAL_HINT && nextAct != TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF) return;
            EnterAct(nextAct);
        });
    }

    private void ApplyActPresentation(TutorialGuidanceAct act)
    {
        if (act != TutorialGuidanceAct.REVEAL_HINT && act != TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF)
            return;

        UIDocument document = actPanel3 != null ? actPanel3.GetComponentInChildren<UIDocument>(true) : null;
        if (document == null || document.rootVisualElement == null) return;

        Label header = document.rootVisualElement.Q<Label>("tutorial-title");
        Label title = document.rootVisualElement.Q<Label>("tutorial-act-title");
        Label detail = document.rootVisualElement.Q<Label>("tutorial-act-detail");
        if (title == null || detail == null) return;

        if (act == TutorialGuidanceAct.REVEAL_HINT)
        {
            if (header != null) header.text = "Mira la pista";
            title.text = "Mira la pista";
            detail.text = "Te mostrare una pista con la enfermedad y la severidad de esta hoja.";
        }
        else
        {
            if (header != null) header.text = "Registra tu diagnostico";
            title.text = "Diagnostica la hoja";
            detail.text = "Usa el menu con tu otra mano, sigue las pistas y confirma tu diagnostico.";
        }
    }

    private void CompleteTutorial()
    {
        if (_tutorialFullyCompleted) return;
        _tutorialFullyCompleted = true;
        DiseaseSelectionSystem.Instance?.SetPanelAvailability(true);
        _avatarDestroyRoutine = StartCoroutine(DestroyAvatarAfterCompletion());
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
        _resumeActAfterGrab = TutorialGuidanceAct.NONE;
        _guidedLeaf = null;
    }

    private void KillTweens()
    {
        _fadeTween?.Kill();
        _slideTween?.Kill();
        _observeTween?.Kill();
        _allActsSlideTween?.Kill();
        foreach (var t in _panelMoveTweens) t?.Kill();
    }

    private void SpawnAvatar()
    {
        if (_avatarDestroyRoutine != null)
        {
            StopCoroutine(_avatarDestroyRoutine);
            _avatarDestroyRoutine = null;
        }
        DestroyAvatar();
        if (avatarModel == null)
        {
            Debug.LogWarning("[Tutorial] No hay avatarModel asignado; el tutorial continuara sin avatar.");
            return;
        }

        try
        {
            _avatarObject = Instantiate(avatarModel);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Tutorial] No se pudo instanciar el avatar ({e.Message}); el tutorial continuara sin avatar.");
            _avatarObject = null;
            return;
        }

        _avatarObject.name = "TutorialAvatar";

        // El nodo referenciado puede ser cualquier sub-nodo del modelo (p.ej. el
        // mesh humanLowPoly). Se normaliza a la raiz instanciada para usar su
        // pivote (pies) y encontrar el Animator humanoide que importo el FBX.
        Transform avatarRoot = _avatarObject.transform;
        while (avatarRoot.parent != null)
            avatarRoot = avatarRoot.parent;
        if (avatarRoot != _avatarObject.transform)
            _avatarObject = avatarRoot.gameObject;

        _avatarObject.transform.localScale = avatarScale;
        _avatarController = _avatarObject.GetComponent<TutorialAvatarController>();
        if (_avatarController == null)
            _avatarController = _avatarObject.AddComponent<TutorialAvatarController>();

        _avatarController.Initialize(avatarAnimatorController);
        Transform player = Camera.main != null ? Camera.main.transform : null;

        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            _avatarController.PlaceRelativeToSurface(
                SceneInteractionManager.Instance.SelectedPlanePosition,
                SceneInteractionManager.Instance.SelectedPlaneRotation,
                SceneInteractionManager.Instance.SelectedPlaneScale,
                player,
                avatarFrontGap,
                avatarSurfaceHeightOffset,
                avatarFacingOffsetY);
        }
        else
        {
            _avatarController.PlaceInFrontOfPlayer(player, avatarFallbackDistance, avatarFallbackHeight);
        }

        _avatarController.PlayGrab();
    }

    private void DestroyAvatar()
    {
        if (_avatarObject != null)
            Destroy(_avatarObject);

        _avatarObject = null;
        _avatarController = null;
    }

    private IEnumerator DestroyAvatarAfterCompletion()
    {
        yield return new WaitForSeconds(1.25f);
        DestroyAvatar();
        _avatarDestroyRoutine = null;
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
