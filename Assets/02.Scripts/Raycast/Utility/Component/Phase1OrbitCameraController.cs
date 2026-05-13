using System.Collections.Generic;
using UnityEngine;

namespace SystemicOverload.Phase1
{
    [RequireComponent(typeof(Camera))]
    public class Phase1OrbitCameraController : MonoBehaviour
    {
        public enum AutoFollowMode
        {
            Always,
            MovingOnly,
            Manual
        }

        [Header("Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private InputProvider inputProvider;
        [SerializeField] private MovementComponent movementComponent;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0.0f, 1.7f, 0.0f);

        [Header("Pointer Look")]
        [SerializeField] private float yawSensitivity = 0.16f;
        [SerializeField] private float pitchSensitivity = 0.11f;

        [Header("Gamepad Look")]
        [SerializeField] private float gamepadYawSpeed = 180.0f;
        [SerializeField] private float gamepadPitchSpeed = 130.0f;

        [Header("Pitch Clamp")]
        [SerializeField] private float minPitch = -35.0f;
        [SerializeField] private float maxPitch = 75.0f;

        [Header("Zoom")]
        [SerializeField] private float defaultZoomDistance = 7.0f;
        [SerializeField] private float maxZoomDistance = 14.0f;
        [SerializeField] private float zoomSpeed = 5.0f;
        [SerializeField] private float zoomSmoothing = 16.0f;
        [SerializeField] private float firstPersonThreshold = 0.05f;

        [Header("Auto Follow")]
        [SerializeField] private AutoFollowMode autoFollowMode = AutoFollowMode.MovingOnly;
        [SerializeField] private float autoFollowSharpness = 4.0f;

        [Header("Spring Arm Collision")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionSphereRadius = 0.2f;
        [SerializeField] private float collisionBuffer = 0.12f;

        [Header("Environment Transition")]
        [SerializeField] private float waterSurfaceHeight = -1000.0f;
        [SerializeField] private List<GameObject> underwaterEffectRoots = new List<GameObject>();

        private float currentYaw;
        private float currentPitch = 22.0f;
        private float targetZoomDistance;
        private float currentZoomDistance;
        private float currentResolvedDistance;
        private bool wasInFirstPerson;
        private bool isUnderwaterActive;
        private Transform cachedRendererTarget;
        private Renderer[] cachedTargetRenderers;

        public Transform FollowTarget => followTarget;
        public float CurrentYaw => currentYaw;
        public Vector3 PivotPosition => followTarget == null ? transform.position : followTarget.position + pivotOffset;

        private void Awake()
        {
            targetZoomDistance = Mathf.Clamp(defaultZoomDistance, 0.0f, maxZoomDistance);
            currentZoomDistance = targetZoomDistance;
            currentResolvedDistance = currentZoomDistance;

            if (followTarget != null)
            {
                currentYaw = followTarget.eulerAngles.y;
                CacheTargetRenderers();
            }
        }

        private void OnDisable()
        {
            SetTargetRenderersEnabled(true);
            SetUnderwaterEffectsActive(false);
            wasInFirstPerson = false;
            isUnderwaterActive = false;
        }

        private void OnValidate()
        {
            yawSensitivity = Mathf.Max(0.0f, yawSensitivity);
            pitchSensitivity = Mathf.Max(0.0f, pitchSensitivity);
            gamepadYawSpeed = Mathf.Max(0.0f, gamepadYawSpeed);
            gamepadPitchSpeed = Mathf.Max(0.0f, gamepadPitchSpeed);
            if (maxPitch < minPitch)
            {
                maxPitch = minPitch;
            }

            maxZoomDistance = Mathf.Max(0.0f, maxZoomDistance);
            defaultZoomDistance = Mathf.Clamp(defaultZoomDistance, 0.0f, maxZoomDistance);
            zoomSpeed = Mathf.Max(0.0f, zoomSpeed);
            zoomSmoothing = Mathf.Max(0.0f, zoomSmoothing);
            firstPersonThreshold = Mathf.Clamp(firstPersonThreshold, 0.0f, maxZoomDistance);
            autoFollowSharpness = Mathf.Max(0.0f, autoFollowSharpness);
            collisionSphereRadius = Mathf.Max(0.0f, collisionSphereRadius);
            collisionBuffer = Mathf.Max(0.0f, collisionBuffer);
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            ResolveReferences();
            HandleLookInput();
            HandleZoomInput();
            ApplyAutoFollow();
            UpdateCameraTransform();
            UpdateFirstPersonRendering();
            UpdateEnvironmentTransition();
        }

        private void ResolveReferences()
        {
            if (inputProvider == null)
            {
                inputProvider = followTarget.GetComponent<InputProvider>();
            }

            if (movementComponent == null)
            {
                movementComponent = followTarget.GetComponent<MovementComponent>();
            }

            if (cachedRendererTarget != followTarget)
            {
                CacheTargetRenderers();
            }
        }

        private void HandleLookInput()
        {
            if (inputProvider == null)
            {
                return;
            }

            if (inputProvider.HasGamepadLookInput)
            {
                Vector2 lookInput = inputProvider.GamepadLookInput;
                currentYaw += lookInput.x * gamepadYawSpeed * Time.deltaTime;
                currentPitch = Mathf.Clamp(currentPitch - lookInput.y * gamepadPitchSpeed * Time.deltaTime, minPitch, maxPitch);
                return;
            }

            if (!inputProvider.IsCameraLookHeld)
            {
                return;
            }

            Vector2 lookDelta = inputProvider.PointerLookDelta;
            if (lookDelta.sqrMagnitude <= 0.0f)
            {
                return;
            }

            currentYaw += lookDelta.x * yawSensitivity;
            currentPitch = Mathf.Clamp(currentPitch - lookDelta.y * pitchSensitivity, minPitch, maxPitch);
        }

        private void HandleZoomInput()
        {
            if (inputProvider != null)
            {
                targetZoomDistance = Mathf.Clamp(targetZoomDistance - inputProvider.ZoomDelta * zoomSpeed, 0.0f, maxZoomDistance);
            }

            float zoomBlend = 1.0f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);
            currentZoomDistance = Mathf.Lerp(currentZoomDistance, targetZoomDistance, zoomBlend);
        }

        private void ApplyAutoFollow()
        {
            if (inputProvider == null || followTarget == null)
            {
                return;
            }

            bool isDraggingCamera = inputProvider.IsCameraLookHeld || inputProvider.HasGamepadLookInput;
            if (isDraggingCamera)
            {
                return;
            }

            bool shouldFollow = autoFollowMode == AutoFollowMode.Always;
            if (autoFollowMode == AutoFollowMode.MovingOnly && movementComponent != null)
            {
                shouldFollow = movementComponent.CurrentPlanarVelocity.sqrMagnitude > 0.0001f;
            }

            if (!shouldFollow)
            {
                return;
            }

            float targetYaw = followTarget.eulerAngles.y;
            float blendFactor = 1.0f - Mathf.Exp(-autoFollowSharpness * Time.deltaTime);
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, blendFactor);
        }

        private void UpdateCameraTransform()
        {
            Vector3 pivotPosition = followTarget.position + pivotOffset;
            Quaternion lookRotation = Quaternion.Euler(currentPitch, currentYaw, 0.0f);
            Vector3 desiredDirection = lookRotation * Vector3.back;

            currentResolvedDistance = ResolveCollisionDistance(pivotPosition, desiredDirection, currentZoomDistance);

            transform.position = pivotPosition + desiredDirection * currentResolvedDistance;
            transform.rotation = lookRotation;
        }

        private float ResolveCollisionDistance(Vector3 pivotPosition, Vector3 desiredDirection, float desiredDistance)
        {
            if (desiredDistance <= 0.0001f)
            {
                return 0.0f;
            }

            if (Physics.SphereCast(pivotPosition, collisionSphereRadius, desiredDirection, out RaycastHit hitInfo, desiredDistance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(hitInfo.distance - collisionBuffer, 0.0f);
            }

            return desiredDistance;
        }

        private void UpdateFirstPersonRendering()
        {
            bool shouldEnableFirstPerson = currentResolvedDistance <= firstPersonThreshold;
            if (shouldEnableFirstPerson == wasInFirstPerson)
            {
                return;
            }

            wasInFirstPerson = shouldEnableFirstPerson;
            SetTargetRenderersEnabled(!shouldEnableFirstPerson);
        }

        private void UpdateEnvironmentTransition()
        {
            bool shouldActivateUnderwater = transform.position.y < waterSurfaceHeight;
            if (shouldActivateUnderwater == isUnderwaterActive)
            {
                return;
            }

            isUnderwaterActive = shouldActivateUnderwater;
            SetUnderwaterEffectsActive(shouldActivateUnderwater);
        }

        private void CacheTargetRenderers()
        {
            cachedRendererTarget = followTarget;
            cachedTargetRenderers = followTarget == null
                ? System.Array.Empty<Renderer>()
                : followTarget.GetComponentsInChildren<Renderer>(true);
        }

        private void SetTargetRenderersEnabled(bool enabled)
        {
            if (cachedTargetRenderers == null || cachedRendererTarget != followTarget)
            {
                CacheTargetRenderers();
            }

            foreach (Renderer targetRenderer in cachedTargetRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = enabled;
                }
            }
        }

        private void SetUnderwaterEffectsActive(bool active)
        {
            foreach (GameObject effectRoot in underwaterEffectRoots)
            {
                if (effectRoot != null)
                {
                    effectRoot.SetActive(active);
                }
            }
        }
    }
}