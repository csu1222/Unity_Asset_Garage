using System;
using UnityEngine;

public class MissileLaunch : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform muzzle;

    [Header("Projectile")]
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private PrimitiveType projectileShape = PrimitiveType.Sphere;
    [SerializeField] private float projectileScale = 0.3f;

    /*
     * true  : missilePrefab을 Instantiate해서 발사체를 생성합니다.
     * false : GameObject.CreatePrimitive()로 임시 발사체를 생성합니다.
     */
    [SerializeField] private bool usePrefab = true;

    [Header("Projectile Field")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private float projectileLifeTime = 3f;

    [Header("Magazine / Reload")]
    [SerializeField] private int maxMagazine = 10;
    [SerializeField] private int currentAmmo = 10;
    [SerializeField] private float reloadTime = 2f;

    /*
     * ProjectileMover.Initialize()가 damage 값을 요구하므로
     * MissileLaunch에서도 발사체 데미지를 설정할 수 있도록 유지합니다.
     */
    [SerializeField] private float projectileDamage = 10f;

    /*
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
    public int MaxMagazine => maxMagazine;
    public int CurrentAmmo => currentAmmo;
    public float ReloadTime => reloadTime;
    public bool HasAmmo => currentAmmo > 0;

    public event Action<float> OnProjectileSpeedChanged;
    public event Action<float> OnFireIntervalChanged;
    public event Action<float> OnProjectileLifeTimeChanged;
    public event Action<int, int> OnAmmoChanged;
    public event Action<float> OnReloadTimeChanged;
    public event Action<GameObject> OnProjectileLaunched;

    private void Awake()
    {
        if (muzzle == null)
        {
            Debug.LogWarning("[MissileLaunch] 머즐이 할당되지 않았습니다.");
            enabled = false;
            return;
        }
    }

    private void OnValidate()
    {
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        fireInterval = Mathf.Max(0f, fireInterval);
        projectileLifeTime = Mathf.Max(0f, projectileLifeTime);

        maxMagazine = Mathf.Max(1, maxMagazine);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maxMagazine);
        reloadTime = Mathf.Max(0f, reloadTime);
        projectileDamage = Mathf.Max(0f, projectileDamage);
    }

    public bool TryLaunch()
    {
        /*
         * 변경점:
         * MissileLaunch는 더 이상 조준중/발사가능/재장전 상태를 판단하지 않습니다.
         * 상태 판단은 TurretManager가 담당하고, 이 메서드는 "실제 발사 시도"만 담당합니다.
         */
        if (currentAmmo <= 0)
        {
            return false;
        }

        GameObject projectileObject = CreateProjectileObject();

        if (projectileObject == null)
        {
            return false;
        }

        ProjectileMover projectileMover = projectileObject.GetComponent<ProjectileMover>();

        if (projectileMover == null)
        {
            Debug.LogWarning("[MissileLaunch] 생성된 발사체에 ProjectileMover가 없습니다.");
            Destroy(projectileObject);
            return false;
        }

        projectileMover.Initialize(
            projectileSpeed,
            projectileLifeTime,
            projectileDamage,
            shooterTeamId
        );

        /*
         * 변경점:
         * 발사체 생성과 초기화가 성공했을 때만 탄환을 1 감소시킵니다.
         * 프리팹 누락, ProjectileMover 누락 등으로 실패한 경우 탄환은 감소하지 않습니다.
         */
        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        OnAmmoChanged?.Invoke(currentAmmo, maxMagazine);
        OnProjectileLaunched?.Invoke(projectileObject);

        return true;
    }

    private GameObject CreateProjectileObject()
    {
        if (usePrefab)
        {
            if (missilePrefab == null)
            {
                Debug.LogWarning("[MissileLaunch] usePrefab이 true이지만 missilePrefab이 할당되지 않았습니다.");
                return null;
            }

            GameObject projectileObject = Instantiate(
                missilePrefab,
                muzzle.position,
                muzzle.rotation
            );

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

        GameObject primitiveProjectileObject = GameObject.CreatePrimitive(projectileShape);
        primitiveProjectileObject.name = "Primitive Missile Projectile";

        primitiveProjectileObject.transform.position = muzzle.position;
        primitiveProjectileObject.transform.rotation = muzzle.rotation;
        primitiveProjectileObject.transform.localScale = Vector3.one * projectileScale;

        Collider projectileCollider = primitiveProjectileObject.GetComponent<Collider>();

        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        Rigidbody projectileRigidbody = primitiveProjectileObject.AddComponent<Rigidbody>();
        projectileRigidbody.useGravity = false;
        projectileRigidbody.isKinematic = true;

        primitiveProjectileObject.AddComponent<ProjectileMover>();

        return primitiveProjectileObject;
    }

    public void SetProjectileSpeed(float newProjectileSpeed)
    {
        newProjectileSpeed = Mathf.Max(0f, newProjectileSpeed);

        if (Mathf.Approximately(projectileSpeed, newProjectileSpeed))
            return;

        projectileSpeed = newProjectileSpeed;
        OnProjectileSpeedChanged?.Invoke(projectileSpeed);
    }

    public void SetFireInterval(float newFireInterval)
    {
        newFireInterval = Mathf.Max(0f, newFireInterval);

        if (Mathf.Approximately(fireInterval, newFireInterval))
            return;

        fireInterval = newFireInterval;
        OnFireIntervalChanged?.Invoke(fireInterval);
    }

    public void SetProjectileLifeTime(float newProjectileLifeTime)
    {
        newProjectileLifeTime = Mathf.Max(0f, newProjectileLifeTime);

        if (Mathf.Approximately(projectileLifeTime, newProjectileLifeTime))
            return;

        projectileLifeTime = newProjectileLifeTime;
        OnProjectileLifeTimeChanged?.Invoke(projectileLifeTime);
    }

    public void SetMaxMagazine(int newMaxMagazine, bool refillCurrentAmmo = false)
    {
        newMaxMagazine = Mathf.Max(1, newMaxMagazine);

        if (maxMagazine == newMaxMagazine && refillCurrentAmmo == false)
            return;

        maxMagazine = newMaxMagazine;

        if (refillCurrentAmmo)
        {
            currentAmmo = maxMagazine;
        }
        else
        {
            currentAmmo = Mathf.Clamp(currentAmmo, 0, maxMagazine);
        }

        OnAmmoChanged?.Invoke(currentAmmo, maxMagazine);
    }

    public void SetReloadTime(float newReloadTime)
    {
        newReloadTime = Mathf.Max(0f, newReloadTime);

        if (Mathf.Approximately(reloadTime, newReloadTime))
            return;

        reloadTime = newReloadTime;
        OnReloadTimeChanged?.Invoke(reloadTime);
    }

    public void RefillAmmo()
    {
        currentAmmo = maxMagazine;
        OnAmmoChanged?.Invoke(currentAmmo, maxMagazine);
    }
}
