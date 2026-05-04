using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// CLASE ASOCIADA A LOS PLANOS AZULES CON OLAS
/// Clase que detecta la proximidad del jugador y controla la visibilidad de elementos UI/visuales.
/// 
/// Funcionalidades principales:
/// 1. Detecta cuando el jugador entra/sale de un area de activacion de los planos azules con olas definida por un BoxCollider
/// 2. Muestra/oculta elementos de UI cuando el jugador esta cerca
/// 3. Ajusta automaticamente la escala de elementos visuales para mantener proporciones correctas
/// 4. Maneja multiples metodos de identificacion del jugador (LayerMask y Tag)
/// 5. Asegura que la UI se oculte cuando el objeto se deshabilita
/// 
/// Configuracion requerida:
/// - El GameObject debe tener un BoxCollider con "Is Trigger" habilitado
/// - Debe tener un GameObject hijo con elementos UI a mostrar/ocultar
/// - Debe tener un GameObject hijo con elementos visuales a escalar
/// - El jugador debe tener configurado el Layer y/o Tag especificado
/// 
/// Flujo de trabajo:
/// - En Start(): Ajusta la escala de los elementos visuales y el BoxCollider
/// - Cuando el jugador entra: Activa la UI y cualquier animacion relacionada
/// - Cuando el jugador sale: Desactiva la UI
/// - Si el objeto se deshabilita: Desactiva la UI automaticamente
/// </summary>
public class PlayerProximityDetector : MonoBehaviour
{
    [Header("Deteccion del Player")]
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
            Debug.LogWarning("DetectarJugadorCerca: No se encontr� BoxCollider o visuales como hijo.");
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
