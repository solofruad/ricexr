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
    [Header("Maya hija a escalar")]
    [SerializeField] private GameObject visual;

    private void Awake()
    {
        if (uiRoot == null)
        {
            Debug.LogWarning("MostrarUI: No se ha asignado uiRoot en el inspector.");
        }

        SetUIVisible(false);
    }


    private void Start()
    {
        // Lo que estoy haciendo aca es simplemente escalar las visuales, el plano de las visuales,
        // haciendo que los demas componentes no se vean afectados por la escala

        Vector3 size = transform.localScale;
        transform.localScale = Vector3.one;

        BoxCollider box = GetComponent<BoxCollider>();
        Transform visuals = visual.transform;
        if (box != null && visuals != null)
        {
            visuals.localScale = new Vector3(size.x, size.z, 1);
            box.size = new Vector3(size.x+1, 0.2f, size.z+1);
        }
        else
        {
            Debug.LogWarning("DetectarJugadorCerca: No se encontró BoxCollider o visuales como hijo.");
        }
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
