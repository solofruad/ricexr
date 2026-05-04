using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GrabbableObjectListener : MonoBehaviour
{
    public static event Action<Leaf, SelectionHand, Transform> SelectionUpdated;
    public static event Action<Leaf> SelectionCleared;

    public enum SelectionHand
    {
        Unknown = 0,
        Left = 1,
        Right = 2
    }

    /// <summary>
    /// Sistema de monitoreo centralizado para objetos agarrables (grabbables) en la escena.
    /// Implementa el patron Singleton para proporcionar acceso global al estado de agarre actual.
    /// 
    /// Funcionalidades principales:
    /// 1. Proporciona un punto de acceso global al objeto Leaf actualmente agarrado
    /// 2. Notifica cuando cambia el objeto agarrado mediante un setter con logica adicional
    /// 3. Mantiene una referencia persistente durante toda la sesion
    /// 
    /// Uso tipico:
    /// - Los objetos Leaf notifican a este listener cuando son agarrados/soltados
    /// - Otros sistemas (como DiseaseSelectionSystem) consultan ActualLeafGrabbed
    /// - Permite la coordinacion entre diferentes componentes que necesitan saber que hoja este activa
    /// 
    /// Flujo de trabajo:
    /// 1. Cuando un usuario agarra una hoja, se asigna a ActualLeafGrabbed
    /// 2. Cuando se suelta o se agarra otra hoja, se actualiza la referencia
    /// 3. Sistemas externos pueden suscribirse o consultar el estado actual
    /// </summary>
    public static GrabbableObjectListener Instance { get; private set; }

    private Leaf _actualLeafGrabbed = null;
    private SelectionHand _activeSelectionHand = SelectionHand.Unknown;
    private Transform _activeSelectionAnchor = null;

    [HideInInspector]
    public Leaf ActualLeafGrabbed => _actualLeafGrabbed;

    [HideInInspector]
    public SelectionHand ActiveSelectionHand => _activeSelectionHand;

    [HideInInspector]
    public Transform ActiveSelectionAnchor => _activeSelectionAnchor;

    public void SetActiveSelection(Leaf leaf, SelectionHand hand, Transform anchor)
    {
        _actualLeafGrabbed = leaf;
        _activeSelectionHand = hand;
        _activeSelectionAnchor = anchor != null ? anchor : leaf != null ? leaf.transform : null;

        SelectionUpdated?.Invoke(_actualLeafGrabbed, _activeSelectionHand, _activeSelectionAnchor);
    }

    public void ClearActiveSelection(Leaf leaf)
    {
        if (_actualLeafGrabbed != leaf)
            return;

        Leaf releasedLeaf = _actualLeafGrabbed;
        _actualLeafGrabbed = null;
        _activeSelectionHand = SelectionHand.Unknown;
        _activeSelectionAnchor = null;

        SelectionCleared?.Invoke(releasedLeaf);
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
