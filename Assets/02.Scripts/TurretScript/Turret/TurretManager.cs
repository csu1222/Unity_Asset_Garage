using System;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class TurretManager : MonoBehaviour
{
    // ================================
    // 추가: 타겟 선택 정책
    // ================================
    private enum TargetSelectionPolicy
    {
        Nearest = 0, // 가장 가까운 적 우선
        First = 1,   // 등록된 순서상 가장 먼저 발견된 적 우선
        Random = 2,  // 조건을 만족하는 적 중 랜덤 선택
    }

    [Header("Reference")]
    [SerializeField] private RotateTargetYaw turretHeadYawPivot;
    [SerializeField] private RotateTargetPitch turretBarrelPitchPivot;
    [SerializeField] private MissileLaunch turretHead;
    [SerializeField] private CheckAiming aimChecker;

    [Header("YawRotate Controller")]
    [SerializeField] private float yawSpeed = 180f;
    [SerializeField] private float yawStopAngle = 1f;

    [Header("PitchRotate Controller")]
    [SerializeField] private float minPitch = -45f;
    [SerializeField] private float maxPitch = 30f;
    [SerializeField] private float pitchSpeed = 45f;
    [SerializeField] private float pitchStopAngle = 1f;

    [Header("Launcher Controller")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private float projectileLifeTime = 3f;

    [Header("CheckAiming Controller")]
    [SerializeField] private float fireAngleThreshold = 5f;


    // ================================
    // 추가: 타겟 선택 관련 설정
    // ================================
    [Header("Target Selection")]
    [SerializeField]
    private TargetSelectionPolicy selectionPolicy = TargetSelectionPolicy.Nearest;

    [SerializeField]
    [Tooltip("이 값과 같은 TeamId를 가진 대상은 아군으로 간주하여 타겟에서 제외합니다. 음수면 팀 필터를 사용하지 않습니다.")]
    private int selfTeamId = -1;

    [SerializeField]
    [Tooltip("타겟 탐색 사거리입니다. 0 이하이면 사거리 제한 없이 탐색합니다.")]
    private float targetSearchRange = 15f;

    [SerializeField]
    [Tooltip("타겟을 다시 찾는 주기입니다. 너무 낮으면 매 프레임 타겟을 다시 검사합니다.")]
    private float targetRefreshInterval = 0.1f;

    // 추가: 현재 선택된 타겟
    private Transform currentTarget;

    // 추가: 타겟 갱신 타이머
    private float targetRefreshTimer;

    // 추가: 같은 타겟을 매 프레임 다시 적용하지 않기 위한 캐시
    private Transform lastAppliedTarget;

    // 추가: 외부에서 현재 타겟을 확인할 수 있게 하는 읽기 전용 프로퍼티
    public Transform CurrentTarget => currentTarget;

    private void Awake()
    {
        ResolveReference();
    }

    // ================================
    // 추가: 런타임 중 타겟 갱신
    // ================================
    private void Update()
    {
        UpdateTargetSelection();
    }


    private void OnValidate()
    {
        // Yaw Controller
        if (turretHeadYawPivot == null)
            return;

        yawSpeed = Mathf.Max(0, yawSpeed);
        yawStopAngle = Mathf.Max(0, yawStopAngle);

        ChangeYawSpeed(yawSpeed);
        ChangeYawStopAngle(yawStopAngle);

        // Pitch Controller
        if (turretBarrelPitchPivot == null)
            return;

        minPitch = Mathf.Max(-90f, minPitch);
        maxPitch = Mathf.Max(-90f, maxPitch);
        pitchSpeed = Mathf.Max(0, pitchSpeed);
        pitchStopAngle = Mathf.Max(0, pitchStopAngle);

        ChangeMinPitch(minPitch);
        ChangeMaxPitch(maxPitch);
        ChangePitchSpeed(pitchSpeed);
        ChangePitchStopAngle(pitchStopAngle);

        // Launch Controller
        if (turretHead == null)
            return;

        projectileSpeed = Mathf.Max(0, projectileSpeed);
        fireInterval = Mathf.Max(0, fireInterval);
        projectileLifeTime = Mathf.Max(0, projectileLifeTime);

        ChangeProjectileSpeed(projectileSpeed);
        ChangeFireInterval(fireInterval);
        ChangeProjectileLifeTime(projectileLifeTime);

        // CheckAiming
        if (aimChecker == null)
            return;

        fireAngleThreshold = Mathf.Max(0, fireAngleThreshold);

        ChangeFireAngleThreshold(fireAngleThreshold);

        // 추가: 타겟 관련 값 보정
        targetSearchRange = Mathf.Max(0f, targetSearchRange);
        targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
    }

    // ================================
    // 추가: TargetSelectionPolicy에 따라 타겟을 갱신하는 메인 로직
    // ================================
    private void UpdateTargetSelection()
    {
        targetRefreshTimer -= Time.deltaTime;

        if (targetRefreshTimer > 0f)
            return;

        targetRefreshTimer = targetRefreshInterval;

        Transform selectedTarget = GetCurrentTarget();

        currentTarget = selectedTarget;

        ApplyTargetToControllers();
    }

    // ================================
    // 추가: 선택 정책에 따라 실제 선택 함수 분기
    // ================================
    private Transform GetCurrentTarget()
    {
        switch (selectionPolicy)
        {
            case TargetSelectionPolicy.First:
                return SelectFirst();

            case TargetSelectionPolicy.Random:
                return SelectRandom();

            case TargetSelectionPolicy.Nearest:
            default:
                return SelectNearest();
        }
    }

    // ================================
    // 추가: 등록된 목록에서 가장 먼저 조건을 만족하는 적 선택
    // ================================
    private Transform SelectFirst()
    {
        var targets = EnemyTarget.ActiveTargets;

        for (int index = 0; index < targets.Count; index++)
        {
            EnemyTarget candidate = targets[index];

            if (!IsValidEnemy(candidate))
                continue;

            if (!IsWithinRange(candidate.AimWorldPosition))
                continue;

            return candidate.CachedTransform;
        }

        return null;
    }

    // ================================
    // 추가: 조건을 만족하는 적 중 랜덤 선택
    // ================================
    private Transform SelectRandom()
    {
        var targets = EnemyTarget.ActiveTargets;

        int validCount = 0;

        // 1차 순회: 유효한 타겟 개수 계산
        for (int index = 0; index < targets.Count; index++)
        {
            EnemyTarget candidate = targets[index];

            if (IsValidEnemy(candidate) && IsWithinRange(candidate.AimWorldPosition))
            {
                validCount++;
            }
        }

        if (validCount == 0)
            return null;

        int pickIndex = UnityEngine.Random.Range(0, validCount);
        int seenValid = 0;

        // 2차 순회: 랜덤으로 선택된 번째의 유효 타겟 반환
        for (int index = 0; index < targets.Count; index++)
        {
            EnemyTarget candidate = targets[index];

            if (!IsValidEnemy(candidate))
                continue;

            if (!IsWithinRange(candidate.AimWorldPosition))
                continue;

            if (seenValid == pickIndex)
            {
                return candidate.CachedTransform;
            }

            seenValid++;
        }

        return null;
    }

    // ================================
    // 추가: 가장 가까운 적 선택
    // ================================
    private Transform SelectNearest()
    {
        var targets = EnemyTarget.ActiveTargets;

        float nearestDistanceSqr = float.PositiveInfinity;
        Transform nearest = null;

        for (int index = 0; index < targets.Count; index++)
        {
            EnemyTarget candidate = targets[index];

            if (!IsValidEnemy(candidate))
                continue;

            Vector3 candidatePosition = candidate.AimWorldPosition;

            if (!IsWithinRange(candidatePosition))
                continue;

            // 거리 비교만 필요하므로 Distance 대신 sqrMagnitude 사용
            float distanceSqr = (candidatePosition - transform.position).sqrMagnitude;

            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = candidate.CachedTransform;
            }
        }

        return nearest;
    }

    // ================================
    // 추가: 아군 처리 로직
    // ================================
    private bool IsValidEnemy(EnemyTarget candidate)
    {
        if (candidate == null)
            return false;

        // selfTeamId가 음수이면 팀 구분을 사용하지 않습니다.
        // 이 경우 EnemyTarget에 등록된 대상은 모두 타겟 후보가 됩니다.
        if (selfTeamId < 0)
            return true;

        // candidate.TeamId가 내 팀 ID와 같으면 아군이므로 제외합니다.
        return candidate.TeamId != selfTeamId;
    }

    // ================================
    // 추가: 사거리 검사 로직
    // ================================
    private bool IsWithinRange(Vector3 candidateWorldPosition)
    {
        // targetSearchRange가 0 이하이면 사거리 제한 없이 탐색합니다.
        if (targetSearchRange <= 0f)
            return true;

        float rangeSqr = targetSearchRange * targetSearchRange;
        float distanceSqr = (candidateWorldPosition - transform.position).sqrMagnitude;

        return distanceSqr <= rangeSqr;
    }

    // ================================
    // 추가: 선택된 타겟을 각 컨트롤러에 전달
    // ================================
    private void ApplyTargetToControllers()
    {
        // 같은 타겟이면 다시 적용하지 않습니다.
        // 단, currentTarget이 null로 바뀐 경우에는 아래 조건을 통과해서
        // 각 컨트롤러의 target도 null로 비워야 합니다.
        if (lastAppliedTarget == currentTarget)
            return;

        lastAppliedTarget = currentTarget;

        // Yaw 회전 컨트롤러에 타겟 전달
        // currentTarget이 null이면 RotateTargetYaw 내부 target도 null로 비워집니다.
        if (turretHeadYawPivot != null)
        {
            turretHeadYawPivot.SetTarget(currentTarget);
        }

        // Pitch 회전 컨트롤러에 타겟 전달
        // currentTarget이 null이면 RotateTargetPitch 내부 target도 null로 비워집니다.
        if (turretBarrelPitchPivot != null)
        {
            turretBarrelPitchPivot.SetTarget(currentTarget);
        }

        // 조준 판정 컨트롤러에 타겟 전달
        // currentTarget이 null이면 CheckAiming.CalculateAim()에서 canFire = false가 됩니다.
        if (aimChecker != null)
        {
            aimChecker.SetTarget(currentTarget);
        }

        // MissileLaunch는 SetTarget을 호출하지 않습니다.
        // 이유:
        // MissileLaunch는 직접 타겟을 추적하지 않고,
        // CheckAiming.CanFire 값과 muzzle의 위치/회전만 사용해서 발사합니다.
    }

    public void ChangeYawSpeed(float value)
    {
        turretHeadYawPivot.SetYawSpeed(value);
    }

    public void ChangeYawStopAngle(float value)
    {
        turretHeadYawPivot.SetYawStopAngle(value);
    }

    public void ChangeMinPitch(float value)
    {
        turretBarrelPitchPivot.SetMinPitch(value);
    }

    public void ChangeMaxPitch(float value)
    {
        turretBarrelPitchPivot.SetMaxPitch(value);
    }

    public void ChangePitchSpeed(float value)
    {
        turretBarrelPitchPivot.SetPitchSpeed(value);
    }

    public void ChangePitchStopAngle(float value)
    {
        turretBarrelPitchPivot.SetPitchStopAngle(value);
    }

    public void ChangeProjectileSpeed(float value)
    {
        turretHead.SetProjectileSpeed(value);
    }

    public void ChangeFireInterval(float value)
    {
        turretHead.SetFireInterval(value);
    }

    public void ChangeProjectileLifeTime(float value)
    {
        turretHead.SetProjectileLifeTime(value);
    }

    public void ChangeFireAngleThreshold(float value)
    {
        aimChecker.SetFireAngleThreshold(value);
    }

    private void ResolveReference()
    {
        if (turretHeadYawPivot == null)
        {
            turretHeadYawPivot = GetComponent<RotateTargetYaw>();
        }
        if (turretHeadYawPivot == null)
        {
            Debug.LogWarning("[Turret Manager] Rotate Target Yaw 탐색 불가");
            return;
        }

        if (turretBarrelPitchPivot == null)
        {
            turretBarrelPitchPivot = GetComponent<RotateTargetPitch>();
        }
        if (turretBarrelPitchPivot == null)
        {
            Debug.LogWarning("[Turret Manager] Rotate Target Pitch 탐색 불가");
            return;
        }

        if (turretHead == null)
        {
            turretHead = GetComponent<MissileLaunch>();
        }
        if (turretHead == null)
        {
            Debug.LogWarning("[Turret Manager] Missile Launch 탐색 불가");
            return;
        }

        if (aimChecker == null)
        {
            aimChecker = GetComponent<CheckAiming>();
        }
        if (aimChecker == null)
        {
            Debug.LogWarning("[Turret Manager] Check Aiming 탐색 불가");
            return;
        }
    }
}
