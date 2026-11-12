using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla la visibilidad del panel y sincroniza sus controles con las opciones guardadas.
/// El contexto de apertura determina si se muestra el botón de regreso al menú.
/// </summary>
public class OptionsController : MonoBehaviour
{
    [Header("Opciones")]
    [SerializeField] private Toggle skipIntroToggle;
    [SerializeField] private Toggle skipTutorialToggle;
    [SerializeField] private Toggle disableVoicesToggle;
    [SerializeField] private Button returnToMainMenuButton;

    [Header("Sliders")]
    [SerializeField] private Slider voiceVelocitySlider;
    [SerializeField] private TextMeshProUGUI voiceVelocityValueLabel;

    [Header("Dependencias")]
    [SerializeField] private NarrationReproductor narrationReproductor;
    [SerializeField] private MainMenuController mainMenuController;

    [Header("Apertura en partida")]
    [Tooltip("Distancia horizontal en metros entre la cabeza y el panel al abrirlo en partida.")]
    [Min(0.2f)] [SerializeField] private float inGameDistance = 0.8f;
    [Tooltip("Desplazamiento vertical en metros respecto a los ojos al abrirlo en partida.")]
    [SerializeField] private float inGameHeightOffset = -0.1f;

    private OptionsContext _context = OptionsContext.MainMenu;

    // Pose local del panel junto al menú principal. En partida el panel se lleva frente
    // al jugador y al cerrarse vuelve aquí, para que el menú lo encuentre en su sitio.
    private Vector3 _homeLocalPosition;
    private Quaternion _homeLocalRotation;
    private bool _homeSaved;

    // El tutorial también bloquea el menú de diagnóstico durante sus actos guiados, así
    // que al cerrar hay que devolverlo a como estaba y no darlo por habilitado.
    private bool _diagnosisAvailableBeforeOpen = true;

    private void Awake()
    {
        ResolveReferences();
        ApplyCurrentSettings();
        SetReturnButtonVisibility();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyCurrentSettings();

        skipIntroToggle?.onValueChanged.AddListener(OnSkipIntroChanged);
        skipTutorialToggle?.onValueChanged.AddListener(OnSkipTutorialChanged);
        disableVoicesToggle?.onValueChanged.AddListener(OnDisableVoicesChanged);
        voiceVelocitySlider?.onValueChanged.AddListener(OnVoiceVelocityChanged);
        returnToMainMenuButton?.onClick.AddListener(OnReturnToMainMenuClicked);
    }

    private void OnDisable()
    {
        skipIntroToggle?.onValueChanged.RemoveListener(OnSkipIntroChanged);
        skipTutorialToggle?.onValueChanged.RemoveListener(OnSkipTutorialChanged);
        disableVoicesToggle?.onValueChanged.RemoveListener(OnDisableVoicesChanged);
        voiceVelocitySlider?.onValueChanged.RemoveListener(OnVoiceVelocityChanged);
        returnToMainMenuButton?.onClick.RemoveListener(OnReturnToMainMenuClicked);
    }

    /// <summary>Indica si el panel está visible.</summary>
    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// Abre o cierra el panel cuando se está en el menú principal.
    /// </summary>
    public void ToggleFromMainMenu()
    {
        if (gameObject.activeSelf)
        {
            Close();
            return;
        }

        _context = OptionsContext.MainMenu;
        // En el menú principal el panel vive en su sitio fijo del prefab, como panel
        // lateral del menú.
        RestoreHomePose();
        Open();
    }

    /// <summary>
    /// Abre el panel durante una partida y muestra el botón para abandonarla.
    /// </summary>
    public void OpenInGame()
    {
        _context = OptionsContext.InGame;
        Open();

        // El panel se abre donde esté el jugador, no donde quedó el menú principal.
        PlaceInFrontOfPlayer();

        // La partida sigue corriendo detrás del panel: no se congela nada. Solo se
        // suspende lo que competiría con él — el menú de diagnóstico usa el mismo rayo
        // y el mismo espacio — y la narración en curso, que hablaría sola.
        DiseaseSelectionSystem diseaseSelection = DiseaseSelectionSystem.Instance;
        _diagnosisAvailableBeforeOpen = diseaseSelection == null || diseaseSelection.PanelAvailable;
        diseaseSelection?.SetPanelAvailability(false);

        narrationReproductor?.Stop();
    }

    /// <summary>Cierra el panel sin modificar el estado de la partida.</summary>
    public void Close()
    {
        if (!gameObject.activeSelf) return;

        if (_context == OptionsContext.InGame)
        {
            RestoreHomePose();
            DiseaseSelectionSystem.Instance?.SetPanelAvailability(_diagnosisAvailableBeforeOpen);
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Coloca el panel frente al jugador, a la altura de los ojos y mirándolo de frente
    /// (solo gira sobre Y). Se posiciona el centro del Canvas visible y no la raíz: el
    /// Canvas está desplazado dentro del prefab y, si se moviera la raíz, el panel
    /// quedaría a un lado. Se coloca una vez y se queda quieto para poder apuntarle.
    /// </summary>
    private void PlaceInFrontOfPlayer()
    {
        if (Camera.main == null) return;

        SaveHomePose();

        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();

        Vector3 target = cam.position + forward * inGameDistance + Vector3.up * inGameHeightOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        Canvas canvas = GetComponentInChildren<Canvas>(true);
        Vector3 visualCenter = canvas != null ? canvas.transform.position : transform.position;
        transform.position += target - visualCenter;
    }

    private void SaveHomePose()
    {
        if (_homeSaved) return;

        _homeLocalPosition = transform.localPosition;
        _homeLocalRotation = transform.localRotation;
        _homeSaved = true;
    }

    private void RestoreHomePose()
    {
        if (!_homeSaved) return;

        transform.localPosition = _homeLocalPosition;
        transform.localRotation = _homeLocalRotation;
    }

    private void Open()
    {
        ResolveReferences();
        SetReturnButtonVisibility();
        gameObject.SetActive(true);
        ApplyCurrentSettings();
    }

    private void ResolveReferences()
    {
        // El panel se instancia dentro de distintos contenedores, así que sus controles
        // se localizan por nombre en vez de depender de referencias de una escena concreta.
        if (skipIntroToggle == null || skipTutorialToggle == null || disableVoicesToggle == null)
        {
            Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                if (toggle == null) continue;
                if (skipIntroToggle == null && NameMatches(toggle.name, "Intro"))
                    skipIntroToggle = toggle;
                else if (skipTutorialToggle == null && NameMatches(toggle.name, "Tutorial"))
                    skipTutorialToggle = toggle;
                else if (disableVoicesToggle == null && NameMatches(toggle.name, "Voices"))
                    disableVoicesToggle = toggle;
            }
        }

        if (returnToMainMenuButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button != null && NameMatches(button.name, "ReturnToMenu"))
                {
                    returnToMainMenuButton = button;
                    break;
                }
            }
        }

        if (voiceVelocitySlider == null)
            voiceVelocitySlider = GetComponentInChildren<Slider>(true);

        if (voiceVelocityValueLabel == null)
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                if (label != null && label.name == "SliderNumber")
                {
                    voiceVelocityValueLabel = label;
                    break;
                }
            }
        }

        if (narrationReproductor == null)
            narrationReproductor = FindObjectOfType<NarrationReproductor>(true);

        if (mainMenuController == null)
            mainMenuController = FindObjectOfType<MainMenuController>(true);
    }

    /// <summary>
    /// Compara el nombre de un control con el esperado, tolerando el sufijo que añaden
    /// los prefabs. 
    /// </summary>
    private static bool NameMatches(string actualName, string baseName)
    {
        return actualName == baseName
            || actualName == baseName + "Toggle"
            || actualName == baseName + "Button";
    }

    private void ApplyCurrentSettings()
    {
        // Al cargar los valores no queremos que los eventos de los controles se ejecuten
        // como si acabaran de cambiarse.
        skipIntroToggle?.SetIsOnWithoutNotify(GameOptions.SkipIntro);
        skipTutorialToggle?.SetIsOnWithoutNotify(GameOptions.SkipTutorial);
        disableVoicesToggle?.SetIsOnWithoutNotify(!GameOptions.VoicesEnabled);
        voiceVelocitySlider?.SetValueWithoutNotify(GameOptions.VoicePitch);
        UpdateVoiceVelocityLabel(GameOptions.VoicePitch);

        narrationReproductor?.SetVoicesEnabled(GameOptions.VoicesEnabled);
        narrationReproductor?.SetVoicePitch(GameOptions.VoicePitch);
    }

    private void SetReturnButtonVisibility()
    {
        if (returnToMainMenuButton != null)
            returnToMainMenuButton.gameObject.SetActive(_context == OptionsContext.InGame);
    }

    private void OnSkipIntroChanged(bool skipIntro)
    {
        GameOptions.SkipIntro = skipIntro;
    }

    private void OnSkipTutorialChanged(bool skipTutorial)
    {
        GameOptions.SkipTutorial = skipTutorial;
    }

    private void OnDisableVoicesChanged(bool disableVoices)
    {
        GameOptions.VoicesEnabled = !disableVoices;
        narrationReproductor?.SetVoicesEnabled(!disableVoices);
    }

    private void OnVoiceVelocityChanged(float pitch)
    {
        GameOptions.VoicePitch = pitch;
        narrationReproductor?.SetVoicePitch(pitch);
        UpdateVoiceVelocityLabel(pitch);
    }

    private void UpdateVoiceVelocityLabel(float pitch)
    {
        if (voiceVelocityValueLabel != null)
            voiceVelocityValueLabel.text = pitch.ToString("0.0");
    }

    private void OnReturnToMainMenuClicked()
    {
        if (_context != OptionsContext.InGame)
        {
            Close();
            return;
        }

        // Este evento sí reinicia el flujo de juego. Por eso el botón solo se muestra
        // cuando el panel se abrió con el contexto InGame.
        GameEventBus.PublishReturnToMenuRequested();
        Close();
        mainMenuController?.ShowMainMenu();
    }
}
