using System;
using UnityEngine;

public class RotateTargetPitch : MonoBehaviour
{
    [Header("Transform")]
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private Transform target;

    [Header("Angle Limit")]
    [SerializeField] private float minPitch = -45f;
    [SerializeField] private float maxPitch = 30f;

    [Header("Spin Speed")]
    [SerializeField] private float pitchSpeed = 30f;
    
    [Header("Stop Angle")]
    [SerializeField] private float pitchStopAngle = 1f;

    public float MinPitch => minPitch;
    public float MaxPitch => maxPitch;
    public float PitchSpeed => pitchSpeed;
    public float StopAngle => pitchStopAngle;

    // Event
    public event Action<float> OnMinPitchChanged;
    public event Action<float> OnMaxPitchChanged;
    public event Action<float> OnPitchSpeedChanged;
    public event Action<float> OnPitchStopAngleChanged;

    [Header("Debug")]
    [SerializeField] private Vector3 rotationDirection = Vector3.zero;

    [SerializeField] private float signedPitchAngle;
    // 디버그용: 현재 바라보는 방향과 타겟 방향 사이의 Pitch 각도 차이

    [SerializeField] private float currentPitch;
    // 디버그용: 현재 로컬 X축 Pitch 각도

    private void Update()
    {
        UpdateRotationPitchToTarget();

        if (rotationDirection != Vector3.zero)
        {
            RotatePitchWithLimit();
        }
    }

    private void UpdateRotationPitchToTarget()
    {
        if (target == null)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 1. 현재 오브젝트에서 타겟으로 향하는 방향
        Vector3 directionToTarget = target.position - pitchPivot.transform.position;

        if (directionToTarget.sqrMagnitude < 0.0001f)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 2. Pitch 회전축
        // Space.Self 기준 X축 회전은 transform.right 축을 기준으로 회전한다.
        Vector3 pitchAxis = pitchPivot.transform.right;

        // 3. 현재 forward와 target 방향을 Pitch 회전 평면에 투영
        // 즉, Pitch 회전에 필요한 위/아래 각도만 계산하기 위한 처리
        Vector3 projectedForward = Vector3.ProjectOnPlane(pitchPivot.transform.forward, pitchAxis);
        Vector3 projectedTargetDirection = Vector3.ProjectOnPlane(directionToTarget, pitchAxis);

        if (projectedForward.sqrMagnitude < 0.0001f ||
            projectedTargetDirection.sqrMagnitude < 0.0001f)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        projectedForward.Normalize();
        projectedTargetDirection.Normalize();

        // 4. 현재 방향에서 타겟 방향까지의 Pitch 각도 계산
        signedPitchAngle = Vector3.SignedAngle(
            projectedForward,
            projectedTargetDirection,
            pitchAxis
        );

        // 5. 거의 바라보고 있으면 회전 정지
        if (Mathf.Abs(signedPitchAngle) <= pitchStopAngle)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 6. 현재 Pitch 각도 확인
        currentPitch = NormalizeAngle(pitchPivot.transform.localEulerAngles.x);

        // 7. 이미 Min / Max 제한에 도달했고,
        //    Target이 그 제한 바깥에 있다면 더 이상 회전하지 않음
        if (currentPitch <= minPitch && signedPitchAngle < 0f)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        if (currentPitch >= maxPitch && signedPitchAngle > 0f)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 8. 타겟이 위쪽이면 -X 또는 +X 방향,
        //    타겟이 아래쪽이면 반대 방향으로 회전한다.
        if (signedPitchAngle > 0f)
        {
            rotationDirection = new Vector3(1f, 0f, 0f);
        }
        else if (signedPitchAngle < 0f)
        {
            rotationDirection = new Vector3(-1f, 0f, 0f);
        }
        else
        {
            rotationDirection = Vector3.zero;
        }
    }

    private void RotatePitchWithLimit()
    {
        Vector3 localEuler = pitchPivot.transform.localEulerAngles;

        currentPitch = NormalizeAngle(localEuler.x);

        float pitchDelta = rotationDirection.x * pitchSpeed * Time.deltaTime;

        float nextPitch = Mathf.Clamp(
            currentPitch + pitchDelta,
            minPitch,
            maxPitch
        );

        pitchPivot.transform.localEulerAngles = new Vector3(
            nextPitch,
            localEuler.y,
            localEuler.z
        );
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

    public void SetMinPitch(float newMinPitch)
    {
        if (minPitch == newMinPitch)
            return;

        minPitch = newMinPitch;

        OnMinPitchChanged?.Invoke(minPitch);
    }

    public void SetMaxPitch(float newMaxPitch)
    {
        if (maxPitch == newMaxPitch)
            return;

        maxPitch = newMaxPitch;

        OnMaxPitchChanged?.Invoke(maxPitch);
    }

    public void SetPitchSpeed(float newPitchSpeed)
    {
        if (pitchSpeed == newPitchSpeed)
            return;

        pitchSpeed = newPitchSpeed;

        OnPitchSpeedChanged?.Invoke(pitchSpeed);
    }

    public void SetPitchStopAngle(float newPitchStopAngle)
    {
        if (pitchStopAngle == newPitchStopAngle)
            return;

        pitchStopAngle = newPitchStopAngle;

        OnPitchStopAngleChanged?.Invoke(pitchStopAngle);
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}