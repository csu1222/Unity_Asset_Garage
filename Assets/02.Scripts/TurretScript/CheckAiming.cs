using UnityEngine;

public class CheckAiming : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Muzzle")]
    [SerializeField] private Transform muzzle;

    [Header("Aim Margin")]
    [SerializeField] private float fireAngleThreshold = 5f;

    [Header("Debug")]
    [SerializeField] private float signedYawAngle;
    [SerializeField] private float signedPitchAngle;
    [SerializeField] private bool canFire = false;

    public bool CanFire => canFire;

    private void Update()
    {
        CalculateAim();

        if (canFire)
        {
            // TODO : 조준완료 

            Debug.Log("[CheckAiming] 발사 준비 완료");
        }
        else
        {
            Debug.Log("[CheckAiming] 발사 준비 중");
        }
    }

    private void CalculateAim()
    {
        if (target == null || muzzle == null)
        {
            canFire = false;
            return;
        }

        Vector3 directionToTarget = target.position - muzzle.position;

        if (directionToTarget.sqrMagnitude < 0.0001f)
        {
            canFire = false;
            return;
        }

        Vector3 muzzleForward = muzzle.forward;

        // -------------------------
        // Yaw 판정
        // -------------------------

        Vector3 directionToTargetYaw = directionToTarget;
        directionToTargetYaw.y = 0f;

        Vector3 muzzleForwardYaw = muzzleForward;
        muzzleForwardYaw.y = 0f;

        if (directionToTargetYaw.sqrMagnitude < 0.0001f ||
            muzzleForwardYaw.sqrMagnitude < 0.0001f)
        {
            canFire = false;
            return;
        }

        signedYawAngle = Vector3.SignedAngle(
            muzzleForwardYaw,
            directionToTargetYaw,
            Vector3.up
        );

        // -------------------------
        // Pitch 판정
        // -------------------------

        Vector3 pitchAxis = muzzle.right;

        Vector3 projectedForward = Vector3.ProjectOnPlane(muzzleForward, pitchAxis);
        Vector3 projectedTargetDirection = Vector3.ProjectOnPlane(directionToTarget, pitchAxis);

        if (projectedForward.sqrMagnitude < 0.0001f ||
            projectedTargetDirection.sqrMagnitude < 0.0001f)
        {
            canFire = false;
            return;
        }

        projectedForward.Normalize();
        projectedTargetDirection.Normalize();
        
        signedPitchAngle = Vector3.SignedAngle(
            projectedForward,
            projectedTargetDirection,
            pitchAxis
        );

        if (Mathf.Abs(signedYawAngle) <= fireAngleThreshold && Mathf.Abs(signedPitchAngle) <= fireAngleThreshold)
        {
            canFire = true;
        }
        else
        {
            canFire = false;
        }
    }
}
