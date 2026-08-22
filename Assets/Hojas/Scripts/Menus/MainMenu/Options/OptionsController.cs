using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opciones generales que se conservan aunque se cierre la aplicación.
/// No forman parte del progreso ni de los datos registrados de una partida.
/// </summary>
public static class GameOptions
{
    private const string SkipTutorialKey = "Hojas.Options.SkipTutorial";
    private const string VoicesEnabledKey = "Hojas.Options.VoicesEnabled";
    private const string VoicePitchKey = "Hojas.Options.VoicePitch";

    /// <summary>Indica si se omite la guía interactiva del primer nivel.</summary>
    public static bool SkipTutorial
    {
        get => PlayerPrefs.GetInt(SkipTutorialKey, 0) == 1;
        set => SetBool(SkipTutorialKey, value);
    }

    /// <summary>Indica si la narración por voz puede reproducirse.</summary>
    public static bool VoicesEnabled
    {
        get => PlayerPrefs.GetInt(VoicesEnabledKey, 1) == 1;
        set => SetBool(VoicesEnabledKey, value);
    }

    /// <summary>
    /// Velocidad de reproducción aplicada al AudioSource de la narración.
    /// </summary>
    public static float VoicePitch
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(VoicePitchKey, 1f), 0.5f, 3f);
        set
        {
            PlayerPrefs.SetFloat(VoicePitchKey, Mathf.Clamp(value, 0.5f, 3f));
            PlayerPrefs.Save();
        }
    }

    private static void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// Contexto desde el que se abrió el panel de opciones.
/// </summary>
public enum OptionsContext
{
    MainMenu,
    InGame
}

/// <summary>
/// Controla la visibilidad del panel y sincroniza sus controles con las opciones guardadas.
/// El contexto de apertura determina si se muestra el botón de regreso al menú.
/// </summary>
public class OptionsController : MonoBehaviour
{
    [Header("Opciones")]
    [SerializeField] private Toggle skipTutorialToggle;
    [SerializeField] private Toggle disableVoicesToggle;
    [SerializeField] private Button returnToMainMenuButton;

    [Header("Sliders")]
    [SerializeField] private Slider voiceVelocitySlider;
    [SerializeField] private TextMeshProUGUI voiceVelocityValueLabel;

    [Header("Dependencias")]
    [SerializeField] private NarrationReproductor narrationReproductor;
    [SerializeField] private MainMenuController mainMenuController;

    private OptionsContext _context = OptionsContext.MainMenu;

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

        skipTutorialToggle?.onValueChanged.AddListener(OnSkipTutorialChanged);
        disableVoicesToggle?.onValueChanged.AddListener(OnDisableVoicesChanged);
        voiceVelocitySlider?.onValueChanged.AddListener(OnVoiceVelocityChanged);
        returnToMainMenuButton?.onClick.AddListener(OnReturnToMainMenuClicked);
    }

    private void OnDisable()
    {
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
        Open();
    }

    /// <summary>
    /// Abre el panel durante una partida y muestra el botón para abandonarla.
    /// </summary>
    public void OpenInGame()
    {
        _context = OptionsContext.InGame;
        Open();
    }

    /// <summary>Cierra el panel sin modificar el estado de la partida.</summary>
    public void Close()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
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
        if (skipTutorialToggle == null || disableVoicesToggle == null)
        {
            Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                if (toggle == null) continue;
                if (toggle.name == "Tutorial" && skipTutorialToggle == null)
                    skipTutorialToggle = toggle;
                else if (toggle.name == "Voices" && disableVoicesToggle == null)
                    disableVoicesToggle = toggle;
            }
        }

        if (returnToMainMenuButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button != null && button.name == "ReturnToMenu")
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

    private void ApplyCurrentSettings()
    {
        // Al cargar los valores no queremos que los eventos de los controles se ejecuten
        // como si acabaran de cambiarse.
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
