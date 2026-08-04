using UnityEngine;

/// <summary>
/// Puente pequeno entre el tutorial y el Animator del granjero.
/// El modelo puede ser un FBX instanciado en runtime; por eso el controller se
/// asigna aqui en lugar de exigir un prefab importado adicional.
/// </summary>
public class TutorialAvatarController : MonoBehaviour
{
    private Animator _animator;

    public void Initialize(RuntimeAnimatorController controller)
    {
        _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null)
            _animator = gameObject.AddComponent<Animator>();

        if (controller != null)
            _animator.runtimeAnimatorController = controller;

        if (_animator.layerCount > 0)
            _animator.SetLayerWeight(0, 1f);
    }

    public void PlayGrab()
    {
        if (_animator == null) return;
        ResetTriggers();
        _animator.Play("Grab", 0, 0f);
    }

    public void PlayObserve()
    {
        if (_animator == null) return;
        ResetTriggers();
        _animator.SetTrigger("PlayObserve");
    }

    public void PlayDiagnosis()
    {
        if (_animator == null) return;
        ResetTriggers();
        _animator.SetTrigger("PlayDiagnosis");
    }

    public void PlayThumbsUp()
    {
        if (_animator == null) return;
        ResetTriggers();
        _animator.SetTrigger("PlayThumbsUp");
    }

    private void ResetTriggers()
    {
        _animator.ResetTrigger("PlayObserve");
        _animator.ResetTrigger("PlayDiagnosis");
        _animator.ResetTrigger("PlayThumbsUp");
    }

    /// <summary>
    /// Coloca los pies del avatar fuera del borde del plano y hacia el jugador.
    /// No consulta MRUK: cualquier sistema que entregue el mismo marco de superficie
    /// (incluida una futura mesa virtual) puede usar esta API.
    /// </summary>
    public void PlaceRelativeToSurface(
        Vector3 surfacePosition,
        Quaternion surfaceRotation,
        Vector3 surfaceScale,
        Transform player,
        float lateralGap,
        float frontGap,
        float surfaceHeightOffset,
        float facingOffsetY)
    {
        Vector3 up = surfaceRotation * Vector3.up;
        Vector3 right = surfaceRotation * Vector3.right;
        Vector3 toPlayer = player != null ? player.position - surfacePosition : surfaceRotation * Vector3.forward;
        toPlayer = Vector3.ProjectOnPlane(toPlayer, up);
        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = Vector3.ProjectOnPlane(surfaceRotation * Vector3.forward, up);
        toPlayer.Normalize();

        float side = Mathf.Sign(Vector3.Dot(toPlayer, right));
        if (Mathf.Abs(Vector3.Dot(toPlayer, right)) < 0.1f)
            side = 1f;

        float halfWidth = Mathf.Abs(surfaceScale.x) * 0.5f;
        float halfDepth = Mathf.Abs(surfaceScale.z) * 0.5f;
        if (halfDepth < 0.01f)
            halfDepth = Mathf.Abs(surfaceScale.y) * 0.5f;

        transform.rotation = Quaternion.LookRotation(toPlayer, up)
            * Quaternion.AngleAxis(facingOffsetY, up);

        GetWorldBounds(
            out float avatarHalfWidth,
            out float avatarHalfDepth,
            out _,
            out float avatarBottomOffset,
            up,
            right,
            toPlayer);

        Vector3 target = surfacePosition
            + right * side * (halfWidth + avatarHalfWidth + Mathf.Max(0f, lateralGap))
            + toPlayer * (halfDepth + avatarHalfDepth + Mathf.Max(0f, frontGap))
            // Corrige el pivote del FBX: puede estar en los pies o en el centro.
            + up * (surfaceHeightOffset - avatarBottomOffset);

        transform.position = target;
    }

    public void PlaceInFrontOfPlayer(Transform player, float distance, float height)
    {
        if (player == null)
        {
            transform.position = Vector3.up * height;
            return;
        }
        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(-forward.normalized, Vector3.up);

        GetWorldBounds(
            out _,
            out _,
            out _,
            out float avatarBottomOffset,
            Vector3.up,
            transform.right,
            transform.forward);
        transform.position = player.position
            + forward.normalized * distance
            + Vector3.up * (height - avatarBottomOffset);
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
            bottomOffset = -halfHeight;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 extents = bounds.extents;
        halfWidth = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.y) * extents.y + Mathf.Abs(right.z) * extents.z;
        halfDepth = Mathf.Abs(front.x) * extents.x + Mathf.Abs(front.y) * extents.y + Mathf.Abs(front.z) * extents.z;
        halfHeight = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
        float centerOffset = Vector3.Dot(bounds.center - transform.position, up);
        bottomOffset = centerOffset - halfHeight;
    }
}
