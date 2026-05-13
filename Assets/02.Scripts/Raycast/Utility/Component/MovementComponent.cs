using UnityEngine;

namespace SystemicOverload.Phase1
{
    /// <summary>
    /// Basic CharacterController-driven movement and facing for Phase 1.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(InputProvider))]
    public sealed class MovementComponent : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6.0f;
        [SerializeField] private float accelerationSharpness = 14.0f;
        [SerializeField] private float decelerationSharpness = 18.0f;
        [SerializeField] private float gravity = -25.0f;
        [SerializeField] private float groundedSnapVelocity = -2.0f;

        [Header("Rotation")]
        [SerializeField] private float rotationSharpness = 16.0f;
        [SerializeField] private bool useMouseRaycastRotation;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private float aimRayMaxDistance = 300.0f;
        [SerializeField] private Phase1OrbitCameraController orbitCameraController;

        private CharacterController characterController;
        private InputProvider inputProvider;
        private Vector3 currentPlanarVelocity;
        private float verticalVelocity;

        public Vector3 CurrentPlanarVelocity => currentPlanarVelocity;

        /// <summary>
        /// 인스펙터에 설정된 최대 평면 이동 속도입니다. 애니메이션 정규화에 사용합니다.
        /// </summary>
        public float MaxMoveSpeed => moveSpeed;

        /// <summary>
        /// 0~1로 정규화된 현재 평면 속도입니다. Blend Tree `Speed` 파라미터에 바로 넣을 수 있습니다.
        /// </summary>
        public float NormalizedPlanarSpeed =>
            moveSpeed > 0.0001f ? Mathf.Clamp01(currentPlanarVelocity.magnitude / moveSpeed) : 0.0f;

        public Vector3 LastAimPoint { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputProvider = GetComponent<InputProvider>();
            TryResolveOrbitCameraController();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.0f, moveSpeed);
            accelerationSharpness = Mathf.Max(0.0f, accelerationSharpness);
            decelerationSharpness = Mathf.Max(0.0f, decelerationSharpness);
            rotationSharpness = Mathf.Max(0.0f, rotationSharpness);
            groundedSnapVelocity = Mathf.Min(groundedSnapVelocity, 0.0f);
            gravity = Mathf.Min(gravity, 0.0f);
            aimRayMaxDistance = Mathf.Max(0.0f, aimRayMaxDistance);

            if (orbitCameraController == null && aimCamera != null)
            {
                orbitCameraController = aimCamera.GetComponent<Phase1OrbitCameraController>();
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0.0f)
            {
                return;
            }

            UpdatePlanarMovement(deltaTime);
            UpdateRotation(deltaTime);
            UpdateVerticalMovement(deltaTime);
        }

        private void UpdatePlanarMovement(float deltaTime)
        {
            Camera targetCamera = ResolveAimCamera();
            if (targetCamera == null)
            {
                currentPlanarVelocity = Vector3.zero;
                return;
            }

            Vector2 moveInput = inputProvider.MoveInput;

            Vector3 cameraForwardOnPlane = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up);
            if (cameraForwardOnPlane.sqrMagnitude <= 0.0001f)
            {
                cameraForwardOnPlane = Vector3.forward;
            }
            else
            {
                cameraForwardOnPlane.Normalize();
            }

            Vector3 cameraRightOnPlane = Vector3.Cross(Vector3.up, cameraForwardOnPlane);
            Vector3 desiredPlanarDirection = cameraForwardOnPlane * moveInput.y + cameraRightOnPlane * moveInput.x;
            Vector3 desiredPlanarVelocity = desiredPlanarDirection * moveSpeed;

            float smoothingSharpness = desiredPlanarVelocity.sqrMagnitude > 0.0001f
                ? accelerationSharpness
                : decelerationSharpness;
            float blendFactor = 1.0f - Mathf.Exp(-smoothingSharpness * deltaTime);
            currentPlanarVelocity = Vector3.Lerp(currentPlanarVelocity, desiredPlanarVelocity, blendFactor);
        }

        private void UpdateVerticalMovement(float deltaTime)
        {
            if (characterController.isGrounded && verticalVelocity < 0.0f)
            {
                verticalVelocity = groundedSnapVelocity;
            }
            else
            {
                verticalVelocity += gravity * deltaTime;
            }

            Vector3 frameVelocity = currentPlanarVelocity + Vector3.up * verticalVelocity;
            CollisionFlags collisionFlags = characterController.Move(frameVelocity * deltaTime);
            if ((collisionFlags & CollisionFlags.Below) != 0 && verticalVelocity < groundedSnapVelocity)
            {
                verticalVelocity = groundedSnapVelocity;
            }
        }

        private void UpdateRotation(float deltaTime)
        {
            if (inputProvider.ShouldAlignCharacterToCamera && TryRotateTowardCameraYaw(deltaTime))
            {
                return;
            }

            if (inputProvider.ShouldBlockPointerFacing)
            {
                return;
            }

            if (useMouseRaycastRotation && !inputProvider.IsUsingGamepad)
            {
                RotateTowardPointer(deltaTime);
            }
        }

        private bool TryRotateTowardCameraYaw(float deltaTime)
        {
            Camera targetCamera = ResolveAimCamera();
            if (targetCamera == null)
            {
                return false;
            }

            TryResolveOrbitCameraController(targetCamera);

            float targetYaw = orbitCameraController != null
                ? orbitCameraController.CurrentYaw
                : targetCamera.transform.eulerAngles.y;
            Quaternion targetRotation = Quaternion.Euler(0.0f, targetYaw, 0.0f);
            float blendFactor = 1.0f - Mathf.Exp(-rotationSharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blendFactor);
            return true;
        }

        private void RotateTowardPointer(float deltaTime)
        {
            Camera targetCamera = ResolveAimCamera();
            if (targetCamera == null)
            {
                return;
            }

            Ray aimRay = targetCamera.ScreenPointToRay(inputProvider.PointerScreenPosition);
            if (!Physics.Raycast(aimRay, out RaycastHit hitInfo, aimRayMaxDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            LastAimPoint = hitInfo.point;

            Vector3 lookDirection = hitInfo.point - transform.position;
            lookDirection.y = 0.0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            float blendFactor = 1.0f - Mathf.Exp(-rotationSharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blendFactor);
        }

        private Camera ResolveAimCamera()
        {
            if (aimCamera != null)
            {
                return aimCamera;
            }

            aimCamera = Camera.main;
            return aimCamera;
        }

        private void TryResolveOrbitCameraController(Camera targetCamera = null)
        {
            if (orbitCameraController != null)
            {
                return;
            }

            targetCamera ??= ResolveAimCamera();
            if (targetCamera != null)
            {
                orbitCameraController = targetCamera.GetComponent<Phase1OrbitCameraController>();
            }
        }
    }
}
