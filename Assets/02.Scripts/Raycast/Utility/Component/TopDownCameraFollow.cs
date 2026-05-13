using UnityEngine;

namespace SystemicOverload.Phase1
{
    /// <summary>
    /// Lightweight top-down follow camera for Phase 1 validation scenes.
    /// </summary>
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 followOffset = new Vector3(0.0f, 14.0f, -8.0f);
        [SerializeField] private float positionSharpness = 10.0f;
        [SerializeField] private bool lockRotation = true;
        [SerializeField] private Vector3 lockedEulerAngles = new Vector3(60.0f, 0.0f, 0.0f);

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            Vector3 targetPosition = followTarget.position + followOffset;
            float deltaTime = Time.deltaTime;
            if (deltaTime > 0.0f)
            {
                float blendFactor = 1.0f - Mathf.Exp(-positionSharpness * deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPosition, blendFactor);
            }
            else
            {
                transform.position = targetPosition;
            }

            if (lockRotation)
            {
                transform.rotation = Quaternion.Euler(lockedEulerAngles);
                return;
            }

            Vector3 lookDirection = followTarget.position - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        private void OnValidate()
        {
            positionSharpness = Mathf.Max(0.0f, positionSharpness);
        }
    }
}
