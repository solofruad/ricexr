using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de logica para botones de interaccion en el entorno de realidad mixta.
/// Conecta botones UI con acciones especificas en el SceneInteractionManager.
/// 
/// Funcionalidades principales:
/// 1. Vincula botones UI con acciones especificas de interaccion
/// 2. Captura la posicion, rotacion y escala del visual asociado al boton
/// 3. Notifica al SceneInteractionManager cuando se activa un boton
/// 4. Gestiona la limpieza automatica de listeners para evitar memory leaks
/// 
/// Uso tpico:
/// - Colocar este componente en un GameObject que tenga un botn UI como hijo
/// - Asignar el visualTransform que representa elrea/interaccin
/// - Al hacer clic, se enva la posicin/rotacin/escala al SceneInteractionManager
/// 
/// Configuracion:
/// - El boton se puede asignar manualmente o se detecta automaticamente en los hijos
/// - visualTransform debe referenciar el objeto que representa elrea de interaccin
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
            // Conecta el bot�n con el manager global
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
