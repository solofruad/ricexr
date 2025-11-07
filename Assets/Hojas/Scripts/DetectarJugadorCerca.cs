using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectarJugadorCerca : MonoBehaviour
{
    [Header("Detección del Player")]
    [Tooltip("Layer asignado al jugador")]
    [SerializeField] private LayerMask playerMask;
    [Tooltip("Tag del jugador (opcional)")]
    [SerializeField] private string playerTag = "Player";

    [Header("UI hijo a mostrar/ocultar")]
    [SerializeField] private GameObject uiRoot;

    private void Awake()
    {
        // Si no se asignó, busca automáticamente un Canvas hijo
        if (uiRoot == null)
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null) uiRoot = canvas.gameObject;
        }

        // Asegura que la UI empiece oculta
        SetUIVisible(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other.gameObject))
            SetUIVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other.gameObject))
            SetUIVisible(false);
    }

    private bool IsPlayer(GameObject obj)
    {
        // Revisa layer y/o tag
        bool layerOk = (playerMask.value & (1 << obj.layer)) != 0;
        bool tagOk = string.IsNullOrEmpty(playerTag) || obj.CompareTag(playerTag);
        return layerOk && tagOk;
    }

    private void SetUIVisible(bool visible)
    {
        if (uiRoot != null)
            uiRoot.SetActive(visible);
    }

    private void OnDisable()
    {
        SetUIVisible(false);
    }
}
