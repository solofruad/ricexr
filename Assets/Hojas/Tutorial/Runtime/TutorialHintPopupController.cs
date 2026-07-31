using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Optional XR popup. Wire its Entendido button to <see cref="Acknowledge"/>.</summary>
[DisallowMultipleComponent]
public class TutorialHintPopupController : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Text hintText;
    [SerializeField] private Button acknowledgeButton;

    public event Action Acknowledged;
    public bool IsConfigured => popupRoot != null;

    private void Awake()
    {
        if (popupRoot == null) popupRoot = gameObject;
        if (acknowledgeButton != null) acknowledgeButton.onClick.AddListener(Acknowledge);
        Hide();
    }

    public void Show(DiseaseSpot spot)
    {
        if (hintText != null && spot != null)
            hintText.text = $"Objetivo: {spot.diseaseName}\nSeveridad: {spot.severity}";
        if (popupRoot != null) popupRoot.SetActive(true);
    }

    public void Hide()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    public void Acknowledge()
    {
        Hide();
        Acknowledged?.Invoke();
    }

    private void OnDestroy()
    {
        if (acknowledgeButton != null) acknowledgeButton.onClick.RemoveListener(Acknowledge);
    }
}
