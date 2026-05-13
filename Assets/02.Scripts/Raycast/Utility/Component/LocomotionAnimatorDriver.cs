using UnityEngine;

namespace SystemicOverload.Phase1
{
    /// <summary>
    /// Movement/CharacterController 상태를 Animator 파라미터로 전달합니다. 클립은 Animator Controller에서 배치합니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(50)]
    public sealed class LocomotionAnimatorDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int IsGroundedId = Animator.StringToHash("IsGrounded");

        [SerializeField] private MovementComponent movementComponent;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float speedDampTime = 0.08f;

        private Animator animator;
        private bool hasSpeedParameter;
        private bool hasIsGroundedParameter;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            movementComponent ??= GetComponent<MovementComponent>();
            characterController ??= GetComponent<CharacterController>();
            CacheParameterAvailability();
        }

        private void OnEnable()
        {
            // 에디터에서 RuntimeAnimatorController를 나중에 할당한 경우에도 파라미터 캐시를 다시 구축합니다.
            CacheParameterAvailability();
        }

        private void OnValidate()
        {
            speedDampTime = Mathf.Max(0.0f, speedDampTime);
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            if (hasSpeedParameter && movementComponent != null)
            {
                float targetSpeed = movementComponent.NormalizedPlanarSpeed;
                animator.SetFloat(SpeedId, targetSpeed, speedDampTime, Time.deltaTime);
            }

            if (hasIsGroundedParameter && characterController != null)
            {
                animator.SetBool(IsGroundedId, characterController.isGrounded);
            }
        }

        /// <summary>
        /// 런타임에 존재하는 파라미터만 갱신해, 빈 Controller에도 안전하게 동작합니다.
        /// </summary>
        private void CacheParameterAvailability()
        {
            hasSpeedParameter = false;
            hasIsGroundedParameter = false;

            if (animator == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == SpeedId)
                {
                    hasSpeedParameter = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == IsGroundedId)
                {
                    hasIsGroundedParameter = true;
                }
            }
        }
    }
}
