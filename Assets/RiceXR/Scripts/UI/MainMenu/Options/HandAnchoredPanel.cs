using UnityEngine;

/// <summary>
/// PANEL ANCLADO A LA MANO
///
/// Hace que un panel de mundo siga a la mano del jugador mientras esta prendido, y lo
/// devuelve a su sitio original al soltarlo. Es la pieza que permite reutilizar el panel
/// de opciones del menu principal durante la partida: en el menu vive en un punto fijo
/// del cuarto, colgado del lado del menu, y en partida tiene que aparecer donde este el
/// jugador, que para entonces se movio a la mesa.
///
/// SEGUIMIENTO PEREZOSO (lo importante):
/// El panel no persigue la mano frame a frame. El destino solo se recalcula cuando la
/// mano se aleja mas de 'deadZone' del destino anterior, y aun entonces el panel llega
/// amortiguado. Sin esto, apuntar con el rayo de la otra mano a un panel que copia cada
/// temblor de la muñeca seria imposible.
///
/// Va en el mismo GameObject que el panel, es decir el que se desactiva al cerrarlo, asi
/// que deja de correr solo cuando el panel no esta visible. No conoce a OptionsController:
/// solo sabe seguir una mano y volver a su sitio.
/// </summary>
[DisallowMultipleComponent]
public class HandAnchoredPanel : MonoBehaviour
{
    [Header("Anclaje")]
    [Tooltip("Anclas candidatas, en orden de preferencia. Se usa la primera que este activa en " +
             "la jerarquia. Vacio = no se ancla nunca y el panel se queda donde lo dejo el prefab. " +
             "Hacen falta dos: el rig apaga el visual de la mano cuando se usan mandos y al reves " +
             "(OVRManager tiene SimultaneousHandsAndControllers desactivado), asi que un solo " +
             "Transform se quedaria congelado en uno de los dos modos. Poner el ancla de la mano " +
             "izquierda primero y la del mando izquierdo despues.")]
    [SerializeField] private Transform[] handAnchors = new Transform[0];

    [Tooltip("Metros por encima de la mano.")]
    [SerializeField] private float heightOffset = 0.22f;

    [Tooltip("Metros hacia la cabeza del jugador, para que el panel no quede dentro de la mano.")]
    [SerializeField] private float towardPlayerOffset = 0.05f;

    [Header("Seguimiento")]
    [Tooltip("Distancia que tiene que recorrer la mano antes de que el panel la siga.")]
    [SerializeField] private float deadZone = 0.12f;

    [Tooltip("Tiempo de amortiguacion del movimiento. Mas alto = mas perezoso.")]
    [SerializeField] private float positionSmoothTime = 0.18f;

    [Tooltip("Velocidad de giro hacia la cabeza del jugador.")]
    [SerializeField] private float rotationSmoothSpeed = 8f;

    /// <summary>Indica si el panel esta siguiendo a la mano ahora mismo.</summary>
    public bool IsAttached => _attached;

    private bool _attached;

    // El sitio original se guarda una sola vez en la vida del componente: si se guardara
    // en cada Attach, un Attach sobre un panel ya anclado perderia el sitio del prefab.
    private bool _originalSaved;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;

    private Vector3 _target;
    private Vector3 _followVelocity;
    private Transform _cameraTransform;

    /// <summary>
    /// Primera ancla activa en la jerarquia. Es lo que hace que el panel siga a la mano cuando
    /// se juega con hand tracking y al mando cuando se juega con mandos, sin que este componente
    /// tenga que saber nada del SDK: el propio rig activa y desactiva los visuales.
    /// </summary>
    private Transform ActiveAnchor
    {
        get
        {
            if (handAnchors == null) return null;

            foreach (Transform anchor in handAnchors)
            {
                if (anchor != null && anchor.gameObject.activeInHierarchy)
                    return anchor;
            }

            return null;
        }
    }

    // ─── API publica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Engancha el panel a la mano y lo coloca ahi de inmediato. El salto es
    /// deliberado: el panel viene del sitio del menu principal, que puede estar al
    /// otro lado del cuarto, y verlo cruzar volando no aporta nada.
    /// </summary>
    public void Attach()
    {
        SaveOriginalOnce();

        Transform anchor = ActiveAnchor;
        if (anchor == null)
        {
            Debug.LogWarning($"[HandAnchoredPanel] {name}: no hay ninguna ancla activa cableada; " +
                             "el panel se queda donde estaba.", this);
            return;
        }

        _attached = true;
        _followVelocity = Vector3.zero;
        _target = ResolveTargetPosition(anchor);

        transform.position = _target;
        ApplyRotation(instant: true);
    }

    /// <summary>
    /// Suelta el panel y lo devuelve al sitio que tenia en el prefab. Hay que llamarlo
    /// ANTES de desactivar el GameObject, o el panel se quedaria guardado en la mano.
    /// </summary>
    public void Detach()
    {
        if (!_attached) return;

        _attached = false;
        RestoreOriginal();
    }

    // ─── Ciclo de vida ────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (!_attached) return;

        // Se relee cada frame: si el jugador suelta los mandos y pasa a manos con el panel
        // abierto, el rig cambia de visual y el panel tiene que seguir al nuevo.
        Transform anchor = ActiveAnchor;
        if (anchor == null) return;

        Vector3 desired = ResolveTargetPosition(anchor);

        // Zona muerta: mientras la mano se mueva poco, el destino no cambia y el panel
        // termina de asentarse en vez de vibrar detras de ella.
        if ((desired - _target).sqrMagnitude > deadZone * deadZone)
            _target = desired;

        transform.position = Vector3.SmoothDamp(
            transform.position, _target, ref _followVelocity, positionSmoothTime);

        ApplyRotation(instant: false);
    }

    // Red de seguridad: si alguien desactiva el GameObject sin pasar por Detach, el panel
    // volveria a aparecer en el menu principal flotando donde quedo la mano.
    private void OnDisable()
    {
        if (!_attached) return;

        _attached = false;
        RestoreOriginal();
    }

    // ─── Colocacion ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sobre la mano y un poco hacia el jugador. El offset se calcula en ejes del mundo
    /// y no de la mano a proposito: anclado a la mano, cualquier giro de muñeca haria
    /// orbitar el panel y no habria forma de apuntarle.
    /// </summary>
    private Vector3 ResolveTargetPosition(Transform anchor)
    {
        Vector3 position = anchor.position + Vector3.up * heightOffset;

        Transform cam = ResolveCamera();
        if (cam == null) return position;

        Vector3 towardHead = cam.position - anchor.position;
        towardHead.y = 0f;
        if (towardHead.sqrMagnitude > 0.0001f)
            position += towardHead.normalized * towardPlayerOffset;

        return position;
    }

    /// <summary>
    /// Misma formula de Y-lock que BillboardUI: el panel mira a la cabeza pivotando solo
    /// sobre Y, para que el texto nunca quede inclinado.
    /// </summary>
    private void ApplyRotation(bool instant)
    {
        Transform cam = ResolveCamera();
        if (cam == null) return;

        Vector3 toHead = cam.position - transform.position;
        toHead.y = 0f;
        if (toHead.sqrMagnitude <= 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(-toHead, Vector3.up);
        transform.rotation = instant
            ? target
            : Quaternion.Slerp(transform.rotation, target, rotationSmoothSpeed * Time.deltaTime);
    }

    // ─── Sitio original ───────────────────────────────────────────────────────

    private void SaveOriginalOnce()
    {
        if (_originalSaved) return;

        _originalLocalPosition = transform.localPosition;
        _originalLocalRotation = transform.localRotation;
        _originalSaved = true;
    }

    private void RestoreOriginal()
    {
        if (!_originalSaved) return;

        transform.localPosition = _originalLocalPosition;
        transform.localRotation = _originalLocalRotation;
    }

    // El GameObject arranca desactivado, asi que Start no corre hasta la primera apertura:
    // la camara se resuelve de forma perezosa en vez de cachearse en Start.
    private Transform ResolveCamera()
    {
        if (_cameraTransform != null) return _cameraTransform;

        Camera cam = Camera.main;
        if (cam != null) _cameraTransform = cam.transform;
        return _cameraTransform;
    }
}
