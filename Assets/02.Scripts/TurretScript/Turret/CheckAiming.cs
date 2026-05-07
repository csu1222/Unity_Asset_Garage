using System;
using UnityEngine;

public class CheckAiming : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Muzzle")]
    [SerializeField] private Transform muzzle;

    [Header("Aim Margin")]
    [SerializeField] private float fireAngleThreshold = 5f;

    public float FireAngleThreshold => fireAngleThreshold;

    public event Action<float> OnFireAngleThresholdChanged;

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

    public void SetTarget(Transform newTarget)
    {
        // 변경점:
        // newTarget이 null이어도 target에 대입할 수 있게 했습니다.
        // 이유:
        // 타겟을 잃었을 때 기존 타겟을 비워야 터렛이 더 이상 이전 타겟을 추적하지 않습니다.
        if (target == newTarget)
            return;

        target = newTarget;
    }

    public void SetFireAngleThreshold(float newFireAngleThreshold)
    {
        if (fireAngleThreshold == newFireAngleThreshold)
            return;

        fireAngleThreshold = newFireAngleThreshold;

        OnFireAngleThresholdChanged?.Invoke(fireAngleThreshold);
    }
}
