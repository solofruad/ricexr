using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DiseaseSelectionSystem : MonoBehaviour
{
    [Header("Toggle Groups")]
    [Tooltip("ToggleGroup para las enfermedades")]
    public ToggleGroup diseaseToggleGroup;

    [Tooltip("ToggleGroup para la severidad")]
    public ToggleGroup severityToggleGroup;

    [Header("Mapeo de Enfermedades")]
    [Tooltip("Toggles de enfermedades en orden")]
    public List<Toggle> diseaseToggles = new List<Toggle>();

    [Tooltip("Nombres correspondientes a cada toggle")]
    public List<string> diseaseNames = new List<string>
    {
        "Pyricularia oryzae",
        "Rhynchosporium oryzae",
    };

    [Header("Mapeo de Severidad")]
    [Tooltip("Toggles de severidad en orden (1-5)")]
    public List<Toggle> severityToggles = new List<Toggle>();

    [Header("Botón de Submit")]
    public Button submitButton;

    [Header("Objetos de Feedback")]
    [Tooltip("GameObject que se mostrará cuando sea correcto")]
    public GameObject correctFeedbackObject;

    [Tooltip("GameObject que se mostrará cuando sea incorrecto")]
    public GameObject incorrectFeedbackObject;

    [Tooltip("Duración que el feedback estará visible")]
    public float feedbackDuration = 3f;

    [Tooltip("Margen de error permitido en la severidad")]
    public int MarginOfError = 0;

    void Start()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmit);
        }

        // Asegurarse de que los objetos de feedback estén ocultos al inicio
        if (correctFeedbackObject != null)
        {
            correctFeedbackObject.SetActive(false);
        }

        if (incorrectFeedbackObject != null)
        {
            incorrectFeedbackObject.SetActive(false);
        }
    }

    void OnSubmit()
    {
        // Verificar que hay una hoja agarrada
        Leaf currentLeaf = GrabbableObjectListener.Instance.ActualLeafGrabbed;

        if (currentLeaf == null)
        {
            Debug.LogWarning("No hay ninguna hoja agarrada");
            return;
        }

        // Obtener las selecciones actuales
        string selectedDisease = GetSelectedDisease();
        int selectedSeverity = GetSelectedSeverity();

        // Verificar que se ha seleccionado algo
        if (string.IsNullOrEmpty(selectedDisease))
        {
            Debug.LogWarning("Debes seleccionar una enfermedad");
            return;
        }

        if (selectedSeverity <= 0)
        {
            Debug.LogWarning("Debes seleccionar una severidad");
            return;
        }

        // Validar contra la hoja
        bool isCorrect = ValidateSelection(currentLeaf, selectedDisease, selectedSeverity);

        // Mostrar feedback
        ShowFeedback(isCorrect);

        // Si es correcto, mostrar marcadores y avanzar al siguiente nivel
        if (isCorrect)
        {
            currentLeaf.ableToShowMarkers = true;
            currentLeaf.showMarkers = true;
            currentLeaf.SetMarkersVisibility(true);

            // Avanzar al siguiente nivel después de mostrar el feedback
            StartCoroutine(AdvanceToNextLevel());
        }
    }

    string GetSelectedDisease()
    {
        if (diseaseToggleGroup == null)
        {
            Debug.LogError("No se ha asignado el ToggleGroup de enfermedades");
            return null;
        }

        // Obtener el toggle activo del grupo
        Toggle activeToggle = diseaseToggleGroup.GetFirstActiveToggle();

        if (activeToggle == null)
        {
            return null;
        }

        // Encontrar el índice del toggle activo
        int index = diseaseToggles.IndexOf(activeToggle);

        if (index >= 0 && index < diseaseNames.Count)
        {
            return diseaseNames[index];
        }

        Debug.LogWarning($"Toggle activo no encontrado en la lista de disease toggles");
        return null;
    }

    int GetSelectedSeverity()
    {
        if (severityToggleGroup == null)
        {
            Debug.LogError("No se ha asignado el ToggleGroup de severidad");
            return -1;
        }

        // Obtener el toggle activo del grupo
        Toggle activeToggle = severityToggleGroup.GetFirstActiveToggle();

        if (activeToggle == null)
        {
            return -1;
        }

        // Encontrar el índice del toggle activo (índice + 1 = severidad)
        int index = severityToggles.IndexOf(activeToggle);

        if (index >= 0)
        {
            return index + 1;
        }

        Debug.LogWarning($"Toggle activo no encontrado en la lista de severity toggles");
        return -1;
    }

    bool ValidateSelection(Leaf leaf, string selectedDisease, int selectedSeverity)
    {
        // Obtener la primera mancha de la hoja
        if (leaf.diseaseSpots.Count == 0)
        {
            Debug.LogWarning("La hoja no tiene manchas configuradas");
            return false;
        }

        DiseaseSpot firstSpot = leaf.diseaseSpots[0];

        // Validar enfermedad (debe coincidir exactamente)
        bool diseaseCorrect = selectedDisease == firstSpot.diseaseName;

        // Validar severidad (permitir margen de error configurado)
        bool severityCorrect = Mathf.Abs(selectedSeverity - firstSpot.severity) <= MarginOfError;

        bool isCorrect = diseaseCorrect && severityCorrect;

        // Log de resultados
        if (isCorrect)
        {
            Debug.Log($"¡CORRECTO! Enfermedad: {selectedDisease}, Severidad: {selectedSeverity}");
        }
        else
        {
            string errorMsg = "INCORRECTO. ";

            if (!diseaseCorrect)
            {
                errorMsg += $"Enfermedad: {selectedDisease} (Correcto: {firstSpot.diseaseName}). ";
            }

            if (!severityCorrect)
            {
                errorMsg += $"Severidad: {selectedSeverity} (Correcto: {firstSpot.severity})";
            }

            Debug.Log(errorMsg);
        }

        return isCorrect;
    }

    void ShowFeedback(bool isCorrect)
    {
        // Ocultar ambos objetos primero
        if (correctFeedbackObject != null)
        {
            correctFeedbackObject.SetActive(false);
        }

        if (incorrectFeedbackObject != null)
        {
            incorrectFeedbackObject.SetActive(false);
        }

        // Seleccionar el objeto apropiado
        GameObject objectToShow = isCorrect ? correctFeedbackObject : incorrectFeedbackObject;

        if (objectToShow == null)
        {
            Debug.LogWarning($"No hay objeto asignado para feedback {(isCorrect ? "correcto" : "incorrecto")}");
            return;
        }

        // Mostrar el objeto
        objectToShow.SetActive(true);

        // Ocultarlo después de un tiempo
        StartCoroutine(HideFeedbackAfterDelay(objectToShow));
    }

    IEnumerator HideFeedbackAfterDelay(GameObject feedbackObject)
    {
        yield return new WaitForSeconds(feedbackDuration);

        if (feedbackObject != null)
        {
            feedbackObject.SetActive(false);
        }
    }

    IEnumerator AdvanceToNextLevel()
    {
        // Esperar a que termine de mostrarse el feedback
        yield return new WaitForSeconds(feedbackDuration);

        // Resetear la selección
        ResetSelection();

        // Notificar al InteractionManager para que avance al siguiente nivel
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.AdvanceToNextLevel();
        }
        else
        {
            Debug.LogWarning("InteractionManager.Instance no encontrado");
        }
    }

    public void ResetSelection()
    {
        // Desactivar todos los toggles
        if (diseaseToggleGroup != null)
        {
            diseaseToggleGroup.SetAllTogglesOff();
        }

        if (severityToggleGroup != null)
        {
            severityToggleGroup.SetAllTogglesOff();
        }
    }

    void OnDestroy()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(OnSubmit);
        }
    }
}