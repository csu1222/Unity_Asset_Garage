using UnityEngine;

/// <summary>
/// 발사체를 전진 방향(transform.forward)으로 이동시키고, 지정된 수명 후 오브젝트를 제거합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectileMover : MonoBehaviour
{
    [SerializeField]
    [Tooltip("초당 이동 거리(월드 단위).")]
    private float moveSpeedUnitsPerSecond = 12f;

    [SerializeField]
    [Tooltip("생존 시간(초) 이후 Destroy 처리.")]
    private float lifeTimeSeconds = 3f;

    [SerializeField]
    [Tooltip("Projectile 1발당 데미지량.")]
    private float damageAmount = 10f;

    [SerializeField]
    [Tooltip("아군 판정을 위한 팀 식별자입니다.")]
    private int ownerTeamId;

    private float remainingLifeSeconds;

    /// <summary>
    /// 인스턴스 생성 직후 속도·수명을 덮어씁니다.
    /// </summary>
    /// <param name="speed">전진 속도.</param>
    /// <param name="lifeTime">수명(초).</param>
    public void Initialize(float speed, float lifeTime, float damage, int shooterTeamId)
    {
        moveSpeedUnitsPerSecond = speed;
        lifeTimeSeconds = lifeTime;
        damageAmount = damage;
        ownerTeamId = shooterTeamId;
        remainingLifeSeconds = lifeTimeSeconds;
    }

    private void OnEnable()
    {
        remainingLifeSeconds = lifeTimeSeconds;
    }

    private void Update()
    {
        // 전진 이동: Instantiate 시 MuzzlePoint의 world rotation을 그대로 물려받아 forward가 탄도와 일치합니다.
        transform.position += transform.forward * (moveSpeedUnitsPerSecond * Time.deltaTime);

        // 수명이 다 되면 오브젝트를 제거하여 씬에 잔류하지 않도록 합니다.
        remainingLifeSeconds -= Time.deltaTime;
        if (remainingLifeSeconds <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyTarget enemyTarget = other.GetComponentInParent<EnemyTarget>();
        if (enemyTarget == null)
        {
            return;
        }

        // 같은 팀은 무시하고 적 팀에게만 데미지를 적용합니다.
        if (enemyTarget.TeamId == ownerTeamId)
        {
            return;
        }

        enemyTarget.ApplyDamage(damageAmount);
        Destroy(gameObject);
    }
}
