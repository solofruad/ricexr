using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


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
    [SerializeField] private GameObject infoMessage;
    
    [Header("Objeto hija a escalar")]
    [SerializeField] private GameObject visualTransform;
    // Referencias para controlar el tamaño del UI Toolkit sin escalar el GameObject (que puede causar problemas de renderizado)
    [SerializeField] private GameObject surface;
    [SerializeField] private UIDocument uiDocument;

    // Tamaño base en píxeles que pusiste en tu UXML/USS
    private const float BASE_WIDTH = 100f;
    private const float BASE_HEIGHT = 100f;

    private void Awake()
    {
        if (infoMessage == null)
        {
            Debug.LogWarning("MostrarUI: No se ha asignado infoMessage en el inspector.");
        }

        SetUIVisible(false);
    }

    private void Start()
    {
        // Guardamos la escala que pusiste en el editor antes de resetear el root
        Vector3 size = transform.localScale;
        transform.localScale = Vector3.one;

        BoxCollider box = GetComponent<BoxCollider>();
        Transform visuals = visualTransform.transform;

        if (box != null && visuals != null)
        {
            // El plano 3D (Mesh) sí se puede escalar no-uniformemente sin problemas
            visuals.localScale = new Vector3(size.x, size.z, 1f);
            surface.transform.localScale = new Vector3(size.x, size.z, 1f);
            box.size = new Vector3(size.x + 1f, 0.2f, size.z + 1f);

            // En lugar de escalar el GameObject uiRoot, cambiamos el tamaño del Layout en píxeles.
            // Multiplicamos el tamaño base por la escala para que crezca perfectamente de forma limpia.
            uiDocument.worldSpaceSize = new Vector2(BASE_WIDTH * size.x, BASE_HEIGHT * size.z);
        }
        else
        {
            Debug.LogWarning("DetectarJugadorCerca: No se encontro BoxCollider o visuales como hijo.");
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
        if (infoMessage != null)
            infoMessage.SetActive(visible);
    }

    private void OnDisable()
    {
        SetUIVisible(false);
    }
}
