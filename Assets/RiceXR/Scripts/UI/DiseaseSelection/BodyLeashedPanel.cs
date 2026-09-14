using UnityEngine;

[DisallowMultipleComponent]
public sealed class BodyLeashedPanel : MonoBehaviour
{
    [SerializeField] private Vector3 bodyOffset = new Vector3(0.3f, -0.18f, 0.6f);
    [SerializeField, Min(0f)] private float positionDeadZone = 0.12f;
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.22f;
    [SerializeField, Range(0f, 180f)] private float yawDeadZone = 18f;
    [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.25f;
    [SerializeField, Min(0f)] private float trackingJumpDistance = 1.5f;

    public bool IsAttached => _attached;

    private bool _attached;
    private bool _warnedMissingCamera;
    private GrabbableLeafListener.SelectionHand _hand;
    private Camera _camera;
    private Vector3 _anchorHeadPosition;
    private Vector3 _lastHeadPosition;
    private Vector3 _positionVelocity;
    private float _bodyYaw;
    private float _targetBodyYaw;
    private float _bodyYawVelocity;
    private float _rotationVelocity;

    public void Attach(GrabbableLeafListener.SelectionHand hand)
    {
        _hand = hand;
        _attached = true;
        ResetVelocities();

        Transform head = ResolveHead();
        if (head != null)
            SnapTo(head);
    }

    public void Detach()
    {
        _attached = false;
        ResetVelocities();
    }

    private void LateUpdate()
    {
        if (!_attached) return;

        Transform head = ResolveHead();
        if (head == null) return;

        Vector3 headPosition = head.position;
        if (trackingJumpDistance > 0f
            && Vector3.Distance(headPosition, _lastHeadPosition) > trackingJumpDistance)
        {
            SnapTo(head);
            return;
        }

        _lastHeadPosition = headPosition;

        if (Vector3.Distance(headPosition, _anchorHeadPosition) > positionDeadZone)
            _anchorHeadPosition = headPosition;

        float headYaw = head.eulerAngles.y;
        if (Mathf.Abs(Mathf.DeltaAngle(_targetBodyYaw, headYaw)) > yawDeadZone)
            _targetBodyYaw = headYaw;

        _bodyYaw = Mathf.SmoothDampAngle(
            _bodyYaw,
            _targetBodyYaw,
            ref _bodyYawVelocity,
            rotationSmoothTime);

        Vector3 targetPosition = ResolvePosition(_anchorHeadPosition, _bodyYaw);
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref _positionVelocity,
            positionSmoothTime);

        ApplyRotation(headPosition, false);
    }

    private void SnapTo(Transform head)
    {
        _anchorHeadPosition = head.position;
        _lastHeadPosition = head.position;
        _bodyYaw = head.eulerAngles.y;
        _targetBodyYaw = _bodyYaw;
        transform.position = ResolvePosition(_anchorHeadPosition, _bodyYaw);
        ApplyRotation(head.position, true);
        ResetVelocities();
    }

    private Vector3 ResolvePosition(Vector3 headPosition, float yaw)
    {
        float side = _hand == GrabbableLeafListener.SelectionHand.Left ? 1f : -1f;
        Vector3 offset = new Vector3(
            Mathf.Abs(bodyOffset.x) * side,
            bodyOffset.y,
            bodyOffset.z);
        return headPosition + Quaternion.Euler(0f, yaw, 0f) * offset;
    }

    private void ApplyRotation(Vector3 headPosition, bool instant)
    {
        Vector3 facing = transform.position - headPosition;
        facing.y = 0f;
        if (facing.sqrMagnitude <= 0.0001f) return;

        float targetYaw = Quaternion.LookRotation(facing, Vector3.up).eulerAngles.y;
        float yaw = instant
            ? targetYaw
            : Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetYaw,
                ref _rotationVelocity,
                rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private Transform ResolveHead()
    {
        if (_camera != null && _camera.isActiveAndEnabled)
            return _camera.transform;

        _camera = Camera.main;
        if (_camera != null)
            return _camera.transform;

        if (!_warnedMissingCamera)
        {
            _warnedMissingCamera = true;
            Debug.LogWarning("[BodyLeashedPanel] No se encontro Camera.main.", this);
        }

        return null;
    }

    private void ResetVelocities()
    {
        _positionVelocity = Vector3.zero;
        _bodyYawVelocity = 0f;
        _rotationVelocity = 0f;
    }
}
