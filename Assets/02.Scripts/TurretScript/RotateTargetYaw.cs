using UnityEngine;

public class RotateTargetYaw : MonoBehaviour
{
    [Header("Target Transform")]
    [SerializeField] private Transform target;

    [Header("Spin Speed")]
    [SerializeField] private float speed = 60f;

    [Header("Stop Angle")]
    [SerializeField] private float stopAngle = 1f;
    // 목표 방향과의 각도 차이가 이 값보다 작으면 회전을 멈춘다.

    [Header("Debug")]
    [SerializeField] private Vector3 rotationDirection = Vector3.zero;

    [SerializeField] private float signedYawAngle;
    // 디버그용: 현재 바라보는 방향과 타겟 방향 사이의 Yaw 각도 차이

    private void Update()
    {
        UpdateRotationYawToTarget();

        if (rotationDirection != Vector3.zero)
        {
            transform.Rotate(rotationDirection * speed * Time.deltaTime, Space.Self);
        }
    }

    private void UpdateRotationYawToTarget()
    {
        if (target == null)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 1. 현재 오브젝트에서 타겟으로 향하는 방향
        Vector3 directionToTarget = target.position - transform.position;

        // 2. Yaw만 회전해야 하므로 높이 차이는 제거
        directionToTarget.y = 0f;

        // 3. 타겟이 자기 자신과 거의 같은 위치라면 회전 불가
        if (directionToTarget.sqrMagnitude < 0.0001f)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 4. 현재 오브젝트가 바라보는 방향
        Vector3 currentForward = transform.forward;

        // 5. 현재 바라보는 방향도 수평 방향만 사용
        currentForward.y = 0f;

        // forward 가 y 축과 평행해지면 (0, 0, 0) 이 되어서 생략
        if (currentForward.sqrMagnitude < 0.0001f)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 6. 현재 방향에서 타겟 방향까지의 좌우 각도 계산
        // SignedAngle(from, to, axis) 
        // from 벡터에서 to 벡터 까지 axis를 축으로 오일러 각 값을 반환 
        signedYawAngle = Vector3.SignedAngle(
            currentForward,
            directionToTarget,
            Vector3.up
        );

        // 7. 거의 바라보고 있으면 회전 정지
        if (Mathf.Abs(signedYawAngle) <= stopAngle)
        {
            rotationDirection = Vector3.zero;
            return;
        }

        // 8. 타겟이 오른쪽에 있으면 +Y 회전, 왼쪽에 있으면 -Y 회전
        // Mathf.Sign 은 인자의 부호를 반환
        float rotateSign = Mathf.Sign(signedYawAngle);

        rotationDirection = new Vector3(0f, rotateSign, 0f);
    }
}