using System.Collections;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Controla los estados fisicos de una hoja.
///
/// Una hoja permanece cinemática mientras reposa o se agarra. Al soltarse,
/// espera a que el SDK de Meta calcule la velocidad de lanzamiento y despues
/// la convierte en un Rigidbody dinamico con gravedad.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Grabbable))]
public sealed class LeafPhysicsController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private Grabbable _grabbable;

    private ThrowWhenUnselected _throwWhenUnselected;
    private Coroutine _wireThrowRoutine;
    private Coroutine _releaseFallbackRoutine;
    private bool _throwSubscribed;
    private bool _releaseHandled;
    private bool _warnedMissingReferences;

    private void Reset()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grabbable = GetComponent<Grabbable>();
    }

    private void Awake()
    {
        EnsureReferences();
        EnterRestingState();
    }

    private void OnEnable()
    {
        EnsureReferences();

        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised += HandlePointerEvent;

        if (!_throwSubscribed && _wireThrowRoutine == null)
            _wireThrowRoutine = StartCoroutine(ConnectToThrowSystem());
    }

    private void OnDisable()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised -= HandlePointerEvent;

        UnsubscribeFromThrowSystem();

        if (_wireThrowRoutine != null)
        {
            StopCoroutine(_wireThrowRoutine);
            _wireThrowRoutine = null;
        }

        if (_releaseFallbackRoutine != null)
        {
            StopCoroutine(_releaseFallbackRoutine);
            _releaseFallbackRoutine = null;
        }
    }

    private IEnumerator ConnectToThrowSystem()
    {
        const int maxAttempts = 120;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!isActiveAndEnabled)
                yield break;

            if (_grabbable != null && _grabbable.VelocityThrow != null)
            {
                _throwWhenUnselected = _grabbable.VelocityThrow;
                _throwWhenUnselected.WhenThrown += HandleThrown;
                _throwSubscribed = true;
                _wireThrowRoutine = null;
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            $"[LeafPhysics] No se pudo conectar el sistema de lanzamiento en {name}. " +
            "Comprueba que Grabbable tenga Rigidbody asignado y Throw When Unselected activo.",
            this);
        _wireThrowRoutine = null;
    }

    private void HandlePointerEvent(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:
                StopReleaseFallback();
                _releaseHandled = false;
                EnterSelectedState();
                break;

            case PointerEventType.Cancel:
                StopReleaseFallback();
                _releaseHandled = true;
                EnterRestingState();
                break;

            case PointerEventType.Unselect:
                // Si queda otra mano seleccionando, aun no se ha soltado del todo.
                if (_grabbable != null && _grabbable.SelectingPointsCount > 0)
                    break;

                _releaseHandled = false;
                StartReleaseFallback();
                break;
        }
    }

    private void HandleThrown(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (_grabbable != null && _grabbable.SelectingPointsCount > 0)
            return;

        StopReleaseFallback();
        ReleaseWithVelocity(linearVelocity, angularVelocity);
    }

    private void StartReleaseFallback()
    {
        StopReleaseFallback();
        _releaseFallbackRoutine = StartCoroutine(ReleaseFallbackNextFrame());
    }

    private IEnumerator ReleaseFallbackNextFrame()
    {
        yield return null;
        _releaseFallbackRoutine = null;

        if (_releaseHandled)
            yield break;

        if (_grabbable != null && _grabbable.SelectingPointsCount > 0)
            yield break;

        Debug.LogWarning(
            $"[LeafPhysics] La hoja {name} se libero sin recibir velocidad del SDK; " +
            "se libera con velocidad cero.",
            this);
        ReleaseWithVelocity(Vector3.zero, Vector3.zero);
    }

    private void ReleaseWithVelocity(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (_rigidbody == null)
            return;

        _releaseHandled = true;

        // Primero se vuelve dinamico y despues se asignan las velocidades.
        // Asi Unity no descarta la velocidad por estar el Rigidbody cinemático.
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.linearVelocity = linearVelocity;
        _rigidbody.angularVelocity = angularVelocity;
    }

    private void EnterSelectedState()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = true;
    }

    private void EnterRestingState()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = true;
    }

    private void EnsureReferences()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_grabbable == null)
            _grabbable = GetComponent<Grabbable>();

        if ((_rigidbody == null || _grabbable == null) && !_warnedMissingReferences)
        {
            _warnedMissingReferences = true;
            Debug.LogError(
                $"[LeafPhysics] Faltan referencias de Rigidbody o Grabbable en {name}.",
                this);
        }
    }

    private void StopReleaseFallback()
    {
        if (_releaseFallbackRoutine == null)
            return;

        StopCoroutine(_releaseFallbackRoutine);
        _releaseFallbackRoutine = null;
    }

    private void UnsubscribeFromThrowSystem()
    {
        if (!_throwSubscribed || _throwWhenUnselected == null)
            return;

        _throwWhenUnselected.WhenThrown -= HandleThrown;
        _throwWhenUnselected = null;
        _throwSubscribed = false;
    }
}
