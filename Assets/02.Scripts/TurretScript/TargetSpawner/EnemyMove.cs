using UnityEngine;

public class EnemyMove : MonoBehaviour
{
    [Header("Field")]
    [SerializeField] private float enemyMoveSpeed = 10f;
    [SerializeField] private float enemyLifeTime = 3f;

    private float remainingLifeTime;

    public void Initialize(float moveSpeed, float lifeTime)
    {
        enemyMoveSpeed = moveSpeed;
        enemyLifeTime = lifeTime;
        remainingLifeTime = enemyLifeTime;
    }

    private void OnEnable()
    {
        remainingLifeTime = enemyLifeTime;
    }
    private void Update()
    {
        // Projectile과 동일하게 forward 기준 등속 이동으로 단순한 테스트 타겟 행동을 보장합니다.
        transform.position += transform.forward * (enemyMoveSpeed * Time.deltaTime);

        remainingLifeTime -= Time.deltaTime;
        if (remainingLifeTime <= 0f)
        {
            EnemyTarget enemyTarget = GetComponent<EnemyTarget>();

            if (enemyTarget != null)
            {
                enemyTarget.ReleaseToPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
