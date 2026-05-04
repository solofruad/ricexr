using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de logica para botones de interaccion en el entorno de realidad mixta.
/// Conecta botones UI con el GameEventBus publicando PlaneSelected.
/// 
/// Funcionalidades principales:
/// 1. Vincula botones UI con acciones especificas de interaccion
/// 2. Captura la posicion, rotacion y escala del visual asociado al boton
/// 3. Publica PlaneSelected al GameEventBus cuando se activa un boton
/// 4. Gestiona la limpieza automatica de listeners para evitar memory leaks
/// 
/// Uso tipico:
/// - Colocar este componente en un GameObject que tenga un boton UI como hijo
/// - Asignar el visualTransform que representa el area/interaccion
/// - Al hacer clic, publica posicion/rotacion/escala al bus de eventos
/// 
/// Configuracion:
/// - El boton se puede asignar manualmente o se detecta automaticamente en los hijos
/// - visualTransform debe referenciar el objeto que representa el area de interaccion
/// </summary>
public class ButtonPlaneLogic : MonoBehaviour
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
            // Conecta el boton con el bus de eventos
            interactionButton.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            Debug.LogError("No se encontró un Button en los hijos de " + gameObject.name);
        }
    }

    private void OnButtonClicked()
    {
        if (visualTransform == null)
        {
            Debug.LogError("visualTransform no asignado en " + gameObject.name);
            return;
        }

        // Publicar al bus — GameFlowController escucha esto
        GameEventBus.PublishPlaneSelected(
            visualTransform.position,
            transform.rotation,
            visualTransform.localScale
        );
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
