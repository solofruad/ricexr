using System;
using System.Collections;
using System.Collections.Generic;
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

/// <summary>
/// TUTORIAL
///
/// Orquesta los actos guiados del nivel 0: tomar una hoja, observarla, revelar
/// la pista, diagnosticarla y practicar por su cuenta.
///
/// Tras la reforma, la presentacion es UN mensaje a la vez frente al jugador
/// (PlayerFacingMessagePanel), sin avatar y sin paneles apilados sobre la mesa:
/// en VR el jugador ya carga con las gafas, la voz y la accion; la guia visual
/// tiene que ser una sola cosa clara que mirar.
///
/// Lo que NO cambia: la maquina de actos y sus eventos (TutorialActChanged,
/// GuidedPhaseCompleted, ...) que consumen GameNarrationController y
/// GameFlowController, la gestion de marcadores de las hojas y el bloqueo del
/// menu de diagnostico fuera del acto de diagnosticar.
/// </summary>
[DisallowMultipleComponent]
public class TutorialPanelController : MonoBehaviour
{
    public event Action TutorialStarted;
    public event Action GuidedPhaseCompleted;
    public event Action<TutorialGuidanceAct> TutorialActChanged;
    public event Action TutorialCompleted;

    [Header("Panel de mensajes")]
    [Tooltip("PanelSettings compartido del proyecto para el UIDocument del mensaje.")]
    [SerializeField] private PanelSettings panelSettings;

    [Tooltip("UXML del mensaje (Assets/Hojas/UI/Tutorial/TutorialMessage.uxml).")]
    [SerializeField] private VisualTreeAsset messageUxml;

    [Header("Mensajes (en orden de acto)")]
    [Tooltip("Un mensaje por acto guiado, en orden: tomar hoja, observar, revelar pista, diagnosticar, practica libre.")]
    [SerializeField] private List<TutorialMessage> messages = new List<TutorialMessage>
    {
        new TutorialMessage
        {
            title = "Toma una hoja",
            body = "Agarra una hoja de la mesa con la mano.",
            iconElement = "icon-grab"
        },
        new TutorialMessage
        {
            title = "Obsérvala",
            body = "Gírala y busca las manchas de la enfermedad.",
            iconElement = "icon-observe"
        },
        new TutorialMessage
        {
            title = "Los síntomas aparecen",
            body = "Las manchas quedan marcadas en la hoja.",
            iconElement = ""
        },
        new TutorialMessage
        {
            title = "Diagnostica",
            body = "Elige la enfermedad y la severidad en el menú, y confirma.",
            iconElement = "icon-diagnose"
        },
        new TutorialMessage
        {
            title = "¡Bien hecho!",
            body = "Diagnostica otra hoja por tu cuenta.",
            iconElement = ""
        }
    };

    [Header("Actos guiados")]
    [Tooltip("Tiempo en segundos de observacion antes de revelar la pista.")]
    [SerializeField] private float observeToDiagnoseDelay = 3f;
    [Tooltip("Tiempo en segundos que la pista permanece visible antes de abrir el menu.")]
    [SerializeField] private float hintToDiagnoseDelay = 3f;

    private PlayerFacingMessagePanel _messagePanel;
    private Coroutine _actTimerRoutine;

    private bool _isVisible;
    private bool _guidedPhaseCompleted;
    private bool _tutorialFullyCompleted;
    private int _plantsSelected;
    private int _plantsRequired = 2;
    private TutorialGuidanceAct _currentAct = TutorialGuidanceAct.NONE;
    private TutorialGuidanceAct _resumeActAfterGrab = TutorialGuidanceAct.NONE;
    private Leaf _guidedLeaf;

    private void Awake()
    {
        EnsureMessagePanel();
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
        StopActTimer();
    }

    private void OnDestroy() => StopActTimer();

    // ─── API pública ──────────────────────────────────────────────────────────

    /// <summary>
    /// Comienza el tutorial desde el primer acto. Publica OnTutorialStarted, que
    /// es la señal con la que GameFlowController spawnea las hojas del nivel.
    /// </summary>
    public void ShowAndStart()
    {
        EnsureMessagePanel();
        StopActTimer();
        ResetSessionState();
        DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
        _isVisible = true;

        TutorialStarted?.Invoke();
        GameEventBus.PublishTutorialStarted();
        EnterAct(TutorialGuidanceAct.GRAB_LEAF, true);
    }

    /// <summary>
    /// Oculta el mensaje con fade, corta los timers y resetea el acto actual.
    /// </summary>
    public void Hide()
    {
        if (!_isVisible) return;

        StopActTimer();
        _isVisible = false;
        _currentAct = TutorialGuidanceAct.NONE;
        DiseaseSelectionSystem.Instance?.SetPanelAvailability(true);
        _messagePanel?.Hide();
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
    /// Regresa al acto de agarrar hoja si el usuario la suelta antes de
    /// diagnosticar. Al volver a agarrar, se retoma el acto donde iba.
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
                StopActTimer();
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
            int remaining = Mathf.Max(0, _plantsRequired - _plantsSelected);
            EnterAct(remaining > 0
                ? TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF
                : TutorialGuidanceAct.COMPLETED, true);

            if (remaining == 0) CompleteTutorial();
        }
    }

    /// <summary>
    /// Completa el tutorial cuando el usuario ha seleccionado correctamente todas
    /// las plantas requeridas.
    /// </summary>
    private void HandleAllPlantsSelected()
    {
        if (!_isVisible || _tutorialFullyCompleted) return;
        EnterAct(TutorialGuidanceAct.COMPLETED, true);
        CompleteTutorial();
    }

    // ─── Maquina de actos ─────────────────────────────────────────────────────

    /// <summary>
    /// Transiciona al acto indicado, muestra su mensaje y dispara el timer de
    /// observacion si corresponde. El parametro force permite re-entrar al mismo
    /// acto, util para reiniciar GRAB_LEAF cuando el usuario suelta y vuelve a
    /// tomar la hoja.
    /// </summary>
    private void EnterAct(TutorialGuidanceAct act, bool force = false)
    {
        if (!force && _currentAct == act) return;

        _currentAct = act;
        StopActTimer();
        RenderAct(act);
        TutorialActChanged?.Invoke(act);

        if (act == TutorialGuidanceAct.OBSERVE_LEAF)
        {
            _guidedLeaf?.SetMarkersAvailability(false);
            _guidedLeaf?.HideMarkersImmediate();
            DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
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
        }
        else if (act == TutorialGuidanceAct.GRAB_LEAF)
        {
            DiseaseSelectionSystem.Instance?.SetPanelAvailability(false);
        }
    }

    /// <summary>
    /// Muestra el mensaje del acto. Un acto, un mensaje, frente al jugador.
    /// </summary>
    private void RenderAct(TutorialGuidanceAct act)
    {
        int index = MessageIndexForAct(act);
        if (index < 0)
        {
            // COMPLETED (o NONE): sin mensaje. La felicitacion final la muestra
            // UIMessagesController al completar el nivel.
            _messagePanel?.Hide();
            return;
        }

        ShowMessage(index);
    }

    private static int MessageIndexForAct(TutorialGuidanceAct act)
    {
        switch (act)
        {
            case TutorialGuidanceAct.GRAB_LEAF: return 0;
            case TutorialGuidanceAct.OBSERVE_LEAF: return 1;
            case TutorialGuidanceAct.REVEAL_HINT: return 2;
            case TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF: return 3;
            case TutorialGuidanceAct.FREE_PRACTICE_SECOND_LEAF: return 4;
            default: return -1;
        }
    }

    private void ShowMessage(int index)
    {
        TutorialMessage message = messages != null && index >= 0 && index < messages.Count
            ? messages[index]
            : null;

        if (message == null || _messagePanel == null)
        {
            Debug.LogWarning($"[Tutorial] No hay mensaje configurado para el indice {index}.");
            return;
        }

        _messagePanel.Show(message.title, message.body, message.iconElement);
    }

    // ─── Timers de actos ──────────────────────────────────────────────────────

    /// <summary>
    /// Temporizador de transicion entre actos. Va en corrutina y NO en
    /// DOVirtual.DelayedCall: en Android/IL2CPP los DelayedCall fallan
    /// silenciosamente (ver nota en UIGameListener).
    /// </summary>
    private void StartActTimer(TutorialGuidanceAct nextAct, float delay)
    {
        StopActTimer();
        _actTimerRoutine = StartCoroutine(ActTimerRoutine(nextAct, Mathf.Max(0.5f, delay)));
    }

    private void StopActTimer()
    {
        if (_actTimerRoutine == null) return;
        StopCoroutine(_actTimerRoutine);
        _actTimerRoutine = null;
    }

    private IEnumerator ActTimerRoutine(TutorialGuidanceAct nextAct, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Mismas guardas que tenia el timer original: si el jugador solto la
        // hoja, fallo un diagnostico o el tutorial termino, no se avanza.
        if (!_isVisible || _tutorialFullyCompleted || _guidedPhaseCompleted) yield break;
        if (_currentAct == TutorialGuidanceAct.NONE || _currentAct == TutorialGuidanceAct.GRAB_LEAF) yield break;
        if (GrabbableLeafListener.Instance?.ActualLeafGrabbed == null) yield break;

        if (_currentAct == TutorialGuidanceAct.OBSERVE_LEAF && nextAct != TutorialGuidanceAct.REVEAL_HINT) yield break;
        if (_currentAct == TutorialGuidanceAct.REVEAL_HINT && nextAct != TutorialGuidanceAct.DIAGNOSE_FIRST_LEAF) yield break;

        _actTimerRoutine = null;
        EnterAct(nextAct);
    }

    // ─── Utilidades ───────────────────────────────────────────────────────────

    private void CompleteTutorial()
    {
        if (_tutorialFullyCompleted) return;
        _tutorialFullyCompleted = true;
        StopActTimer();
        DiseaseSelectionSystem.Instance?.SetPanelAvailability(true);
        TutorialCompleted?.Invoke();
        GameEventBus.PublishTutorialCompleted();
        _messagePanel?.Hide();
    }

    /// <summary>
    /// Oculta el mensaje de forma inmediata sin animaciones. Util al inicializar.
    /// </summary>
    private void HideImmediate()
    {
        _isVisible = false;
        _currentAct = TutorialGuidanceAct.NONE;
        _messagePanel?.HideImmediate();
    }

    /// <summary>
    /// Resetea el estado interno para permitir reiniciar el tutorial sin
    /// recargar la escena.
    /// </summary>
    private void ResetSessionState()
    {
        _guidedPhaseCompleted = false;
        _tutorialFullyCompleted = false;
        _plantsSelected = 0;
        _plantsRequired = 2;
        _currentAct = TutorialGuidanceAct.NONE;
        _resumeActAfterGrab = TutorialGuidanceAct.NONE;
        _guidedLeaf = null;
    }

    /// <summary>
    /// Crea el panel de mensajes como hijo de este controller. El panel se
    /// autogestiona (UIDocument + billboard + fades); aqui solo se le pasan los
    /// assets y se compensa la escala heredada de UIManager/TutorialController
    /// para que su tamano de mundo sea siempre el configurado.
    /// </summary>
    private void EnsureMessagePanel()
    {
        if (_messagePanel != null) return;

        if (panelSettings == null || messageUxml == null)
        {
            Debug.LogWarning("[Tutorial] Falta asignar panelSettings o messageUxml; los mensajes del tutorial no se mostraran.");
            return;
        }

        GameObject panelObject = new GameObject("TutorialMessagePanel");
        panelObject.transform.SetParent(transform, false);

        Vector3 inherited = transform.lossyScale;
        if (Mathf.Abs(inherited.x) > 0.0001f
            && Mathf.Abs(inherited.y) > 0.0001f
            && Mathf.Abs(inherited.z) > 0.0001f)
        {
            panelObject.transform.localScale = new Vector3(
                1f / inherited.x, 1f / inherited.y, 1f / inherited.z);
        }

        _messagePanel = panelObject.AddComponent<PlayerFacingMessagePanel>();
        _messagePanel.Initialize(panelSettings, messageUxml);
    }
}
