using UnityEngine;

namespace SystemicOverload.Phase1
{
    /// <summary>
    /// CharacterController 캡슐 형태를 기반으로 대시 경로를 사전 검사합니다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(InputProvider))]
    public sealed class TpsDashCollisionComponent : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private float dashDistance = 4.0f;
        [SerializeField] private float dashCooldown = 0.8f;
        [SerializeField] private float safetyDistance = 0.1f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private bool useCameraForward = true;

        private CharacterController characterController;
        private InputProvider inputProvider;
        private float nextAllowedDashTime;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputProvider = GetComponent<InputProvider>();
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void OnValidate()
        {
            dashDistance = Mathf.Max(0.1f, dashDistance);
            dashCooldown = Mathf.Max(0.0f, dashCooldown);
            safetyDistance = Mathf.Max(0.0f, safetyDistance);
        }

        private void Update()
        {
            if (!inputProvider.WasDashPressedThisFrame)
            {
                return;
            }

            if (Time.time < nextAllowedDashTime)
            {
                return;
            }

            nextAllowedDashTime = Time.time + dashCooldown;
            PerformDash();
        }

        private void PerformDash()
        {
            Vector3 dashDirection = ResolveDashDirection();
            if (dashDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            dashDirection.Normalize();
            GetCapsulePoints(out Vector3 capsulePointTop, out Vector3 capsulePointBottom, out float capsuleRadius);

            float moveDistance = dashDistance;
            if (Physics.CapsuleCast(
                    capsulePointTop,
                    capsulePointBottom,
                    capsuleRadius,
                    dashDirection,
                    out RaycastHit hitInfo,
                    dashDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                moveDistance = Mathf.Max(0.0f, hitInfo.distance - safetyDistance);
                Debug.DrawRay(transform.position, dashDirection * hitInfo.distance, Color.red, 0.5f);
            }
            else
            {
                Debug.DrawRay(transform.position, dashDirection * dashDistance, Color.green, 0.5f);
            }

            if (moveDistance <= 0.0001f)
            {
                return;
            }
            Debug.Log(
    $"[Dash] frame={Time.frameCount}, " +
    $"dashDistance={dashDistance}, " +
    $"moveDistance={moveDistance}, " +
    $"direction={dashDirection}, " +
    $"positionBefore={transform.position}",
    this
);

            characterController.Move(dashDirection * moveDistance);


            Debug.Log(
                $"[Dash] positionAfter={transform.position}",
                this
            );
        }

        private Vector3 ResolveDashDirection()
        {
            Vector2 moveInput = inputProvider.MoveInput;
            if (moveInput.sqrMagnitude > 0.001f)
            {
                Camera referenceCamera = aimCamera != null ? aimCamera : Camera.main;
                if (referenceCamera != null)
                {
                    Vector3 cameraForward = Vector3.ProjectOnPlane(referenceCamera.transform.forward, Vector3.up).normalized;
                    Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;
                    Vector3 moveDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
                    if (moveDirection.sqrMagnitude > 0.0001f)
                    {
                        return moveDirection;
                    }
                }
            }

            if (useCameraForward)
            {
                Camera referenceCamera = aimCamera != null ? aimCamera : Camera.main;
                if (referenceCamera != null)
                {
                    Vector3 cameraForward = Vector3.ProjectOnPlane(referenceCamera.transform.forward, Vector3.up);
                    if (cameraForward.sqrMagnitude > 0.0001f)
                    {
                        return cameraForward.normalized;
                    }
                }
            }

            return transform.forward;
        }

        private void GetCapsulePoints(out Vector3 capsulePointTop, out Vector3 capsulePointBottom, out float capsuleRadius)
        {
            Vector3 worldCenter = transform.TransformPoint(characterController.center);
            float horizontalScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            capsuleRadius = characterController.radius * horizontalScale;

            float scaledHeight = Mathf.Max(characterController.height * Mathf.Abs(transform.lossyScale.y), capsuleRadius * 2.0f);
            float halfStraightHeight = scaledHeight * 0.5f - capsuleRadius;
            capsulePointTop = worldCenter + transform.up * halfStraightHeight;
            capsulePointBottom = worldCenter - transform.up * halfStraightHeight;
        }
    }
}
