using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnAnchorButtonClicked(visualTransform.position, transform.rotation, visualTransform.localScale);
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
