using System;
using System.Collections;
using UnityEngine;

/*# 0508 
# Turret 고쳤던,  +Pool  이어서 upgrade 
# 코루틴을 사용해서 연출을 최소 3개 이상 만들어주세요 !
여러분의 콘텐츠 IP 를 적용해주세요 ~ ~
짝 이랑 동일 한것은 OK 
프로젝트에 사용할 가능성을 고려해주세요.
코루틴 -> 절차적 연출 
EX) 즉발 총알 발사 →[ 반동 모션 + 쿨타임+발사 + 쿨타임 + 이펙트 On + 사운드 ]-> 절차 -> 코루틴으로 절차적으로 만들기 
코루틴 호출 절차 묶음 3개 [  ] 

# 완성된 GIF  + 적용한 코드 스크린샷 제출
*/

/*
 추가할 연출 
    1. 터렛에 장탄수를 추가한 후 장탄수를 다 사용하면 재장전
        - MissileLauncher 에서 장탄수 개념 추가, 재장전 시간 필드 추가 코루틴으로 재장전 타이머 동안 발사 금지

    2. Enemy 피격시 0.5초 정지와 붉은 색으로 색 변화 그리고 그동안 피격 판정을 안받도록
        - Enumy Prefab에서 피격모션 길이 필드 추가 피격시 변한 색상 추가, 코루틴으로 피격시 이동 멈춤과 Collider off, 피격모션 끝나면 다시 원상 복구

    3. 터렛 격발시 포신 반동 모션
        - TurretManager 에 포신 transform 필드 추가, 탄환 발사 속도에 비례해 Lerp 로 반동모션 시간 계산, 반동 크기 필드화, 반동 구현은 포신의 Local Position을 사용
          반동은 발사 시간으로 제어하지만 또 bool로 체크를 해 다음 발사 가능 여부 판별
 */

public class TurretManager : MonoBehaviour
{
    // ================================
    // 추가: 터렛 상태
    // ================================
    public enum TurretState
    {
        Aiming = 0,    // 타겟을 찾았지만 아직 발사 조건을 만족하지 못한 상태
        FireReady = 1, // 조준, 탄환, 발사 간격 조건을 모두 만족해서 발사 가능한 상태
        Reloading = 2  // 탄환을 모두 사용해서 재장전 코루틴이 진행 중인 상태
    }

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

    /*
     * 변경점:
     * 포신 반동 연출은 별도 컴포넌트인 BarrelRecoil이 담당합니다.
     * TurretManager는 발사 성공 시점에 PlayRecoil()만 호출하고,
     * Inspector 설정값을 BarrelRecoil에 전달합니다.
     */
    [SerializeField] private BarrelRecoil barrelRecoil;

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
    [SerializeField] private int maxMagazine = 10;
    [SerializeField] private float reloadTime = 2f;

    [Header("Barrel Recoil Controller")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("포신이 발사 시 로컬 Z축 음수 방향으로 밀리는 거리입니다.")]
    private float recoilDistance = 0.3f;

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

    [Header("State Debug")]
    [SerializeField] private TurretState currentState = TurretState.Aiming;
    [SerializeField] private float nextFireTime = 0f;

    // 추가: 현재 선택된 타겟
    private Transform currentTarget;

    // 추가: 타겟 갱신 타이머
    private float targetRefreshTimer;

    // 추가: 같은 타겟을 매 프레임 다시 적용하지 않기 위한 캐시
    private Transform lastAppliedTarget;

    // 추가: 재장전 코루틴 참조
    private Coroutine reloadCoroutine;

    // 추가: 외부에서 현재 타겟과 상태를 확인할 수 있게 하는 읽기 전용 프로퍼티
    public Transform CurrentTarget => currentTarget;
    public TurretState CurrentState => currentState;

    public event Action<TurretState> OnTurretStateChanged;

    private void Awake()
    {
        ResolveReference();
        ClampInspectorValues();

        /*
         * 변경점:
         * OnValidate는 에디터에서 값이 바뀔 때 주로 호출됩니다.
         * 게임 실행 시점에도 Manager의 Inspector 값을 실제 컨트롤러들에게 전달하기 위해
         * Awake에서 한 번 더 적용합니다.
         */
        ApplyControllerSettings(refillAmmo: true);
    }

    private void OnEnable()
    {
        /*
         * 변경점:
         * 터렛 Enable 시점의 초기화는 TurretManager가 담당합니다.
         * 현재 탄환을 최대 장탄수로 채우고, 상태를 조준중으로 되돌립니다.
         */
        StopReloadCoroutine();

        if (turretHead != null)
        {
            turretHead.RefillAmmo();
        }

        nextFireTime = 0f;
        SetState(TurretState.Aiming);
    }

    private void OnDisable()
    {
        StopReloadCoroutine();
        SetState(TurretState.Aiming);
    }

    // ================================
    // 추가: 런타임 중 타겟 갱신 + 상태 갱신
    // ================================
    private void Update()
    {
        UpdateTargetSelection();
        UpdateTurretState();
        FireWhenReady();
    }

    private void OnValidate()
    {
        ClampInspectorValues();
        ApplyControllerSettings(refillAmmo: false);
    }

    private void ClampInspectorValues()
    {
        yawSpeed = Mathf.Max(0f, yawSpeed);
        yawStopAngle = Mathf.Max(0f, yawStopAngle);

        minPitch = Mathf.Clamp(minPitch, -90f, 90f);
        maxPitch = Mathf.Clamp(maxPitch, -90f, 90f);
        pitchSpeed = Mathf.Max(0f, pitchSpeed);
        pitchStopAngle = Mathf.Max(0f, pitchStopAngle);

        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        fireInterval = Mathf.Max(0f, fireInterval);
        projectileLifeTime = Mathf.Max(0f, projectileLifeTime);

        maxMagazine = Mathf.Max(1, maxMagazine);
        reloadTime = Mathf.Max(0f, reloadTime);

        recoilDistance = Mathf.Max(0f, recoilDistance);

        fireAngleThreshold = Mathf.Max(0f, fireAngleThreshold);

        targetSearchRange = Mathf.Max(0f, targetSearchRange);
        targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
    }

    private void ApplyControllerSettings(bool refillAmmo)
    {
        if (turretHeadYawPivot != null)
        {
            ChangeYawSpeed(yawSpeed);
            ChangeYawStopAngle(yawStopAngle);
        }

        if (turretBarrelPitchPivot != null)
        {
            ChangeMinPitch(minPitch);
            ChangeMaxPitch(maxPitch);
            ChangePitchSpeed(pitchSpeed);
            ChangePitchStopAngle(pitchStopAngle);
        }

        if (turretHead != null)
        {
            ChangeProjectileSpeed(projectileSpeed);
            ChangeFireInterval(fireInterval);
            ChangeProjectileLifeTime(projectileLifeTime);
            ChangeMaxMagazine(maxMagazine, refillAmmo);
            ChangeReloadTime(reloadTime);
        }

        if (aimChecker != null)
        {
            ChangeFireAngleThreshold(fireAngleThreshold);
        }

        if (barrelRecoil != null)
        {
            /*
             * 변경점:
             * TurretManager의 Inspector에서 recoilDistance를 수정하면
             * ApplyControllerSettings()를 통해 BarrelRecoil에 즉시 반영됩니다.
             *
             * 반동 전체 시간은 MissileLaunch.FireInterval 값을 기준으로 동기화합니다.
             */
            ChangeRecoilDistance(recoilDistance);
            ChangeRecoilDuration(turretHead != null ? turretHead.FireInterval : fireInterval);
        }
    }

    // ================================
    // 추가: enum 기반 터렛 상태 갱신
    // ================================
    private void UpdateTurretState()
    {
        /*
         * 변경점:
         * 재장전 상태는 코루틴이 끝날 때까지 유지합니다.
         * 이전 구조처럼 isReloading bool을 직접 보지 않고 currentState로 판별합니다.
         */
        if (currentState == TurretState.Reloading)
            return;

        if (turretHead == null)
        {
            SetState(TurretState.Aiming);
            return;
        }

        if (turretHead.HasAmmo == false)
        {
            BeginReload();
            return;
        }

        /*
         * CheckAiming은 상태머신이 아니라 조준 완료 여부를 계산하는 입력 역할만 합니다.
         * 실제 상태 결정은 TurretManager가 담당합니다.
         */
        bool isAimComplete = aimChecker != null && aimChecker.CanFire;

        if (isAimComplete == false)
        {
            SetState(TurretState.Aiming);
            return;
        }

        if (Time.time < nextFireTime)
        {
            SetState(TurretState.Aiming);
            return;
        }

        SetState(TurretState.FireReady);
    }

    private void FireWhenReady()
    {
        if (currentState != TurretState.FireReady)
            return;

        if (turretHead == null)
        {
            SetState(TurretState.Aiming);
            return;
        }

        bool launchSucceeded = turretHead.TryLaunch();

        if (launchSucceeded == false)
        {
            SetState(TurretState.Aiming);
            return;
        }

        /*
         * 변경점:
         * 발사체 생성이 성공한 직후 포신 반동 연출을 실행합니다.
         * 반동 시간은 MissileLaunch.FireInterval을 사용합니다.
         */
        PlayBarrelRecoil();

        nextFireTime = Time.time + turretHead.FireInterval;

        if (turretHead.HasAmmo == false)
        {
            BeginReload();
            return;
        }

        /*
         * 발사 직후에는 fireInterval이 지나기 전까지 다시 발사하면 안 됩니다.
         * 별도의 Cooldown 상태를 만들지 않는 조건이므로, 이 시간은 Aiming 상태로 처리합니다.
         */
        SetState(TurretState.Aiming);
    }

    private void BeginReload()
    {
        if (currentState == TurretState.Reloading)
            return;

        if (reloadCoroutine != null)
            return;

        SetState(TurretState.Reloading);
        reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        float waitTime = turretHead != null ? turretHead.ReloadTime : reloadTime;

        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        if (turretHead != null)
        {
            turretHead.RefillAmmo();
        }

        reloadCoroutine = null;
        nextFireTime = Time.time;

        /*
         * 재장전이 끝난 직후에도 즉시 FireReady로 바꾸지 않습니다.
         * 다음 Update에서 CheckAiming.CanFire, 탄환, 발사 간격을 다시 평가해서 상태를 결정합니다.
         */
        SetState(TurretState.Aiming);
    }

    private void StopReloadCoroutine()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }
    }


    private void PlayBarrelRecoil()
    {
        if (barrelRecoil == null || turretHead == null)
        {
            return;
        }

        /*
         * 변경점:
         * 반동이 동작하는 시간은 MissileLaunch.FireInterval 값을 사용합니다.
         * fireInterval을 런타임에 바꿔도 발사 시점마다 최신 값을 적용합니다.
         */
        barrelRecoil.SetRecoilDuration(turretHead.FireInterval);
        barrelRecoil.PlayRecoil();
    }

    private void SetState(TurretState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        OnTurretStateChanged?.Invoke(currentState);
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
    }

    public void ChangeYawSpeed(float value)
    {
        if (turretHeadYawPivot != null)
        {
            turretHeadYawPivot.SetYawSpeed(value);
        }
    }

    public void ChangeYawStopAngle(float value)
    {
        if (turretHeadYawPivot != null)
        {
            turretHeadYawPivot.SetYawStopAngle(value);
        }
    }

    public void ChangeMinPitch(float value)
    {
        if (turretBarrelPitchPivot != null)
        {
            turretBarrelPitchPivot.SetMinPitch(value);
        }
    }

    public void ChangeMaxPitch(float value)
    {
        if (turretBarrelPitchPivot != null)
        {
            turretBarrelPitchPivot.SetMaxPitch(value);
        }
    }

    public void ChangePitchSpeed(float value)
    {
        if (turretBarrelPitchPivot != null)
        {
            turretBarrelPitchPivot.SetPitchSpeed(value);
        }
    }

    public void ChangePitchStopAngle(float value)
    {
        if (turretBarrelPitchPivot != null)
        {
            turretBarrelPitchPivot.SetPitchStopAngle(value);
        }
    }

    public void ChangeProjectileSpeed(float value)
    {
        if (turretHead != null)
        {
            turretHead.SetProjectileSpeed(value);
        }
    }

    public void ChangeFireInterval(float value)
    {
        if (turretHead != null)
        {
            turretHead.SetFireInterval(value);
        }

        /*
         * 변경점:
         * 반동 시간은 MissileLaunch.FireInterval을 따라가야 하므로
         * 발사 간격이 변경될 때 BarrelRecoil에도 최신 시간을 전달합니다.
         */
        if (barrelRecoil != null)
        {
            float recoilDuration = turretHead != null ? turretHead.FireInterval : value;
            barrelRecoil.SetRecoilDuration(recoilDuration);
        }
    }

    public void ChangeProjectileLifeTime(float value)
    {
        if (turretHead != null)
        {
            turretHead.SetProjectileLifeTime(value);
        }
    }

    public void ChangeMaxMagazine(int value, bool refillAmmo = false)
    {
        if (turretHead != null)
        {
            turretHead.SetMaxMagazine(value, refillAmmo);
        }
    }

    public void ChangeReloadTime(float value)
    {
        if (turretHead != null)
        {
            turretHead.SetReloadTime(value);
        }
    }

    public void ChangeFireAngleThreshold(float value)
    {
        if (aimChecker != null)
        {
            aimChecker.SetFireAngleThreshold(value);
        }
    }

    public void ChangeRecoilDistance(float value)
    {
        if (barrelRecoil != null)
        {
            barrelRecoil.SetRecoilDistance(value);
        }
    }

    public void ChangeRecoilDuration(float value)
    {
        if (barrelRecoil != null)
        {
            barrelRecoil.SetRecoilDuration(value);
        }
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
        }

        if (turretBarrelPitchPivot == null)
        {
            turretBarrelPitchPivot = GetComponent<RotateTargetPitch>();
        }
        if (turretBarrelPitchPivot == null)
        {
            Debug.LogWarning("[Turret Manager] Rotate Target Pitch 탐색 불가");
        }

        if (turretHead == null)
        {
            turretHead = GetComponent<MissileLaunch>();
        }
        if (turretHead == null)
        {
            Debug.LogWarning("[Turret Manager] Missile Launch 탐색 불가");
        }

        if (aimChecker == null)
        {
            aimChecker = GetComponent<CheckAiming>();
        }
        if (aimChecker == null)
        {
            Debug.LogWarning("[Turret Manager] Check Aiming 탐색 불가");
        }

        if (barrelRecoil == null)
        {
            barrelRecoil = GetComponentInChildren<BarrelRecoil>();
        }
    }
}
