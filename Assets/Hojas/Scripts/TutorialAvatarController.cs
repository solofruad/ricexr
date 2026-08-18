using UnityEngine;

/// <summary>
/// Puente pequeno entre el tutorial y el Animator del granjero.
/// Coloca al avatar al otro lado de la mesa respecto al jugador y dispara
/// las animaciones del AnimationController por nombre de estado.
/// </summary>
public class TutorialAvatarController : MonoBehaviour
{
    private static readonly int GrabState = Animator.StringToHash("Grab");
    private static readonly int ObserveState = Animator.StringToHash("Observe");
    private static readonly int DiagnosisState = Animator.StringToHash("Diagnosis");
    private static readonly int ThumbsUpState = Animator.StringToHash("ThumbsUp");

    private Animator _animator;
    private Vector3 _plantedPosition;
    private Quaternion _plantedRotation;
    private bool _isPlanted;

    public void Initialize(RuntimeAnimatorController controller)
    {
        _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null)
            _animator = GetComponentInParent<Animator>(true);
        if (_animator == null)
            _animator = gameObject.AddComponent<Animator>();

        if (controller != null)
            _animator.runtimeAnimatorController = controller;

        // Los clips son humanoides de musculos; no deben desplazar al root.
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.enabled = true;

        if (_animator.layerCount > 0)
            _animator.SetLayerWeight(0, 1f);

        _animator.Rebind();
        _animator.Update(0f);
    }

    private void LateUpdate()
    {
        // Por si algun clip reintroduce root motion, mantenemos la pose plantada.
        if (!_isPlanted || _animator == null || !_animator.enabled) return;
        if (_animator.deltaPosition == Vector3.zero && _animator.deltaRotation == Quaternion.identity) return;
        transform.SetPositionAndRotation(_plantedPosition, _plantedRotation);
    }

    public void PlayGrab() => PlayState(GrabState, "Grab");

    public void PlayObserve() => PlayState(ObserveState, "Observe");

    public void PlayDiagnosis() => PlayState(DiagnosisState, "Diagnosis");

    public void PlayThumbsUp() => PlayState(ThumbsUpState, "ThumbsUp");

    private void PlayState(int stateHash, string stateName)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;

        _animator.enabled = true;
        if (!_animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"[TutorialAvatar] Estado '{stateName}' no encontrado en el AnimatorController.");
            return;
        }

        // Play fuerza el estado al instante (ignora HasExitTime de las transiciones).
        _animator.Play(stateHash, 0, 0f);
        _animator.Update(0f);
    }

    /// <summary>
    /// Coloca al avatar al otro lado de la superficie respecto al jugador,
    /// mirando al jugador con la mesa en medio (J - M - A).
    /// </summary>
    public void PlaceRelativeToSurface(
        Vector3 surfacePosition,
        Quaternion surfaceRotation,
        Vector3 surfaceScale,
        Transform player,
        float frontGap,
        float surfaceHeightOffset,
        float facingOffsetY)
    {
        Vector3 up = surfaceRotation * Vector3.up;
        if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
        up.Normalize();

        Vector3 right = surfaceRotation * Vector3.right;
        right = Vector3.ProjectOnPlane(right, up);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.Cross(up, Vector3.forward);
        right.Normalize();

        // Direccion horizontal desde el centro de la mesa hacia el jugador.
        Vector3 toPlayer = player != null ? player.position - surfacePosition : surfaceRotation * Vector3.forward;
        toPlayer = Vector3.ProjectOnPlane(toPlayer, up);
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = Vector3.ProjectOnPlane(surfaceRotation * Vector3.forward, up);
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = Vector3.Cross(up, right);
        toPlayer.Normalize();

        // Lado opuesto al jugador: mesa en medio.
        Vector3 awayFromPlayer = -toPlayer;

        float halfDepth = Mathf.Abs(surfaceScale.z) * 0.5f;
        if (halfDepth < 0.01f)
            halfDepth = Mathf.Abs(surfaceScale.x) * 0.5f;
        if (halfDepth < 0.01f)
            halfDepth = Mathf.Abs(surfaceScale.y) * 0.5f;

        // Mira al jugador a traves de la mesa.
        Quaternion facing = Quaternion.LookRotation(toPlayer, up)
            * Quaternion.AngleAxis(facingOffsetY, up);
        transform.rotation = facing;

        GetWorldBounds(
            out _,
            out float avatarHalfDepth,
            out _,
            out float avatarBottomOffset,
            up,
            right,
            awayFromPlayer);

        Vector3 target = surfacePosition
            + awayFromPlayer * (halfDepth + avatarHalfDepth + Mathf.Max(0f, frontGap))
            + up * (surfaceHeightOffset - avatarBottomOffset);

        Plant(target, facing);
    }

    public void PlaceInFrontOfPlayer(Transform player, float distance, float height)
    {
        if (player == null)
        {
            Plant(Vector3.up * height, Quaternion.identity);
            return;
        }

        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Quaternion facing = Quaternion.LookRotation(-forward, Vector3.up);
        transform.rotation = facing;

        GetWorldBounds(
            out _,
            out _,
            out _,
            out float avatarBottomOffset,
            Vector3.up,
            transform.right,
            transform.forward);

        Vector3 target = player.position
            + forward * distance
            + Vector3.up * (height - avatarBottomOffset);

        Plant(target, facing);
    }

    private void Plant(Vector3 position, Quaternion rotation)
    {
        _plantedPosition = position;
        _plantedRotation = rotation;
        _isPlanted = true;
        transform.SetPositionAndRotation(position, rotation);
    }

    private void GetWorldBounds(
        out float halfWidth,
        out float halfDepth,
        out float halfHeight,
        out float bottomOffset,
        Vector3 up,
        Vector3 right,
        Vector3 front)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            halfWidth = halfDepth = halfHeight = 0.25f;
            bottomOffset = 0f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 extents = bounds.extents;
        halfWidth = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.y) * extents.y + Mathf.Abs(right.z) * extents.z;
        halfDepth = Mathf.Abs(front.x) * extents.x + Mathf.Abs(front.y) * extents.y + Mathf.Abs(front.z) * extents.z;
        halfHeight = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
        float centerOffset = Vector3.Dot(bounds.center - transform.position, up);
        bottomOffset = centerOffset - halfHeight;
    }
}
