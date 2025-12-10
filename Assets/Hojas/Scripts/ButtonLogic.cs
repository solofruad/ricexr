using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de lógica para botones de interacción en el entorno de realidad mixta.
/// Conecta botones UI con acciones específicas en el SceneInteractionManager.
/// 
/// Funcionalidades principales:
/// 1. Vincula botones UI con acciones específicas de interacción
/// 2. Captura la posición, rotación y escala del visual asociado al botón
/// 3. Notifica al SceneInteractionManager cuando se activa un botón
/// 4. Gestiona la limpieza automática de listeners para evitar memory leaks
/// 
/// Uso típico:
/// - Colocar este componente en un GameObject que tenga un botón UI como hijo
/// - Asignar el visualTransform que representa el área/interacción
/// - Al hacer clic, se envía la posición/rotación/escala al SceneInteractionManager
/// 
/// Configuración:
/// - El botón se puede asignar manualmente o se detecta automáticamente en los hijos
/// - visualTransform debe referenciar el objeto que representa el área de interacción
/// - Requiere que SceneInteractionManager.Instance exista en la escena
/// </summary>
public class ButtonLogic : MonoBehaviour
{
    [SerializeField] private Button interactionButton;
    [SerializeField] private Transform visualTransform;

    void Start()
    {
        if (interactionButton == null)
        {
            interactionButton = GetComponentInChildren<Button>();
        }

        if (interactionButton != null)
        {
            // Conecta el botón con el manager global
            interactionButton.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            Debug.LogError("No button found in AnchorUIButton!");
        }
    }

    private void OnButtonClicked()
    {
        if (SceneInteractionManager.Instance != null)
        {
            SceneInteractionManager.Instance.OnAnchorButtonClicked(visualTransform.position, transform.rotation, visualTransform.localScale);
        }
        else
        {
            Debug.LogError("InteractionManager not found in scene");
        }
    }

    void OnDestroy()
    {
        // Limpia el listener
        if (interactionButton != null)
        {
            interactionButton.onClick.RemoveListener(OnButtonClicked);
        }
    }
}
