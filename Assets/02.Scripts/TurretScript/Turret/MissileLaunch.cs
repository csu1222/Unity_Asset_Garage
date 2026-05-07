using System;
using UnityEngine;

public class MissileLaunch : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private CheckAiming checkAiming;

    [Header("Projectile")]
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private PrimitiveType projectileShape = PrimitiveType.Sphere;
    [SerializeField] private float projectileScale = 0.3f;

    /*
     * 변경점:
     * true  : missilePrefab을 Instantiate해서 발사체를 생성합니다.
     * false : GameObject.CreatePrimitive()로 임시 발사체를 생성합니다.
     */
    [SerializeField] private bool usePrefab = true;

    [Header("Projectile Field")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private float projectileLifeTime = 3f;

    /*
     * 변경점:
     * ProjectileMover.Initialize()가 damage 값을 요구하므로
     * MissileLaunch에서도 발사체 데미지를 설정할 수 있도록 추가했습니다.
     */
    [SerializeField] private float projectileDamage = 10f;

    /*
     * 변경점:
     * ProjectileMover는 ownerTeamId와 EnemyTarget.TeamId를 비교해서
     * 같은 팀이면 데미지를 주지 않습니다.
     *
     * 예:
     * Player/Turret = 0
     * Enemy = 1
     */
    [SerializeField] private int shooterTeamId = 0;

    public float ProjectileSpeed => projectileSpeed;
    public float FireInterval => fireInterval;
    public float ProjectileLifeTime => projectileLifeTime;

    public event Action<float> OnProjectileSpeedChanged;
    public event Action<float> OnFireIntervalChanged;
    public event Action<float> OnProjectileLifeTimeChanged;

    [Header("Debug")]
    [SerializeField] private bool canLaunch = false;
    [SerializeField] private float nextFireTime = 0f;

    private void Awake()
    {
        if (muzzle == null)
        {
            Debug.LogWarning("[MissileLaunch] 머즐이 할당되지 않았습니다.");
            enabled = false;
            return;
        }

        if (checkAiming == null)
        {
            Debug.Log("[MissileLaunch] CheckAiming을 자식 컴포넌트에서 참조 시도합니다.");

            checkAiming = GetComponentInChildren<CheckAiming>();

            if (checkAiming == null)
            {
                Debug.LogWarning("[MissileLaunch] CheckAiming이 할당되지 않았습니다.");
                enabled = false;
                return;
            }
        }
    }

    private void Update()
    {
        // 1. 조준 완료 여부 확인
        canLaunch = checkAiming.CanFire;

        if (canLaunch == false)
            return;

        // 2. 발사 간격 체크
        if (Time.time < nextFireTime)
            return;

        // 3. 발사
        Launch();

        // 4. 다음 발사 가능 시간 갱신
        nextFireTime = Time.time + fireInterval;
    }

    private void Launch()
    {
        /*
         * 변경점:
         * 발사체 생성 로직을 별도 메서드로 분리했습니다.
         *
         * 이유:
         * Launch() 안에서 Prefab 생성 / Primitive 생성 / 컴포넌트 세팅을 모두 처리하면
         * 코드가 길어지고 읽기 어려워집니다.
         */
        GameObject projectileObject = CreateProjectileObject();

        if (projectileObject == null)
        {
            return;
        }

        /*
         * 변경점:
         * 기존 MissileProjectile 대신 ProjectileMover를 사용합니다.
         *
         * missilePrefab에 이미 ProjectileMover가 붙어 있다면 GetComponent로 가져오고,
         * Primitive 방식으로 생성한 경우에는 CreateProjectileObject()에서 추가됩니다.
         */
        ProjectileMover projectileMover = projectileObject.GetComponent<ProjectileMover>();

        if (projectileMover == null)
        {
            Debug.LogWarning("[MissileLaunch] 생성된 발사체에 ProjectileMover가 없습니다.");
            Destroy(projectileObject);
            return;
        }

        /*
         * 변경점:
         * ProjectileMover.Initialize()의 시그니처에 맞춰
         * speed, lifeTime, damage, shooterTeamId를 전달합니다.
         *
         * ProjectileMover는 transform.forward 방향으로 이동하므로
         * 별도의 fireDirection 값은 넘기지 않습니다.
         */
        projectileMover.Initialize(
            projectileSpeed,
            projectileLifeTime,
            projectileDamage,
            shooterTeamId
        );
    }

    private GameObject CreateProjectileObject()
    {
        if (usePrefab)
        {
            /*
             * 변경점:
             * usePrefab이 true이면 missilePrefab을 사용합니다.
             */
            if (missilePrefab == null)
            {
                Debug.LogWarning("[MissileLaunch] usePrefab이 true이지만 missilePrefab이 할당되지 않았습니다.");
                return null;
            }

            /*
             * 설명:
             * muzzle.position에서 생성하고,
             * muzzle.rotation을 그대로 넘깁니다.
             *
             * ProjectileMover는 transform.forward 방향으로 이동하므로
             * 발사체의 forward 방향이 muzzle.forward와 같아야 합니다.
             */
            GameObject projectileObject = Instantiate(
                missilePrefab,
                muzzle.position,
                muzzle.rotation
            );

            /*
             * 설명:
             * missilePrefab에 Rigidbody와 ProjectileMover가 이미 붙어 있다고 했으므로
             * 여기서는 AddComponent를 하지 않습니다.
             *
             * 단, 실수로 빠졌을 경우를 대비해 경고만 출력합니다.
             */
            if (projectileObject.GetComponent<Rigidbody>() == null)
            {
                Debug.LogWarning("[MissileLaunch] missilePrefab에 Rigidbody가 없습니다.");
            }

            if (projectileObject.GetComponent<ProjectileMover>() == null)
            {
                Debug.LogWarning("[MissileLaunch] missilePrefab에 ProjectileMover가 없습니다.");
            }

            return projectileObject;
        }
        else
        {
            /*
             * 변경점:
             * usePrefab이 false이면 Primitive 발사체를 생성합니다.
             */
            GameObject projectileObject = GameObject.CreatePrimitive(projectileShape);
            projectileObject.name = "Primitive Missile Projectile";

            projectileObject.transform.position = muzzle.position;
            projectileObject.transform.rotation = muzzle.rotation;
            projectileObject.transform.localScale = Vector3.one * projectileScale;

            /*
             * 변경점:
             * ProjectileMover의 OnTriggerEnter가 동작하려면
             * 보통 Collider 중 하나는 Trigger이고,
             * 충돌하는 두 오브젝트 중 하나에는 Rigidbody가 있어야 합니다.
             *
             * CreatePrimitive는 Collider를 자동으로 가지고 있으므로
             * 이 Collider를 Trigger로 바꿉니다.
             */
            Collider projectileCollider = projectileObject.GetComponent<Collider>();

            if (projectileCollider != null)
            {
                projectileCollider.isTrigger = true;
            }

            /*
             * 변경점:
             * Primitive 방식으로 생성한 발사체에는 Rigidbody가 없으므로 추가합니다.
             *
             * 주의:
             * 현재 ProjectileMover는 Rigidbody velocity가 아니라
             * transform.position += transform.forward * speed 방식으로 이동합니다.
             * 따라서 Rigidbody에 중력이나 물리 속도를 적용하지 않도록 설정합니다.
             */
            Rigidbody projectileRigidbody = projectileObject.AddComponent<Rigidbody>();
            projectileRigidbody.useGravity = false;
            projectileRigidbody.isKinematic = true;

            /*
             * 변경점:
             * Primitive 방식으로 생성한 발사체에는 ProjectileMover도 없으므로 추가합니다.
             */
            projectileObject.AddComponent<ProjectileMover>();

            return projectileObject;
        }
    }

    public void SetProjectileSpeed(float newProjectileSpeed)
    {
        if (projectileSpeed == newProjectileSpeed)
            return;

        projectileSpeed = newProjectileSpeed;

        OnProjectileSpeedChanged?.Invoke(projectileSpeed);
    }

    public void SetFireInterval(float newFireInterval)
    {
        if (fireInterval == newFireInterval)
            return;

        fireInterval = newFireInterval;

        OnFireIntervalChanged?.Invoke(fireInterval);
    }

    public void SetProjectileLifeTime(float newProjectileLifeTime)
    {
        if (projectileLifeTime == newProjectileLifeTime)
            return;

        projectileLifeTime = newProjectileLifeTime;

        OnProjectileLifeTimeChanged?.Invoke(projectileLifeTime);
    }
}