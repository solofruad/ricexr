using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

// ─── Tipos de miniguía disponibles ───────────────────────────────────────────

/// <summary>Panel de miniguía a mostrar antes del intro principal.</summary>
public enum MiniGuideType
{
    None,
    Tutorial,
    NextLevel,
    FinalLevel
}

/// <summary>
/// Regla que asocia un tipo de miniguía con los niveles en que debe aparecer.
/// Se configura desde el Inspector y permite perfiles completamente distintos
/// sin cambiar código (e.g. juegos sin tutorial, sin advertencia de nivel final, etc.).
/// </summary>
[Serializable]
public class MiniGuideRule
{
    [Tooltip("Tipo de panel de miniguía que se mostrará.")]
    public MiniGuideType type = MiniGuideType.None;

    [Tooltip("¿Aplica solo al último nivel (total-1)? Si está activo, ignora fromLevel/toLevel.")]
    public bool onlyFinalLevel = false;

    [Tooltip("Índice de nivel mínimo (inclusive, base-0) en que aplica la regla.")]
    public int fromLevel = 0;

    [Tooltip("Índice de nivel máximo (inclusive, base-0) en que aplica. -1 = sin límite.")]
    public int toLevel = -1;
}

// ─── Controlador principal ───────────────────────────────────────────────────

/// <summary>
/// Muestra la secuencia de introducción al inicio de cada nivel.
///
/// Orden:
///   1) [Opcional] Miniguía breve (tutorial / next-level / final-level), auto-cierre.
///   2) Panel principal de intro con colores de nivel y botón de continuar (o auto-avance).
///
/// Se configura completamente desde el Inspector — qué miniguía mostrar en qué nivel
/// se define mediante el array <see cref="miniGuideRules"/>, sin hardcodear índices.
/// </summary>
[DisallowMultipleComponent]
public class LevelIntroController : MonoBehaviour
{
    // ── Eventos públicos ──────────────────────────────────────────────────────

    public event Action IntroPanelShown;
    public event Action IntroContinueRequested;

    // ── Inspector — UI Documents ──────────────────────────────────────────────

    [Header("UI Documents — Panel principal")]
    [SerializeField] private UIDocument introDocument;

    [Header("UI Documents — Boton continuar")]
    [SerializeField] private UIDocument continueButtonDocument;

    [Header("UI Documents — Miniguías (una por tipo)")]
    [SerializeField] private UIDocument miniGuideTutorialDocument;
    [SerializeField] private UIDocument miniGuideNextLevelDocument;
    [SerializeField] private UIDocument miniGuideFinalLevelDocument;

    [Tooltip("Si está activo, busca los UIDocuments automáticamente por nombre de elemento raíz.")]
    [SerializeField] private bool autoFindDocuments = true;

    // ── Inspector — Nombres de elementos UI ──────────────────────────────────

    [Header("Nombres de elementos raíz en cada UIDocument")]
    [SerializeField] private string introRootElementName      = "panel-intro";
    [SerializeField] private string continueButtonElementName = "intro-continue-button";
    [SerializeField] private string miniGuideTutorialRootName  = "msg-tutorial-start";
    [SerializeField] private string miniGuideNextLevelRootName = "msg-next-level";
    [SerializeField] private string miniGuideFinalLevelRootName= "msg-final-level";

    // ── Inspector — Indicadores de nivel ─────────────────────────────────────

    [Header("Indicadores de nivel en el panel principal")]
    [SerializeField] private string[] levelElementNames = new string[]
    {
        "msgLevel1", "msgLevel2", "msgLevel3", "msgLevel4"
    };

    [SerializeField] private Color activeLevelBackgroundColor   = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color activeLevelBorderColor       = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color inactiveLevelBackgroundColor = new Color(0.8f, 0.8f, 0.8f, 0f);
    [SerializeField] private Color inactiveLevelBorderColor     = new Color(0.8f, 0.8f, 0.8f, 0f);

    // ── Inspector — Reglas de miniguías ──────────────────────────────────────

    [Header("Reglas de miniguías por nivel")]
    [Tooltip(
        "Define qué tipo de miniguía se muestra en qué niveles.\n" +
        "Las reglas se evalúan en orden; se aplica la primera que coincide.\n" +
        "Las reglas con 'Only Final Level' tienen prioridad automática.\n\n" +
        "Ejemplo de configuración estándar:\n" +
        "  [0] Tutorial  | From:0  To:0   | OnlyFinal:false\n" +
        "  [1] FinalLevel| From:0  To:-1  | OnlyFinal:true\n" +
        "  [2] NextLevel | From:1  To:-1  | OnlyFinal:false")]
    [SerializeField] private MiniGuideRule[] miniGuideRules = new MiniGuideRule[]
    {
        new MiniGuideRule { type = MiniGuideType.Tutorial,    onlyFinalLevel = false, fromLevel = 0, toLevel = 0  },
        new MiniGuideRule { type = MiniGuideType.FinalLevel,  onlyFinalLevel = true,  fromLevel = 0, toLevel = -1 },
        new MiniGuideRule { type = MiniGuideType.NextLevel,   onlyFinalLevel = false, fromLevel = 1, toLevel = -1 },
    };

    // ── Inspector — Tiempos ───────────────────────────────────────────────────

    [Header("Tiempos")]
    [SerializeField] private float introAutoAdvanceDelay   = 99f;
    [SerializeField] private float miniGuideDuration       = 2.6f;
    [SerializeField] private float fadeInDuration          = 0.35f;
    [SerializeField] private float fadeOutDuration         = 0.25f;
    [SerializeField] private float levelColorTweenDuration = 0.25f;

    // ── Inspector — Posicionamiento ───────────────────────────────────────────

    [Header("World Space")]
    [SerializeField] private float surfaceHeightOffset = 0.28f;

    // ── VisualElements cacheados ──────────────────────────────────────────────

    private VisualElement _introRoot;
    private VisualElement _continueRoot;

    private VisualElement _miniGuideTutorialRoot;
    private VisualElement _miniGuideNextLevelRoot;
    private VisualElement _miniGuideFinalLevelRoot;

    // ── Tweens ────────────────────────────────────────────────────────────────

    private Tween _introFadeTween;
    private Tween _introAutoAdvanceTween;
    private Tween _continueButtonFadeTween;
    private Tween _miniGuideFadeTween;
    private Tween _miniGuideHoldTween;

    private Coroutine _introFadeDelayRoutine;
    private Coroutine _continueButtonFadeDelayRoutine;
    private Coroutine _miniGuideFadeDelayRoutine;

    // ── Estado interno de nivel ───────────────────────────────────────────────

    private readonly List<VisualElement> _levelElements        = new List<VisualElement>();
    private readonly List<Tween>         _levelColorTweens     = new List<Tween>();
    private readonly List<Coroutine>     _levelColorDelayRoutines = new List<Coroutine>();
    private UIDocument _cachedLevelsDocument;
    private int        _currentLevelIndex = -1;

    // ── Estado de secuencia ───────────────────────────────────────────────────

    private bool   _sequenceRunning;
    private bool   _introAdvanced;
    private bool   _completionTriggered;
    private Action _onSequenceCompleted;

    // ── Contexto de nivel actual ──────────────────────────────────────────────

    private int _levelIndex;
    private int _totalLevels;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        ResolveDocuments();
        CacheElements();
        ResetState();
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API pública
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Muestra la secuencia de intro para el nivel indicado:
    ///   1) Transición de colores en los indicadores de nivel.
    ///   2) Miniguía apropiada (según <see cref="miniGuideRules"/>), si aplica.
    ///   3) Panel principal con botón de continuar o auto-avance.
    ///   Al completar, invoca <paramref name="onCompleted"/>.
    /// </summary>
    public void ShowIntro(int levelIndex, int totalLevels, Action onCompleted)
    {
        ResolveDocuments();
        CacheElements();

        if (_introRoot == null)
        {
            Debug.LogWarning("[LevelIntro] No hay panel de intro configurado. Se omite secuencia.");
            ResetState();
            onCompleted?.Invoke();
            return;
        }

        if (_sequenceRunning) return;

        ResetState();

        _levelIndex          = levelIndex;
        _totalLevels         = totalLevels;
        _sequenceRunning     = true;
        _onSequenceCompleted = onCompleted;

        RepositionPanels();

        MiniGuideType miniGuide = ResolveMiniGuide(levelIndex, totalLevels);

        if (miniGuide != MiniGuideType.None)
            ShowMiniGuide(miniGuide, ShowIntroPanelAfterMiniGuide);
        else
            ShowIntroPanel();
    }

    /// <summary>Resetea todos los tweens y oculta paneles. Seguro de llamar en cualquier momento.</summary>
    public void ResetState()
    {
        KillAllTweens();

        _sequenceRunning     = false;
        _introAdvanced       = false;
        _completionTriggered = false;
        _onSequenceCompleted = null;
        _currentLevelIndex   = -1;

        SetHidden(_introRoot, introDocument);
        SetHidden(_continueRoot, continueButtonDocument);
        SetHidden(_miniGuideTutorialRoot, miniGuideTutorialDocument);
        SetHidden(_miniGuideNextLevelRoot, miniGuideNextLevelDocument);
        SetHidden(_miniGuideFinalLevelRoot, miniGuideFinalLevelDocument);
    }

    /// <summary>
    /// Responde al botón de continuar del panel principal.
    /// Puede conectarse desde el inspector del UIDocument (o desde código).
    /// </summary>
    public void HandleContinueClicked()
    {
        IntroContinueRequested?.Invoke();
        AdvanceFromIntro();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Resolución de documentos y cacheado de elementos
    // ─────────────────────────────────────────────────────────────────────────

    private void ResolveDocuments()
    {
        if (!autoFindDocuments) return;

        bool needIntro         = introDocument == null;
        bool needContinue      = continueButtonDocument == null;
        bool needTutorial      = miniGuideTutorialDocument == null;
        bool needNextLevel     = miniGuideNextLevelDocument == null;
        bool needFinalLevel    = miniGuideFinalLevelDocument == null;

        if (!needIntro && !needContinue && !needTutorial && !needNextLevel && !needFinalLevel) return;

        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
        foreach (UIDocument doc in docs)
        {
            if (doc == null || doc.rootVisualElement == null) continue;

            if (needIntro      && doc.rootVisualElement.Q<VisualElement>(introRootElementName)       != null) { introDocument               = doc; needIntro      = false; }
            if (needContinue   && doc.rootVisualElement.Q<VisualElement>(continueButtonElementName)  != null) { continueButtonDocument      = doc; needContinue   = false; }
            if (needTutorial   && doc.rootVisualElement.Q<VisualElement>(miniGuideTutorialRootName)  != null) { miniGuideTutorialDocument   = doc; needTutorial   = false; }
            if (needNextLevel  && doc.rootVisualElement.Q<VisualElement>(miniGuideNextLevelRootName) != null) { miniGuideNextLevelDocument  = doc; needNextLevel  = false; }
            if (needFinalLevel && doc.rootVisualElement.Q<VisualElement>(miniGuideFinalLevelRootName)!= null) { miniGuideFinalLevelDocument = doc; needFinalLevel = false; }

            if (!needIntro && !needContinue && !needTutorial && !needNextLevel && !needFinalLevel) break;
        }
    }

    private void CacheElements()
    {
        ActivateDocumentForCache(introDocument);
        ActivateDocumentForCache(continueButtonDocument);
        ActivateDocumentForCache(miniGuideTutorialDocument);
        ActivateDocumentForCache(miniGuideNextLevelDocument);
        ActivateDocumentForCache(miniGuideFinalLevelDocument);

        _introRoot     = introDocument?.rootVisualElement?.Q<VisualElement>(introRootElementName);
        _continueRoot  = continueButtonDocument?.rootVisualElement?.Q<VisualElement>(continueButtonElementName);

        _miniGuideTutorialRoot  = miniGuideTutorialDocument ?.rootVisualElement?.Q<VisualElement>(miniGuideTutorialRootName);
        _miniGuideNextLevelRoot = miniGuideNextLevelDocument?.rootVisualElement?.Q<VisualElement>(miniGuideNextLevelRootName);
        _miniGuideFinalLevelRoot= miniGuideFinalLevelDocument?.rootVisualElement?.Q<VisualElement>(miniGuideFinalLevelRootName);

        if (introDocument != null && _introRoot == null)
            Debug.LogWarning($"[LevelIntro] No se encontró el root '{introRootElementName}' en introDocument.");
        if (continueButtonDocument != null && _continueRoot == null)
            Debug.LogWarning($"[LevelIntro] No se encontró el root '{continueButtonElementName}' en continueButtonDocument.");
    }

    private static void ActivateDocumentForCache(UIDocument document)
    {
        if (document == null) return;
        if (!document.gameObject.activeSelf) document.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Indicadores de nivel (color tween)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Actualiza los colores de los indicadores de nivel en el panel principal.
    /// El nivel activo recibe los colores activos; los demás, los inactivos.
    /// </summary>
    public void SetActiveLevel(int levelIndex, float colorDuration = -1f)
    {
        if (colorDuration < 0f) colorDuration = levelColorTweenDuration;


        Debug.Log($"[LevelIntro] SetActiveLevel({levelIndex}, duration={colorDuration})--------------------------------");
        Debug.Log($"Nivel actual: {_currentLevelIndex}, Nivel solicitado: {levelIndex}");

        CacheLevelElements();
        if (_levelElements.Count == 0) return;

        if (levelIndex < 0 || levelIndex >= _levelElements.Count)
        {
            Debug.LogWarning($"[LevelIntro] Nivel fuera de rango: {levelIndex}.");
            return;
        }

        if (_currentLevelIndex == levelIndex && _currentLevelIndex >= 0) return;

        KillLevelColorTweens();

        if (_currentLevelIndex < 0)
        {
            // Primera vez — poner todos en inactivo excepto el nuevo activo.
            for (int i = 0; i < _levelElements.Count; i++)
            {
                if (i == levelIndex) continue;
                TweenLevelColors(_levelElements[i], inactiveLevelBackgroundColor, inactiveLevelBorderColor, colorDuration);
            }
        }
        else if (_currentLevelIndex < _levelElements.Count)
        {
            TweenLevelColors(_levelElements[_currentLevelIndex], inactiveLevelBackgroundColor, inactiveLevelBorderColor, colorDuration);
        }

        TweenLevelColors(_levelElements[levelIndex], activeLevelBackgroundColor, activeLevelBorderColor, colorDuration);
        _currentLevelIndex = levelIndex;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Secuencia — miniguía
    // ─────────────────────────────────────────────────────────────────────────

    private MiniGuideType ResolveMiniGuide(int levelIndex, int totalLevels)
    {
        if (miniGuideRules == null || miniGuideRules.Length == 0) return MiniGuideType.None;

        int finalIndex = Mathf.Max(0, totalLevels - 1);

        // Primero evaluar reglas de solo-último-nivel (prioridad)
        foreach (MiniGuideRule rule in miniGuideRules)
        {
            if (rule == null || rule.type == MiniGuideType.None) continue;
            if (!rule.onlyFinalLevel) continue;
            if (levelIndex == finalIndex) return rule.type;
        }

        // Luego evaluar el resto por rango
        foreach (MiniGuideRule rule in miniGuideRules)
        {
            if (rule == null || rule.type == MiniGuideType.None) continue;
            if (rule.onlyFinalLevel) continue;

            bool inRange = levelIndex >= rule.fromLevel &&
                           (rule.toLevel < 0 || levelIndex <= rule.toLevel);
            if (inRange) return rule.type;
        }

        return MiniGuideType.None;
    }

    private VisualElement GetMiniGuideRoot(MiniGuideType type)
    {
        return type switch
        {
            MiniGuideType.Tutorial   => _miniGuideTutorialRoot,
            MiniGuideType.NextLevel  => _miniGuideNextLevelRoot,
            MiniGuideType.FinalLevel => _miniGuideFinalLevelRoot,
            _                        => null
        };
    }

    private UIDocument GetMiniGuideDocument(MiniGuideType type)
    {
        return type switch
        {
            MiniGuideType.Tutorial   => miniGuideTutorialDocument,
            MiniGuideType.NextLevel  => miniGuideNextLevelDocument,
            MiniGuideType.FinalLevel => miniGuideFinalLevelDocument,
            _                        => null
        };
    }

    private void ShowMiniGuide(MiniGuideType type, Action onDone)
    {
        VisualElement root = GetMiniGuideRoot(type);
        UIDocument doc = GetMiniGuideDocument(type);

        if (root == null)
        {
            Debug.LogWarning($"[LevelIntro] Miniguía '{type}' solicitada pero su UIDocument/elemento no está configurado. Se omite.");
            SetHidden(null, doc);
            onDone?.Invoke();
            return;
        }

        SetVisible(root, doc);
        _miniGuideFadeTween?.Kill();
        StartNextFrame(ref _miniGuideFadeDelayRoutine, () =>
        {
            _miniGuideFadeTween = FadeElement(root, 0f, 1f, fadeInDuration, Ease.OutCubic, () =>
            {
                _miniGuideHoldTween?.Kill();
                _miniGuideHoldTween = DOVirtual.DelayedCall(
                    Mathf.Max(0.5f, miniGuideDuration),
                    () => HideMiniGuide(root, doc, onDone));
            });
        });
    }

    private void HideMiniGuide(VisualElement root, UIDocument doc, Action onDone)
    {
        _miniGuideFadeTween?.Kill();
        float from = root.resolvedStyle.opacity;
        StartNextFrame(ref _miniGuideFadeDelayRoutine, () =>
        {
            _miniGuideFadeTween = FadeElement(root, from, 0f, fadeOutDuration, Ease.InQuad, () =>
            {
                SetHidden(root, doc);
                onDone?.Invoke();
            });
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Secuencia — panel principal de intro
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowIntroPanelAfterMiniGuide() => ShowIntroPanel();

    private void ShowIntroPanel()
    {
        if (_introRoot == null)
        {
            CompleteSequence();
            return;
        }

        SetVisible(_introRoot, introDocument);
        SetVisible(_continueRoot, continueButtonDocument);
        SetActiveLevel(_levelIndex);
        IntroPanelShown?.Invoke();

        _introFadeTween?.Kill();
        _continueButtonFadeTween?.Kill();

        StartNextFrame(ref _introFadeDelayRoutine, () =>
        {
            _introFadeTween = FadeElement(_introRoot, 0f, 1f, fadeInDuration, Ease.OutCubic, null);
        });

        StartNextFrame(ref _continueButtonFadeDelayRoutine, () =>
        {
            _continueButtonFadeTween = FadeElement(_continueRoot, 0f, 1f, fadeInDuration, Ease.OutCubic, null);
        });

        _introAutoAdvanceTween?.Kill();
        _introAutoAdvanceTween = DOVirtual.DelayedCall(Mathf.Max(1f, introAutoAdvanceDelay), AdvanceFromIntro);
    }

    public void AdvanceFromIntro()
    {
        if (!_sequenceRunning || _introAdvanced) return;

        _introAdvanced = true;
        _introAutoAdvanceTween?.Kill();
        _introFadeTween?.Kill();
        _continueButtonFadeTween?.Kill();

        StopDelayRoutine(ref _introFadeDelayRoutine);
        StopDelayRoutine(ref _continueButtonFadeDelayRoutine);

        float fromIntro   = _introRoot?.resolvedStyle.opacity ?? 1f;
        float fromButton  = _continueRoot?.resolvedStyle.opacity ?? 1f;

        StartNextFrame(ref _continueButtonFadeDelayRoutine, () =>
        {
            _continueButtonFadeTween = FadeElement(_continueRoot, fromButton, 0f, fadeOutDuration, Ease.InQuad, () =>
            {
                SetHidden(_continueRoot, continueButtonDocument);
            });
        });

        StartNextFrame(ref _introFadeDelayRoutine, () =>
        {
            _introFadeTween = FadeElement(_introRoot, fromIntro, 0f, fadeOutDuration, Ease.InQuad, () =>
            {
                SetHidden(_introRoot, introDocument);
                CompleteSequence();
            });
        });
    }

    private void CompleteSequence()
    {
        if (_completionTriggered) return;

        _completionTriggered = true;
        _sequenceRunning     = false;

        SetHidden(_introRoot, introDocument);
        SetHidden(_continueRoot, continueButtonDocument);
        SetHidden(_miniGuideTutorialRoot, miniGuideTutorialDocument);
        SetHidden(_miniGuideNextLevelRoot, miniGuideNextLevelDocument);
        SetHidden(_miniGuideFinalLevelRoot, miniGuideFinalLevelDocument);

        Action callback = _onSequenceCompleted;
        _onSequenceCompleted = null;
        callback?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Posicionamiento
    // ─────────────────────────────────────────────────────────────────────────

    private void RepositionPanels()
    {
        Vector3 target;

        if (SceneInteractionManager.Instance != null && SceneInteractionManager.Instance.HasSelectedPlane)
        {
            target = SceneInteractionManager.Instance.SelectedPlanePosition
                     + (SceneInteractionManager.Instance.SelectedPlaneRotation * Vector3.up) * surfaceHeightOffset;
        }
        else
        {
            if (Camera.main == null) return;
            Transform cam = Camera.main.transform;
            Vector3 fwd   = cam.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
            target = cam.position + fwd.normalized;
        }

        if (introDocument              != null) introDocument.transform.position              = target;
        if (miniGuideTutorialDocument  != null) miniGuideTutorialDocument.transform.position  = target;
        if (miniGuideNextLevelDocument != null) miniGuideNextLevelDocument.transform.position = target;
        if (miniGuideFinalLevelDocument!= null) miniGuideFinalLevelDocument.transform.position= target;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Indicadores de nivel — helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheLevelElements()
    {
        if (introDocument == null || introDocument.rootVisualElement == null || levelElementNames == null) return;
        if (_cachedLevelsDocument == introDocument && _levelElements.Count == levelElementNames.Length) return;

        _levelElements.Clear();
        _cachedLevelsDocument = introDocument;

        foreach (string name in levelElementNames)
        {
            VisualElement el = introDocument.rootVisualElement.Q<VisualElement>(name);
            if (el == null) Debug.LogWarning($"[LevelIntro] No se encontró el indicador de nivel '{name}' en introDocument.");
            _levelElements.Add(el);
        }
    }

    private void TweenLevelColors(VisualElement element, Color bgColor, Color borderColor, float duration)
    {
        if (element == null) return;
        StartNextFrame(_levelColorDelayRoutines, () =>
        {
            Tween t = BuildColorTween(element, bgColor, borderColor, duration);
            if (t != null) _levelColorTweens.Add(t);
        });
    }

    private static Tween BuildColorTween(VisualElement element, Color bgColor, Color borderColor, float duration)
    {
        Color fromBg     = element.resolvedStyle.backgroundColor;
        Color fromBorder = element.resolvedStyle.borderLeftColor;

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        seq.Join(DOTween.To(() => fromBg,     v => { fromBg     = v; element.style.backgroundColor = v; }, bgColor,     duration));
        seq.Join(DOTween.To(() => fromBorder, v => { fromBorder = v; SetBorderColor(element, v);         }, borderColor, duration));
        return seq;
    }

    private static void SetBorderColor(VisualElement element, Color color)
    {
        element.style.borderTopColor    = color;
        element.style.borderRightColor  = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor   = color;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers de visibilidad y fade
    // ─────────────────────────────────────────────────────────────────────────

    private static void SetVisible(VisualElement element, UIDocument document)
    {
        if (element == null) return;
        if (document != null && !document.gameObject.activeSelf)
            document.gameObject.SetActive(true);
        element.style.display = DisplayStyle.Flex;
        element.style.opacity = 0f;
    }

    private static void SetHidden(VisualElement element, UIDocument document)
    {
        if (element != null)
        {
            element.style.display = DisplayStyle.None;
            element.style.opacity = 0f;
        }
    }

    private static Tween FadeElement(VisualElement element, float from, float to, float duration, Ease ease, Action onComplete)
    {
        if (element == null)
        {
            onComplete?.Invoke();
            return null;
        }

        float opacity = from;
        element.style.opacity = from;

        return DOTween.To(
                () => opacity,
                v  => { opacity = v; element.style.opacity = v; },
                to, duration)
            .SetUpdate(true)
            .SetEase(ease)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void StartNextFrame(ref Coroutine routine, Action action)
    {
        StopDelayRoutine(ref routine);
        routine = StartCoroutine(NextFrameRoutine(action));
    }

    private void StartNextFrame(List<Coroutine> routines, Action action)
    {
        Coroutine routine = StartCoroutine(NextFrameRoutine(action));
        routines.Add(routine);
    }

    private static IEnumerator NextFrameRoutine(Action action)
    {
        yield return null; // Espera un frame para asegurar que el panel esté activo.
        action?.Invoke();
    }

    private void StopDelayRoutine(ref Coroutine routine)
    {
        if (routine == null) return;
        StopCoroutine(routine);
        routine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Limpieza de tweens
    // ─────────────────────────────────────────────────────────────────────────

    private void KillLevelColorTweens()
    {
        for (int i = 0; i < _levelColorDelayRoutines.Count; i++)
        {
            if (_levelColorDelayRoutines[i] != null) StopCoroutine(_levelColorDelayRoutines[i]);
        }
        _levelColorDelayRoutines.Clear();

        for (int i = 0; i < _levelColorTweens.Count; i++) _levelColorTweens[i]?.Kill();
        _levelColorTweens.Clear();
    }

    private void KillAllTweens()
    {
        _introFadeTween?.Kill();
        _introAutoAdvanceTween?.Kill();
        _continueButtonFadeTween?.Kill();
        _miniGuideFadeTween?.Kill();
        _miniGuideHoldTween?.Kill();

        StopDelayRoutine(ref _introFadeDelayRoutine);
        StopDelayRoutine(ref _continueButtonFadeDelayRoutine);
        StopDelayRoutine(ref _miniGuideFadeDelayRoutine);
        KillLevelColorTweens();
    }
}
