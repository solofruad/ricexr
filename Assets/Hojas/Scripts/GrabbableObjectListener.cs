using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrabbableObjectListener : MonoBehaviour
{
    /// <summary>
    /// Sistema de monitoreo centralizado para objetos agarrables (grabbables) en la escena.
    /// Implementa el patrón Singleton para proporcionar acceso global al estado de agarre actual.
    /// 
    /// Funcionalidades principales:
    /// 1. Proporciona un punto de acceso global al objeto Leaf actualmente agarrado
    /// 2. Notifica cuando cambia el objeto agarrado mediante un setter con lógica adicional
    /// 3. Mantiene una referencia persistente durante toda la sesión
    /// 
    /// Uso típico:
    /// - Los objetos Leaf notifican a este listener cuando son agarrados/soltados
    /// - Otros sistemas (como DiseaseSelectionSystem) consultan ActualLeafGrabbed
    /// - Permite la coordinación entre diferentes componentes que necesitan saber qué hoja está activa
    /// 
    /// Flujo de trabajo:
    /// 1. Cuando un usuario agarra una hoja, se asigna a ActualLeafGrabbed
    /// 2. Cuando se suelta o se agarra otra hoja, se actualiza la referencia
    /// 3. Sistemas externos pueden suscribirse o consultar el estado actual
    /// </summary>
    public static GrabbableObjectListener Instance { get; private set; }

    private Leaf _previousLeafGrabbed = null;
    private Leaf _actualLeafGrabbed = null;
    [HideInInspector]
    public Leaf ActualLeafGrabbed {
        get
        {
            return _actualLeafGrabbed;
        }
        set { 
            if (value == null)
            {
                if (_previousLeafGrabbed != null)
                {
                    _actualLeafGrabbed = _previousLeafGrabbed;
                    _previousLeafGrabbed = null;
                }
                else { 
                    _actualLeafGrabbed = null;
                }
                return;
            }

            if (_actualLeafGrabbed != value)
            {
                if (_actualLeafGrabbed != null)
                {
                    _previousLeafGrabbed = _actualLeafGrabbed;
                }
                _actualLeafGrabbed = value;
            }
        } 
    }


    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

}
