# Turret / Enemy Target System Logic 정리

## 1. 전체 구조 요약

이 시스템은 크게 두 축으로 나뉩니다.

```text
[Turret System]
TurretManager
├─ RotateTargetYaw
├─ RotateTargetPitch
├─ CheckAiming
├─ MissileLaunch
└─ BarrelRecoil

[Enemy / Projectile System]
EnemySpawner
├─ EnemyTarget
├─ EnemyMove
└─ ProjectileMover
```

핵심 흐름은 다음과 같습니다.

```text
EnemySpawner가 Enemy를 ObjectPool에서 꺼내 스폰
→ EnemyTarget이 ActiveTargets 목록에 등록됨
→ TurretManager가 ActiveTargets에서 타겟 선택
→ RotateTargetYaw / RotateTargetPitch가 타겟을 향해 회전
→ CheckAiming이 조준 완료 여부 계산
→ TurretManager 상태가 FireReady가 되면 MissileLaunch.TryLaunch()
→ 발사 성공 시 BarrelRecoil.PlayRecoil()
→ ProjectileMover가 전진 이동
→ Projectile이 EnemyTarget과 충돌
→ EnemyTarget.ApplyDamage()
→ 체력 0 이하 시 DeathReactionRoutine()
→ Enemy 정지 / 붉은색 변경 / 피격 판정 비활성화
→ 일정 시간 후 ObjectPool로 반환
```

---

## 2. 스크립트별 역할

| 스크립트 | 주요 역할 |
|---|---|
| `TurretManager.cs` | 터렛 전체 제어, 타겟 선택, 상태머신, 재장전 코루틴, 발사 요청, 반동 요청 |
| `RotateTargetYaw.cs` | 터렛 머리 또는 본체를 좌우 방향으로 회전 |
| `RotateTargetPitch.cs` | 포신을 위아래 방향으로 회전, Pitch 각도 제한 적용 |
| `CheckAiming.cs` | 현재 Muzzle 방향이 타겟을 향하고 있는지 판정 |
| `MissileLaunch.cs` | 발사체 생성, 탄환 수 관리, ProjectileMover 초기화 |
| `BarrelRecoil.cs` | 발사 성공 시 포신 메쉬를 로컬 Z축으로 뒤로 밀었다가 복귀 |
| `ProjectileMover.cs` | 발사체 이동, 수명 관리, Enemy 충돌 시 데미지 전달 |
| `EnemySpawner.cs` | Enemy ObjectPool 생성/관리, 주기적 스폰 |
| `EnemyTarget.cs` | Enemy 체력, 타겟 등록, 데미지 처리, 사망 연출, Pool 반환 |
| `EnemyMove.cs` | Enemy 전진 이동, 수명 종료 시 Pool 반환 |
| `RotateAround.cs` | 지정한 중심을 기준으로 오브젝트를 회전시키는 테스트용 보조 스크립트 |

---

# 3. Turret System

## 3.1 TurretManager.cs

`TurretManager`는 터렛 시스템의 중심 관리자입니다.

주요 책임은 다음과 같습니다.

```text
1. Inspector 설정값 보정
2. 하위 컨트롤러에 설정값 전달
3. EnemyTarget.ActiveTargets에서 타겟 선택
4. 선택된 타겟을 회전 컨트롤러와 조준 판정 컨트롤러에 전달
5. 터렛 상태머신 관리
6. 발사 가능 상태일 때 MissileLaunch.TryLaunch() 호출
7. 탄환이 0이면 재장전 코루틴 실행
8. 발사 성공 시 BarrelRecoil.PlayRecoil() 호출
```

---

## 3.2 터렛 상태머신

`TurretManager`는 다음 enum으로 터렛 상태를 관리합니다.

```csharp
public enum TurretState
{
    Aiming = 0,
    FireReady = 1,
    Reloading = 2
}
```

각 상태의 의미는 다음과 같습니다.

| 상태 | 의미 |
|---|---|
| `Aiming` | 타겟을 찾았지만 아직 발사 조건을 모두 만족하지 못한 상태 |
| `FireReady` | 조준 완료, 탄환 있음, 발사 간격 경과 조건을 모두 만족한 상태 |
| `Reloading` | 탄환이 0이 되어 재장전 코루틴이 진행 중인 상태 |

상태 전환 흐름은 다음과 같습니다.

```text
Aiming
├─ 탄환 없음
│  └─ Reloading
│
├─ 조준 미완료
│  └─ Aiming 유지
│
├─ 발사 간격 대기 중
│  └─ Aiming 유지
│
└─ 조준 완료 + 탄환 있음 + 발사 간격 경과
   └─ FireReady

FireReady
├─ TryLaunch 실패
│  └─ Aiming
│
├─ TryLaunch 성공 + 탄환 남음
│  └─ Aiming
│
└─ TryLaunch 성공 + 탄환 0
   └─ Reloading

Reloading
└─ reloadTime 대기 후 탄환 보충
   └─ Aiming
```

---

## 3.3 TurretManager.Update() 흐름

`Update()`에서는 세 가지 처리가 순서대로 실행됩니다.

```csharp
private void Update()
{
    UpdateTargetSelection();
    UpdateTurretState();
    FireWhenReady();
}
```

의미는 다음과 같습니다.

| 순서 | 메서드 | 역할 |
|---|---|---|
| 1 | `UpdateTargetSelection()` | 현재 타겟을 갱신 |
| 2 | `UpdateTurretState()` | 조준, 탄환, 발사 간격을 기준으로 상태 갱신 |
| 3 | `FireWhenReady()` | `FireReady` 상태이면 실제 발사 시도 |

이 순서가 중요한 이유는, 먼저 타겟을 정하고 조준 상태를 갱신한 뒤에야 발사 가능 여부를 판단할 수 있기 때문입니다.

---

## 3.4 타겟 선택 로직

`TurretManager`는 `EnemyTarget.ActiveTargets` 목록을 기준으로 타겟을 선택합니다.

지원하는 선택 정책은 다음 세 가지입니다.

```csharp
private enum TargetSelectionPolicy
{
    Nearest = 0,
    First = 1,
    Random = 2,
}
```

| 정책 | 동작 |
|---|---|
| `Nearest` | 터렛과 가장 가까운 Enemy 선택 |
| `First` | ActiveTargets 목록에서 먼저 발견된 Enemy 선택 |
| `Random` | 조건을 만족하는 Enemy 중 무작위 선택 |

타겟 후보는 다음 조건을 통과해야 합니다.

```text
1. EnemyTarget이 null이 아님
2. selfTeamId와 같은 TeamId가 아님
3. targetSearchRange 안에 있음
```

`selfTeamId < 0`이면 팀 필터를 사용하지 않습니다.

---

## 3.5 선택된 타겟 전달

타겟이 선택되면 다음 컴포넌트에 전달됩니다.

```text
RotateTargetYaw.SetTarget(currentTarget)
RotateTargetPitch.SetTarget(currentTarget)
CheckAiming.SetTarget(currentTarget)
```

`MissileLaunch`에는 타겟을 직접 전달하지 않습니다.

이유는 `MissileLaunch`가 타겟 추적을 하지 않고, 단순히 `muzzle.position`, `muzzle.rotation`을 기준으로 발사체를 생성하기 때문입니다.

---

## 3.6 재장전 로직

탄환이 없으면 `BeginReload()`가 호출됩니다.

```text
탄환 0
→ BeginReload()
→ SetState(Reloading)
→ ReloadRoutine() 코루틴 시작
→ reloadTime 대기
→ turretHead.RefillAmmo()
→ SetState(Aiming)
```

재장전 도중에는 `UpdateTurretState()`에서 바로 return되므로 발사 상태로 넘어가지 않습니다.

```csharp
if (currentState == TurretState.Reloading)
    return;
```

---

## 3.7 BarrelRecoil 연동

발사 성공 직후 `PlayBarrelRecoil()`이 호출됩니다.

```text
TryLaunch 성공
→ BarrelRecoil.SetRecoilDuration(turretHead.FireInterval)
→ BarrelRecoil.PlayRecoil()
```

반동 거리는 `TurretManager`의 Inspector 값인 `recoilDistance`로 관리합니다.

```csharp
[SerializeField]
private float recoilDistance = 0.3f;
```

`ApplyControllerSettings()`에서 이 값을 `BarrelRecoil`에 전달합니다.

```text
TurretManager.recoilDistance
→ BarrelRecoil.SetRecoilDistance()

MissileLaunch.FireInterval
→ BarrelRecoil.SetRecoilDuration()
```

---

# 4. BarrelRecoil.cs

`BarrelRecoil`은 포신 반동 연출 전용 컴포넌트입니다.

실제 발사 가능 여부, 탄환 수, 재장전 여부는 판단하지 않습니다.

## 4.1 책임

```text
1. 반동을 적용할 Transform 참조
2. 원래 localPosition 저장
3. 발사 시 로컬 Z축 음수 방향으로 이동
4. 지정 시간 동안 다시 원위치 복귀
5. 비활성화 시 원위치 복구
```

---

## 4.2 반동 코루틴 흐름

```text
PlayRecoil()
→ 기존 recoilCoroutine 중단
→ RecoilRoutine() 시작

RecoilRoutine()
→ originalLocalPosition 저장값 사용
→ originalLocalPosition + Vector3.back * recoilDistance 위치까지 이동
→ 다시 originalLocalPosition으로 복귀
→ recoilCoroutine = null
```

시간 배분은 다음과 같습니다.

```text
전체 recoilDuration
├─ 앞 절반: 원위치 → 뒤로 밀림
└─ 뒤 절반: 뒤로 밀림 → 원위치
```

예를 들어 `fireInterval = 0.5f`이면:

```text
0.25초 동안 뒤로 이동
0.25초 동안 앞으로 복귀
```

---

## 4.3 로컬 Z축 기준

반동 위치는 다음 방식으로 계산됩니다.

```csharp
Vector3 recoilPosition = originalLocalPosition + Vector3.back * recoilDistance;
```

즉, 포신 메쉬의 로컬 기준 `-Z` 방향으로 밀립니다.

주의할 점은 모델의 발사 방향이 반드시 로컬 `+Z` 방향으로 잡혀 있어야 자연스럽게 보인다는 것입니다.  
모델 축이 다르면 반동 방향도 다르게 보일 수 있습니다.

---

# 5. MissileLaunch.cs

`MissileLaunch`는 실제 발사체 생성과 탄환 수 관리를 담당합니다.

터렛 상태 판단은 하지 않습니다.  
상태 판단은 `TurretManager`가 담당합니다.

## 5.1 주요 필드

```text
muzzle
missilePrefab
usePrefab
projectileSpeed
fireInterval
projectileLifeTime
maxMagazine
currentAmmo
reloadTime
projectileDamage
shooterTeamId
```

## 5.2 TryLaunch() 흐름

```text
TryLaunch()
├─ currentAmmo <= 0이면 실패
├─ CreateProjectileObject()
├─ ProjectileMover 확인
├─ ProjectileMover.Initialize(...)
├─ currentAmmo 1 감소
├─ OnAmmoChanged 이벤트 호출
├─ OnProjectileLaunched 이벤트 호출
└─ true 반환
```

중요한 점은 발사체 생성과 초기화가 성공했을 때만 탄환을 감소시킨다는 것입니다.

```text
Prefab 누락
ProjectileMover 누락
발사체 생성 실패
→ 탄환 감소 없음
```

---

## 5.3 발사체 생성 방식

`usePrefab` 값에 따라 두 가지 방식으로 발사체를 생성합니다.

| usePrefab | 생성 방식 |
|---|---|
| `true` | `missilePrefab`을 `Instantiate()` |
| `false` | `GameObject.CreatePrimitive()`로 임시 발사체 생성 |

Prefab 방식은 실제 프로젝트에서 사용할 발사체 프리팹을 기준으로 합니다.

Primitive 방식은 테스트용에 가깝습니다.

---

## 5.4 탄환 관리

| 메서드 | 역할 |
|---|---|
| `SetMaxMagazine(int, bool)` | 최대 장탄수 설정, 필요 시 현재 탄환 보충 |
| `RefillAmmo()` | 현재 탄환을 최대 장탄수로 채움 |
| `SetReloadTime(float)` | 재장전 시간 설정 |
| `HasAmmo` | 현재 탄환이 1 이상인지 확인 |

`MissileLaunch`는 재장전 코루틴을 직접 실행하지 않습니다.  
재장전 시점과 상태 관리는 `TurretManager`가 담당합니다.

---

# 6. CheckAiming.cs

`CheckAiming`은 현재 Muzzle이 타겟을 향해 조준되었는지 계산합니다.

## 6.1 역할

```text
1. target과 muzzle 사이의 방향 계산
2. Yaw 각도 차이 계산
3. Pitch 각도 차이 계산
4. 두 각도가 fireAngleThreshold 이하이면 canFire = true
```

## 6.2 Yaw 판정

Yaw는 수평 방향 회전입니다.

```text
directionToTarget.y = 0
muzzleForward.y = 0
Vector3.SignedAngle(..., Vector3.up)
```

즉, 위아래 차이는 무시하고 좌우 방향만 봅니다.

---

## 6.3 Pitch 판정

Pitch는 위아래 회전입니다.

```text
pitchAxis = muzzle.right
forward와 target 방향을 pitchAxis 기준 평면에 투영
SignedAngle로 위아래 각도 차이 계산
```

---

## 6.4 CanFire의 의미

`CanFire`는 실제 발사 명령이 아닙니다.

```text
CheckAiming.CanFire
→ 조준이 완료되었는지 알려주는 입력값

TurretManager.CurrentState
→ 실제 터렛 상태
```

즉, 최종 발사 여부는 `TurretManager`가 다음 조건을 모두 확인해서 결정합니다.

```text
CheckAiming.CanFire == true
MissileLaunch.HasAmmo == true
Time.time >= nextFireTime
currentState != Reloading
```

---

# 7. RotateTargetYaw.cs

`RotateTargetYaw`는 터렛을 좌우 방향으로 회전시킵니다.

## 7.1 처리 흐름

```text
Update()
→ UpdateRotationYawToTarget()
→ rotationDirection이 있으면 yawPivot.Rotate()
```

## 7.2 Yaw 방향 계산

```text
1. yawPivot에서 target까지의 방향 계산
2. y 값을 0으로 만들어 수평 방향만 사용
3. yawPivot.forward도 y 값을 0으로 만듦
4. SignedAngle로 좌우 각도 계산
5. 각도 차이가 yawStopAngle 이하이면 정지
6. 타겟이 오른쪽이면 +Y, 왼쪽이면 -Y 방향 회전
```

---

# 8. RotateTargetPitch.cs

`RotateTargetPitch`는 포신을 위아래 방향으로 회전시킵니다.

## 8.1 처리 흐름

```text
Update()
→ UpdateRotationPitchToTarget()
→ RotatePitchWithLimit()
```

## 8.2 Pitch 방향 계산

```text
1. pitchPivot에서 target까지의 방향 계산
2. pitchPivot.right를 Pitch 회전축으로 사용
3. 현재 forward와 target 방향을 Pitch 평면에 투영
4. SignedAngle로 Pitch 각도 차이 계산
5. pitchStopAngle 이하이면 정지
6. minPitch / maxPitch 범위 안에서 회전
```

## 8.3 각도 제한

`RotatePitchWithLimit()`은 현재 로컬 X축 각도를 기준으로 다음 각도를 제한합니다.

```csharp
float nextPitch = Mathf.Clamp(
    currentPitch + pitchDelta,
    minPitch,
    maxPitch
);
```

이 덕분에 포신이 지나치게 위나 아래로 꺾이지 않습니다.

---

# 9. ProjectileMover.cs

`ProjectileMover`는 발사체의 이동, 수명, 충돌 처리를 담당합니다.

## 9.1 Initialize()

`MissileLaunch`가 발사체를 생성한 직후 다음 값을 전달합니다.

```text
speed
lifeTime
damage
shooterTeamId
```

## 9.2 Update() 흐름

```text
transform.forward 방향으로 이동
remainingLifeSeconds 감소
수명이 0 이하가 되면 Destroy(gameObject)
```

이동 방식은 Rigidbody velocity가 아니라 Transform 직접 이동입니다.

```csharp
transform.position += transform.forward * (moveSpeedUnitsPerSecond * Time.deltaTime);
```

## 9.3 충돌 처리

```text
OnTriggerEnter(Collider other)
→ other.GetComponentInParent<EnemyTarget>()
→ EnemyTarget이 없으면 무시
→ 같은 TeamId면 무시
→ EnemyTarget.ApplyDamage(damageAmount)
→ Projectile Destroy
```

`EnemyTarget.ApplyDamage()` 내부에서 사망 연출 중인 Enemy인지 다시 검사하므로, 중복 피격도 방어합니다.

---

# 10. EnemySpawner.cs

`EnemySpawner`는 Enemy의 생성과 Pool 반환을 관리합니다.

## 10.1 ObjectPool 구조

```csharp
private ObjectPool<EnemyTarget> enemyPool;
```

Pool 콜백은 다음 역할을 가집니다.

| 콜백 | 역할 |
|---|---|
| `CreateEnemy()` | 새 Enemy 인스턴스 생성 |
| `OnGetEnemy()` | Pool에서 꺼낼 때 호출. 실제 활성화는 하지 않음 |
| `OnReleaseEnemy()` | Pool로 반환될 때 비활성화 |
| `OnDestroyEnemy()` | Pool 용량 초과 등으로 실제 Destroy 필요 시 호출 |

---

## 10.2 스폰 흐름

```text
Start()
→ initialSpawnCount만큼 TryGetOneEnemy()

Update()
→ 비활성/삭제된 Enemy 목록 정리
→ spawnIntervalSeconds 확인
→ maxAliveCount 확인
→ TryGetOneEnemy()
```

## 10.3 TryGetOneEnemy() 흐름

```text
1. EnemySpawner 자신의 위치를 spawnPosition으로 사용
2. 기본 회전은 EnemySpawner 자신의 회전
3. lookAtCenter가 있으면 해당 방향을 바라보도록 회전 계산
4. enemyPool.Get()으로 EnemyTarget 획득
5. EnemyTarget.Initialize(...) 호출
6. EnemyMove.Initialize(...) 호출
7. aliveEnemies에 등록
```

`EnemyTarget.Initialize()`에는 다음 값들이 전달됩니다.

```text
spawnPosition
spawnRotation
enemyRoot
ReleaseEnemyToPool 콜백
deathReactionDurationSeconds
```

즉, 사망 연출 시간은 `EnemySpawner`의 Inspector에서 관리됩니다.

---

# 11. EnemyTarget.cs

`EnemyTarget`은 Enemy의 체력, 타겟 등록, 데미지, 사망 연출, Pool 반환을 담당합니다.

## 11.1 ActiveTargets 목록

```csharp
private static readonly List<EnemyTarget> ActiveTargetsInternal
```

활성화된 Enemy는 `OnEnable()`에서 ActiveTargets에 등록됩니다.

```text
Enemy 활성화
→ ActiveTargetsInternal.Add(this)
```

비활성화되면 제거됩니다.

```text
Enemy 비활성화
→ ActiveTargetsInternal.Remove(this)
```

`TurretManager`는 이 목록을 기준으로 타겟을 찾습니다.

---

## 11.2 Initialize() 흐름

Pool에서 꺼낸 Enemy는 `Initialize()`를 통해 초기화됩니다.

```text
1. Pool 반환 콜백 저장
2. deathReactionDurationSeconds 저장
3. ResetRuntimeState()
4. 부모 설정
5. 위치/회전 설정
6. gameObject.SetActive(true)
```

`ResetRuntimeState()`는 Pool 재사용 시 이전 상태를 제거합니다.

```text
isReleased = false
isDamageLocked = false
isDeathReactionPlaying = false
currentHealth = maxHealth
색상 복구
Collider 활성화
EnemyMove 활성화
```

---

## 11.3 ApplyDamage() 흐름

```text
ApplyDamage(damageAmount)
├─ 이미 Pool 반환 처리됨 → false
├─ 사망 연출 중이라 피격 잠금 상태 → false
├─ damageAmount <= 0 → false
├─ currentHealth 감소
├─ 체력 0 이하 → Die()
└─ true
```

---

## 11.4 사망 연출 코루틴

기존 방식은 사망 위치에 새 Sphere를 생성하는 방식이었습니다.  
현재 방식은 Enemy 자신을 이용해 사망 연출을 수행합니다.

```text
Die()
→ DeathReactionRoutine()
```

`DeathReactionRoutine()` 흐름은 다음과 같습니다.

```text
1. isDeathReactionPlaying = true
2. isDamageLocked = true
3. ActiveTargetsInternal에서 제거
4. EnemyMove 비활성화
5. Rigidbody 속도 정지
6. Collider 비활성화
7. Renderer 색상을 붉은색으로 변경
8. deathReactionDurationSeconds 대기
9. ReleaseToPool()
```

핵심은 사망 연출 중 Enemy가 더 이상 타겟으로 선택되지 않고, 추가 피격도 받지 않게 하는 것입니다.

---

## 11.5 Kinematic Rigidbody 경고 방지

`StopRigidbodyMotion()`은 Rigidbody가 있는 경우 속도를 0으로 만듭니다.

다만 Kinematic Rigidbody는 `linearVelocity`, `angularVelocity` 설정을 지원하지 않으므로 먼저 검사합니다.

```text
Rigidbody 없음
→ return

Rigidbody.isKinematic == true
→ return

Non-Kinematic Rigidbody
→ linearVelocity = Vector3.zero
→ angularVelocity = Vector3.zero
```

이 처리가 없으면 Unity 6에서 다음 경고가 발생할 수 있습니다.

```text
Setting linear velocity of a kinematic body is not supported.
Setting angular velocity of a kinematic body is not supported.
```

---

# 12. EnemyMove.cs

`EnemyMove`는 Enemy의 단순 전진 이동과 수명 관리를 담당합니다.

## 12.1 이동

```csharp
transform.position += transform.forward * (enemyMoveSpeed * Time.deltaTime);
```

즉, Enemy는 자신의 `transform.forward` 방향으로 이동합니다.

## 12.2 수명 종료

```text
remainingLifeTime 감소
→ 0 이하
→ EnemyTarget이 있으면 ReleaseToPool()
→ EnemyTarget이 없으면 Destroy(gameObject)
```

Pool 구조에서는 `Destroy()`가 아니라 `EnemyTarget.ReleaseToPool()`을 호출하는 것이 핵심입니다.

---

# 13. RotateAround.cs

`RotateAround`는 지정된 `center`를 기준으로 오브젝트를 회전시키는 보조 스크립트입니다.

```text
center가 없으면 로그 출력 후 return
center가 있으면 RotateAround(center.position, Vector3.up, speed * Time.deltaTime)
```

주로 테스트용 이동 타겟이나 회전 오브젝트를 만들 때 사용할 수 있습니다.

---

# 14. 전체 런타임 시나리오

## 14.1 Enemy 생성

```text
EnemySpawner.Awake()
→ ObjectPool<EnemyTarget> 생성

EnemySpawner.Start()
→ 초기 Enemy 생성

EnemySpawner.Update()
→ 주기적으로 Enemy 추가 생성
```

## 14.2 Enemy 등록

```text
EnemyTarget.Initialize()
→ 위치/회전/Pool 콜백 설정
→ gameObject.SetActive(true)

EnemyTarget.OnEnable()
→ ActiveTargetsInternal에 등록
```

## 14.3 터렛 타겟 탐색

```text
TurretManager.UpdateTargetSelection()
→ EnemyTarget.ActiveTargets 조회
→ selectionPolicy에 따라 타겟 선택
→ RotateTargetYaw / RotateTargetPitch / CheckAiming에 타겟 전달
```

## 14.4 터렛 조준

```text
RotateTargetYaw.Update()
→ 좌우 회전

RotateTargetPitch.Update()
→ 상하 회전

CheckAiming.Update()
→ muzzle 기준으로 yaw/pitch 각도 차이 계산
→ fireAngleThreshold 이내면 CanFire = true
```

## 14.5 터렛 발사

```text
TurretManager.UpdateTurretState()
→ CanFire true
→ HasAmmo true
→ Time.time >= nextFireTime
→ FireReady

TurretManager.FireWhenReady()
→ MissileLaunch.TryLaunch()
→ 발사 성공
→ BarrelRecoil.PlayRecoil()
→ nextFireTime 갱신
```

## 14.6 발사체 충돌

```text
ProjectileMover.Update()
→ forward 방향 이동

ProjectileMover.OnTriggerEnter()
→ EnemyTarget 탐색
→ 팀 검사
→ ApplyDamage()
→ Projectile Destroy
```

## 14.7 Enemy 사망

```text
EnemyTarget.ApplyDamage()
→ 체력 0 이하
→ Die()
→ DeathReactionRoutine()

DeathReactionRoutine()
→ ActiveTargets에서 제거
→ 이동 정지
→ Collider 비활성화
→ 붉은색 변경
→ 일정 시간 대기
→ Pool 반환
```

---

# 15. Inspector 연결 체크리스트

## 15.1 TurretManager

| 필드 | 연결 대상 |
|---|---|
| `turretHeadYawPivot` | `RotateTargetYaw` |
| `turretBarrelPitchPivot` | `RotateTargetPitch` |
| `turretHead` | `MissileLaunch` |
| `aimChecker` | `CheckAiming` |
| `barrelRecoil` | `BarrelRecoil` |
| `projectileSpeed` | 발사체 속도 |
| `fireInterval` | 발사 간격 및 반동 전체 시간 |
| `maxMagazine` | 최대 장탄수 |
| `reloadTime` | 재장전 시간 |
| `recoilDistance` | 포신 반동 거리 |
| `fireAngleThreshold` | 조준 완료 허용 각도 |
| `selectionPolicy` | 타겟 선택 방식 |
| `targetSearchRange` | 타겟 탐색 거리 |

---

## 15.2 MissileLaunch

| 필드 | 연결 대상 |
|---|---|
| `muzzle` | 발사 위치와 방향 기준 Transform |
| `missilePrefab` | 실제 발사체 Prefab |
| `usePrefab` | Prefab 사용 여부 |
| `projectileDamage` | 발사체 데미지 |
| `shooterTeamId` | 발사자 팀 ID |

---

## 15.3 BarrelRecoil

| 필드 | 연결 대상 |
|---|---|
| `recoilTarget` | 실제로 뒤로 밀릴 포신 메쉬 Transform |
| `recoilDistance` | 보통 TurretManager에서 전달 |
| `recoilDuration` | 보통 MissileLaunch.FireInterval 기준으로 전달 |

---

## 15.4 EnemySpawner

| 필드 | 의미 |
|---|---|
| `enemyPrefab` | EnemyTarget이 붙은 Enemy Prefab |
| `enemyRoot` | 생성된 Enemy들의 부모 Transform |
| `defaultCapacity` | Pool 기본 용량 |
| `maxSize` | Pool 최대 용량 |
| `initialSpawnCount` | 시작 시 생성할 Enemy 수 |
| `maxAliveCount` | 동시에 살아있을 수 있는 Enemy 수 |
| `spawnIntervalSeconds` | Enemy 생성 간격 |
| `lookAtCenter` | 생성 시 Enemy가 바라볼 위치 |
| `enemyMoveSpeedUnitsPerSecond` | Enemy 이동 속도 |
| `enemyLifeTimeSeconds` | Enemy 수명 |
| `deathReactionDurationSeconds` | 사망 연출 지속 시간 |

---

## 15.5 EnemyTarget

| 필드 | 의미 |
|---|---|
| `teamId` | 팀 구분 값 |
| `maxHealth` | 최대 체력 |
| `aimPoint` | 터렛이 조준할 위치 |
| `reactionRenderers` | 사망 시 붉게 바꿀 Renderer 목록 |
| `deathReactionColor` | 사망 연출 색상 |

---

# 16. 현재 구조의 장점

## 16.1 책임 분리가 비교적 명확함

```text
TurretManager
→ 상태, 타겟, 발사 타이밍 관리

MissileLaunch
→ 실제 발사체 생성과 탄환 수 관리

CheckAiming
→ 조준 완료 여부 계산

BarrelRecoil
→ 포신 반동 연출

EnemySpawner
→ Pool과 스폰 관리

EnemyTarget
→ 체력, 피격, 사망, Pool 반환

ProjectileMover
→ 발사체 이동과 충돌
```

한 클래스가 모든 기능을 직접 처리하지 않기 때문에 수정 범위가 비교적 작습니다.

---

## 16.2 ObjectPool 구조에 적합함

Enemy는 사망 시 Destroy되지 않고 Pool로 반환됩니다.

```text
Enemy 사망
→ 사망 연출
→ ReleaseToPool()
→ EnemySpawner가 enemyPool.Release()
→ SetActive(false)
```

이 구조는 Enemy가 반복적으로 생성되는 터렛 테스트에서 GC와 Instantiate/Destroy 비용을 줄이는 데 유리합니다.

---

## 16.3 코루틴 사용 지점이 명확함

현재 코루틴은 크게 두 곳에서 사용됩니다.

| 코루틴 | 위치 | 역할 |
|---|---|---|
| `ReloadRoutine()` | `TurretManager` | 재장전 시간 대기 후 탄환 보충 |
| `RecoilRoutine()` | `BarrelRecoil` | 포신 반동 왕복 연출 |
| `DeathReactionRoutine()` | `EnemyTarget` | 사망 연출 후 Pool 반환 |

---

# 17. 주의할 점

## 17.1 CheckAiming의 Debug.Log

`CheckAiming.Update()`는 매 프레임 조준 상태 로그를 출력합니다.

```text
[CheckAiming] 발사 준비 완료
[CheckAiming] 발사 준비 중
```

테스트 단계에서는 유용하지만, Enemy와 터렛이 많아지면 Console 로그가 과도하게 쌓일 수 있습니다.  
실제 프로젝트에서는 Debug 옵션 bool을 두거나 로그를 제거하는 편이 좋습니다.

---

## 17.2 BarrelRecoil의 원래 위치 캐싱

`BarrelRecoil`은 `Awake()`에서 `originalLocalPosition`을 저장합니다.

따라서 런타임 중 포신의 기본 위치를 다른 코드가 변경한다면, 복구 위치가 기대와 다를 수 있습니다.

필요하다면 다음 기능을 추가할 수 있습니다.

```text
- 현재 위치를 새 원점으로 다시 저장하는 메서드
- recoilTarget 변경 시 originalLocalPosition 재캐싱
```

---

## 17.3 Projectile은 Pooling되지 않음

현재 Projectile은 수명이 끝나거나 충돌하면 `Destroy(gameObject)`됩니다.

Enemy는 Pooling되지만 Projectile은 아직 Pooling되지 않습니다.  
발사체 수가 많아질 경우 다음 단계에서는 `ProjectileMover`도 ObjectPool로 바꾸는 것이 좋습니다.

---

# 18. 개선 후보

## 18.1 TurretState에 Cooldown 추가 여부

현재 발사 간격 대기 중인 상태도 `Aiming`으로 처리됩니다.

```text
FireReady
→ 발사
→ fireInterval 동안 Aiming
```

더 명확하게 만들고 싶다면 다음 상태를 추가할 수 있습니다.

```csharp
Cooldown
```

다만 현재 미니 프로젝트 기준에서는 `Aiming`으로 처리해도 충분합니다.

---

## 18.2 타겟 상실 시 반응

현재 타겟이 사망하면 `ActiveTargets`에서 제거되고, 다음 타겟 갱신 주기에 새 타겟을 찾습니다.

`targetRefreshInterval`이 크면 타겟 변경 반응이 늦어질 수 있습니다.

빠른 반응이 필요하면 다음 방법을 고려할 수 있습니다.

```text
- targetRefreshInterval 감소
- EnemyTarget 사망 이벤트 발행
- TurretManager가 이벤트를 받아 즉시 타겟 재탐색
```

---

## 18.3 Renderer.material 사용

`EnemyTarget`은 사망 색상 변경을 위해 `targetRenderer.material`을 사용합니다.

이 방식은 Renderer별 Material 인스턴스를 만들 수 있습니다.  
Enemy 수가 많아지면 `MaterialPropertyBlock` 방식이 더 적합합니다.

현재 테스트 규모에서는 문제될 가능성이 낮지만, 다수 Enemy를 운영할 예정이라면 개선 후보입니다.

---

# 19. 최종 요약

현재 구조는 다음 기준으로 정리할 수 있습니다.

```text
터렛은 TurretManager가 상태를 관리한다.
MissileLaunch는 발사체 생성과 탄환 수만 담당한다.
CheckAiming은 조준 완료 여부만 계산한다.
BarrelRecoil은 발사 성공 후 포신 반동 연출만 담당한다.
EnemySpawner는 Enemy를 ObjectPool로 관리한다.
EnemyTarget은 체력, 데미지, 사망 연출, Pool 반환을 담당한다.
ProjectileMover는 발사체 이동과 충돌 데미지 전달을 담당한다.
```

전체 흐름은 다음 한 줄로 요약할 수 있습니다.

```text
EnemySpawner가 Enemy를 Pool에서 꺼내 등록하고,
TurretManager가 EnemyTarget 목록에서 타겟을 골라 조준한 뒤,
MissileLaunch로 발사체를 생성하고,
ProjectileMover가 충돌 시 EnemyTarget에 데미지를 전달하며,
EnemyTarget은 사망 연출 후 Pool로 반환된다.
```
