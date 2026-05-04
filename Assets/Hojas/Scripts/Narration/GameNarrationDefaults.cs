using System.Collections.Generic;

public static class GameNarrationLineIds
{
    public const string StartSelectPlane = "START_SELECT_PLANE";
    public const string OnboardingOverview = "ONBOARDING_OVERVIEW";
    public const string OnboardingContinue = "ONBOARDING_CONTINUE";

    public const string TutorialBegin = "TUTORIAL_BEGIN";
    public const string TutorialGrab = "TUTORIAL_GRAB";
    public const string TutorialInspect = "TUTORIAL_INSPECT";
    public const string TutorialMenuOpen = "TUTORIAL_MENU_OPEN";
    public const string TutorialSelectDisease = "TUTORIAL_SELECT_DISEASE";
    public const string TutorialSelectSeverity = "TUTORIAL_SELECT_SEVERITY";
    public const string TutorialConfirm = "TUTORIAL_CONFIRM";
    public const string TutorialRetryGuided = "TUTORIAL_RETRY_GUIDED";
    public const string TutorialFreePractice = "TUTORIAL_FREE_PRACTICE";
    public const string TutorialDone = "TUTORIAL_DONE";

    public const string Level2Intro = "LEVEL2_INTRO";
    public const string Level2Explain = "LEVEL2_EXPLAIN";
    public const string Level3Intro = "LEVEL3_INTRO";
    public const string Level3Explain = "LEVEL3_EXPLAIN";
    public const string FinalWarning = "FINAL_WARNING";

    public const string ProgressRemain3 = "PROGRESS_REMAIN_3";
    public const string ProgressRemain2 = "PROGRESS_REMAIN_2";
    public const string ProgressRemain1 = "PROGRESS_REMAIN_1";
    public const string ProgressDone = "PROGRESS_DONE";

    public const string FirstCorrect = "FIRST_CORRECT";
    public const string SecondCorrect = "SECOND_CORRECT";
    public const string ThirdCorrect = "THIRD_CORRECT";
    public const string FirstErrorOnly = "FIRST_ERROR_ONLY";

    public const string LevelComplete = "LEVEL_COMPLETE";
    public const string AllLevelsComplete = "ALL_LEVELS_COMPLETE";

    public const string EndSessionPromptSave = "ENDSESSION_PROMPT_SAVE";
    public const string SaveOk = "SAVE_OK";
    public const string SaveFail = "SAVE_FAIL";
    public const string ReturnMenu = "RETURN_MENU";
}

public static class GameNarrationDefaults
{
    public static List<NarrationLineEntry> CreateDefaultEntries()
    {
        return new List<NarrationLineEntry>
        {
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.StartSelectPlane,
                text = "Bienvenido a la experiencia de diagnostico de enfermedades en arroz. Acercate a un plano azul y seleccionalo para comenzar.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.OnboardingOverview,
                text = "Recorreras cuatro niveles: tutorial, Pyricularia, Rhynchosporium y evaluacion final sin ayudas visuales.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.OnboardingContinue,
                text = "Cuando estes listo, pulsa Continuar para iniciar la practica guiada.",
                oncePerSession = true
            },

            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialBegin,
                text = "Inicia el tutorial. Te guiaremos en cada paso del diagnostico.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialGrab,
                text = "Paso uno. Agarra una hoja con tu mano para poder inspeccionarla de cerca.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialInspect,
                text = "Paso dos. Observa la lesion: forma, borde, centro y distribucion sobre la hoja.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialMenuOpen,
                text = "Paso tres. Abre el menu lateral de diagnostico.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialSelectDisease,
                text = "Selecciona la enfermedad que mejor coincide con los sintomas observados.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialSelectSeverity,
                text = "Ahora selecciona la severidad segun el dano visible en la hoja.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialConfirm,
                text = "Presiona Confirmar para registrar tu diagnostico.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialRetryGuided,
                text = "No coincide aun. Vuelve a observar lesion y severidad, y prueba de nuevo.",
                cooldownSec = 0.8f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialFreePractice,
                text = "Excelente. Primer acierto logrado. Te falta una hoja y ahora vas por tu cuenta.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.TutorialDone,
                text = "Excelente. Completaste el tutorial practico y estas listo para el siguiente nivel.",
                oncePerSession = true
            },

            new NarrationLineEntry
            {
                id = GameNarrationLineIds.Level2Intro,
                text = "Nivel dos de cuatro. En este nivel trabajas con Anublo del arroz.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.Level2Explain,
                text = "Pyricularia oryzae suele mostrar manchas necroticas en forma de ojo, con centro gris y borde marron definido.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.Level3Intro,
                text = "Nivel tres de cuatro. Ahora trabajas con Escaldado del arroz.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.Level3Explain,
                text = "Rhynchosporium oryzae suele iniciar en la punta y avanzar en bandas concentricas hacia la base.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.FinalWarning,
                text = "Ultimo nivel. No tendras ayudas visuales. Confia en lo aprendido y diagnostica por tu cuenta.",
                oncePerSession = true
            },

            new NarrationLineEntry
            {
                id = GameNarrationLineIds.ProgressRemain3,
                text = "Te faltan tres hojas para completar este nivel.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.ProgressRemain2,
                text = "Te faltan dos hojas para completar este nivel.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.ProgressRemain1,
                text = "Te falta una hoja para completar este nivel.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.ProgressDone,
                text = "Completaste las hojas requeridas de este nivel.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },

            new NarrationLineEntry
            {
                id = GameNarrationLineIds.FirstCorrect,
                text = "Bien hecho. Ese diagnostico es correcto.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.SecondCorrect,
                text = "Excelente. Vas por muy buen camino.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.ThirdCorrect,
                text = "Muy bien. Objetivo del nivel cumplido.",
                oncePerLevel = true,
                cooldownSec = 0.25f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.FirstErrorOnly,
                text = "Ese diagnostico no coincide. Revisa nuevamente la lesion y la severidad antes de confirmar.",
                firstErrorOnly = true,
                cooldownSec = 0.25f
            },

            new NarrationLineEntry
            {
                id = GameNarrationLineIds.LevelComplete,
                text = "Nivel completado. Preparate para la siguiente fase.",
                oncePerLevel = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.AllLevelsComplete,
                text = "Excelente trabajo. Has completado toda la experiencia.",
                oncePerSession = true
            },

            new NarrationLineEntry
            {
                id = GameNarrationLineIds.EndSessionPromptSave,
                text = "Ingresa tu apodo para guardar el resultado en el marcador.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.SaveOk,
                text = "Guardado exitoso. Tu resultado fue registrado correctamente.",
                oncePerSession = true
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.SaveFail,
                text = "No se pudo guardar el resultado. Intenta nuevamente.",
                cooldownSec = 1f
            },
            new NarrationLineEntry
            {
                id = GameNarrationLineIds.ReturnMenu,
                text = "Sesion finalizada. Regresando al menu principal.",
                oncePerSession = true
            }
        };
    }
}
