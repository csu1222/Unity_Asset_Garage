using UnityEngine;

public class MissileLaunch : MonoBehaviour
{
    [Header("Muzzle Ref")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private CheckAiming checkAiming;

    [Header("Projectile Shape")]
    [SerializeField] private PrimitiveType projectileShape = PrimitiveType.Sphere;
    [SerializeField] private float projectileScale = 0.3f;

    [Header("Projectile Field")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private float projectileLifeTime = 3f;


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
        // 1. 머즐 위치에서 Primitive Shape 발사체 생성
        GameObject projectileObject = GameObject.CreatePrimitive(projectileShape);

        projectileObject.name = "Missile Projectile";

        projectileObject.transform.position = muzzle.position;
        projectileObject.transform.rotation = muzzle.rotation;
        projectileObject.transform.localScale = Vector3.one * projectileScale;

        // 2. 발사 방향은 muzzle.forward
        Vector3 fireDirection = muzzle.forward;

        // 3. 이동 스크립트 추가
        MissileProjectile missileProjectile = projectileObject.AddComponent<MissileProjectile>();

        missileProjectile.Initialize(
            fireDirection,
            projectileSpeed,
            projectileLifeTime
        );
    }
}
