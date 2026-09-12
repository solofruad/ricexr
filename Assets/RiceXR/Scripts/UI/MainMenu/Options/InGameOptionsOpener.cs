using Oculus.Interaction;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// DISPARADOR DEL PANEL DE OPCIONES EN PARTIDA
///
/// Decide CUANDO se abre el panel de opciones durante una sesion. El panel en si no sabe
/// nada de esto: OptionsController solo expone OpenInGame() y Close(), y hasta ahora nadie
/// los llamaba, asi que durante el gameplay no habia forma de salir de la partida.
///
/// DOS DISPARADORES, UN SOLO MECANISMO:
/// Los dos llegan como IActiveState del Interaction SDK, asi que este componente no lee
/// input a mano ni depende de OVRInput.
///   - Gesto: palma izquierda hacia arriba y mano abierta, sostenido 'gestureHoldSeconds'.
///     El grafo del gesto se arma en la escena con ShapeRecognizerActiveState +
///     TransformRecognizerActiveState dentro de un ActiveStateGroup en AND.
///   - Mando: boton de menu (☰) del mando izquierdo, via ControllerButtonUsageActiveState.
///     El del mando derecho es el boton de Meta y lo reserva el sistema.
/// Como OVRManager tiene SimultaneousHandsAndControllersEnabled desactivado, manos y
/// mandos son excluyentes y los dos disparadores nunca compiten.
///
/// POR QUE EL GESTO SE SOSTIENE:
/// El juego se juega con las manos abiertas manipulando hojas. Sin tiempo de retencion, el
/// menu saltaria solo constantemente. A eso se suma que no se abre con una hoja agarrada:
/// quien tiene una hoja en la mano esta diagnosticando, no buscando el menu.
///
/// El gesto es de flanco: despues de disparar hay que SOLTARLO para que vuelva a contar.
/// Si no, con el panel anclado a la mano la postura natural de sujetarlo lo cerraria solo.
/// </summary>
[DisallowMultipleComponent]
public class InGameOptionsOpener : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private OptionsController optionsController;

    [Header("Gesto de mano")]
    [Tooltip("Estado del Interaction SDK que representa el gesto de apertura. " +
             "Normalmente un ActiveStateGroup en AND.")]
    [SerializeField, Interface(typeof(IActiveState))]
    private UnityEngine.Object openGestureSource;

    [Tooltip("Segundos que hay que sostener el gesto. Subirlo es lo primero que hay que " +
             "probar si el menu se abre solo.")]
    [SerializeField] private float gestureHoldSeconds = 1f;

    [Header("Boton del mando")]
    [Tooltip("ControllerButtonUsageActiveState del mando izquierdo con MenuButton.")]
    [SerializeField, Interface(typeof(IActiveState))]
    private UnityEngine.Object menuButtonSource;

    private IActiveState _openGesture;
    private IActiveState _menuButton;

    private float _gestureHeldTime;
    private bool _gestureArmed = true;
    private bool _menuButtonWasActive;

    // ─── Ciclo de vida ────────────────────────────────────────────────────────

    private void Awake()
    {
        _openGesture = openGestureSource as IActiveState;
        _menuButton = menuButtonSource as IActiveState;

        if (optionsController == null)
            optionsController = FindObjectOfType<OptionsController>(true);

        if (_openGesture == null && _menuButton == null)
            Debug.LogWarning("[InGameOptions] No hay ningun disparador cableado: el panel de " +
                             "opciones no podra abrirse durante la partida.", this);
    }

    private void OnEnable()
    {
        GameEventBus.OnReturnToMenuRequested += HandleSessionInterrupted;
        GameEventBus.OnAllLevelsCompleted += HandleSessionInterrupted;
    }

    private void OnDisable()
    {
        GameEventBus.OnReturnToMenuRequested -= HandleSessionInterrupted;
        GameEventBus.OnAllLevelsCompleted -= HandleSessionInterrupted;
    }

    private void Update()
    {
        if (optionsController == null) return;

        UpdateGesture();
        UpdateMenuButton();

#if UNITY_EDITOR
        // Atajo de desarrollo: recorrer el flujo entero en el editor sin casco ni manos,
        // igual que la tecla C de DiseaseSelectionSystem. No llega a la build.
        if (Keyboard.current?.oKey.wasPressedThisFrame == true)
            Toggle();
#endif
    }

    // ─── Disparadores ─────────────────────────────────────────────────────────

    private void UpdateGesture()
    {
        if (_openGesture == null) return;

        if (!_openGesture.Active)
        {
            // Soltar el gesto es lo que lo rearma.
            _gestureHeldTime = 0f;
            _gestureArmed = true;
            return;
        }

        if (!_gestureArmed) return;

        _gestureHeldTime += Time.deltaTime;
        if (_gestureHeldTime < gestureHoldSeconds) return;

        _gestureArmed = false;
        _gestureHeldTime = 0f;
        Toggle();
    }

    private void UpdateMenuButton()
    {
        if (_menuButton == null) return;

        bool active = _menuButton.Active;
        if (active && !_menuButtonWasActive)
            Toggle();

        _menuButtonWasActive = active;
    }

    // ─── Apertura y cierre ────────────────────────────────────────────────────

    /// <summary>
    /// Alterna el panel. Publico para poder cablearlo tambien desde el Inspector, al
    /// estilo de los botones de UI Toolkit del proyecto.
    /// </summary>
    public void Toggle()
    {
        if (optionsController == null) return;

        if (optionsController.IsOpen)
        {
            optionsController.Close();
            return;
        }

        if (!CanOpen()) return;

        optionsController.OpenInGame();
    }

    private bool CanOpen()
    {
        GameFlowController flow = GameFlowController.Instance;
        if (flow == null || !IsSessionRunning(flow.CurrentState)) return false;

        // Con una hoja agarrada el jugador esta diagnosticando. Ademas de evitar falsos
        // positivos del gesto, impide que el panel salga encima del menu de diagnostico.
        GrabbableLeafListener grab = GrabbableLeafListener.Instance;
        return grab == null || grab.ActualLeafGrabbed == null;
    }

    /// <summary>
    /// Estados en los que hay una partida viva detras del panel. Fuera de ellos el menu
    /// principal ya esta a la vista o la sesion se esta cerrando sola.
    /// </summary>
    private static bool IsSessionRunning(FlowState state)
    {
        switch (state)
        {
            case FlowState.None:
            case FlowState.Idle:
            case FlowState.AllLevelsCompleted:
            case FlowState.SessionEnding:
                return false;
            default:
                return true;
        }
    }

    // Si la sesion termina con el panel abierto, se cierra solo: dejarlo colgado en la
    // mano encima del menu principal seria el peor de los dos mundos.
    private void HandleSessionInterrupted()
    {
        optionsController?.Close();
    }
}
