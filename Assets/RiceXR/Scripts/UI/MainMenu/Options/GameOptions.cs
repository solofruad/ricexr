using UnityEngine;

/// <summary>
/// Opciones generales que se conservan aunque se cierre la aplicación.
/// No forman parte del progreso ni de los datos registrados de una partida.
/// </summary>
public static class GameOptions
{
    private const string SkipIntroKey = "Hojas.Options.SkipIntro";
    private const string SkipTutorialKey = "Hojas.Options.SkipTutorial";
    private const string VoicesEnabledKey = "Hojas.Options.VoicesEnabled";
    private const string VoicePitchKey = "Hojas.Options.VoicePitch";

    /// <summary>Indica si se omite la introducción que precede a la selección de plano.</summary>
    public static bool SkipIntro
    {
        get => PlayerPrefs.GetInt(SkipIntroKey, 0) == 1;
        set => SetBool(SkipIntroKey, value);
    }

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
