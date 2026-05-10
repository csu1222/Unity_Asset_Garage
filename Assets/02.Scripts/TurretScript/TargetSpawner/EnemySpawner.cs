using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// EnemySpawner가 붙어 있는 게임 오브젝트의 위치에서 Enemy를 생성하고 최대 개수를 관리합니다.
/// EnemyTarget을 UnityEngine.Pool.ObjectPool로 재사용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private GameObject enemyPrefab;

    [SerializeField]
    [Tooltip("생성된 Enemy의 부모(선택).")]
    private Transform enemyRoot;

    [Header("Pool Setting")]
    [SerializeField]
    [Min(1)]
    private int defaultCapacity = 10;

    [SerializeField]
    [Min(1)]
    private int maxSize = 50;

    private ObjectPool<EnemyTarget> enemyPool;

    [Header("Spawn Settings")]
    [SerializeField]
    [Min(1)]
    private int initialSpawnCount = 4;

    [SerializeField]
    [Min(1)]
    private int maxAliveCount = 8;

    [SerializeField]
    [Min(0.1f)]
    private float spawnIntervalSeconds = 1f;

    [SerializeField]
    [Tooltip("생성 시 Enemy가 바라볼 대상 위치입니다.")]
    private Transform lookAtCenter;

    [SerializeField]
    [Min(0.1f)]
    private float enemyMoveSpeedUnitsPerSecond = 5f;

    [SerializeField]
    [Min(0.1f)]
    private float enemyLifeTimeSeconds = 10f;

    [Header("Enemy Death Reaction")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Enemy가 사망했을 때 제자리에 멈추고 붉은색으로 유지되는 시간입니다.")]
    private float deathReactionDurationSeconds = 0.3f;

    private readonly List<EnemyTarget> aliveEnemies = new List<EnemyTarget>(32);

    private float nextSpawnTimeSeconds;

    private void Awake()
    {

        // Pool 생성 전에 Prefab 유효성을 검사합니다.

        if (enemyPrefab == null)
        {
            Debug.LogWarning("[EnemySpawner] enemyPrefab이 할당되지 않았습니다.");
            enabled = false;
            return;
        }

        if (enemyPrefab.TryGetComponent<EnemyTarget>(out _) == false)
        {
            Debug.LogWarning("[EnemySpawner] enemyPrefab 루트 오브젝트에 EnemyTarget 컴포넌트가 없습니다.");
            enabled = false;
            return;
        }

        /*
         * EnemyTarget 전용 ObjectPool을 생성합니다.
         */
        enemyPool = new ObjectPool<EnemyTarget>(
            createFunc: CreateEnemy,
            actionOnGet: OnGetEnemy,
            actionOnRelease: OnReleaseEnemy,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    private void Start()
    {
        for (int spawnIndex = 0; spawnIndex < initialSpawnCount; spawnIndex++)
        {
            if (!TryGetOneEnemy())
            {
                break;
            }
        }
    }

    private void Update()
    {
        RemoveInactiveOrDestroyedEntries();

        if (Time.time < nextSpawnTimeSeconds)
        {
            return;
        }

        if (aliveEnemies.Count >= maxAliveCount)
        {
            return;
        }

        if (TryGetOneEnemy())
        {
            nextSpawnTimeSeconds = Time.time + spawnIntervalSeconds;
        }
    }

    private EnemyTarget CreateEnemy()
    {
        GameObject enemyObject = Instantiate(enemyPrefab, enemyRoot);
        enemyObject.name = $"Pooled_{enemyPrefab.name}";

        EnemyTarget enemyTarget = enemyObject.GetComponent<EnemyTarget>();

        if (enemyTarget == null)
        {
            Debug.LogError("[EnemySpawner] 생성된 Enemy에 EnemyTarget이 없습니다.");
            Destroy(enemyObject);
            return null;
        }

        enemyObject.SetActive(false);

        return enemyTarget;
    }

    private void OnGetEnemy(EnemyTarget enemyTarget)
    {
        /*
         * 여기서 SetActive(true)를 하지 않습니다.
         *
         * 이유:
         * 위치, 회전, 수명, Pool 반환 콜백 설정이 끝나기 전에 OnEnable이 호출되면
         * Enemy가 이전 위치나 이전 상태로 잠깐 활성화될 수 있습니다.
         *
         * 실제 활성화는 TryGetOneEnemy()에서 enemyTarget.Initialize(...) 내부에서 처리합니다.
         */
    }

    private void OnReleaseEnemy(EnemyTarget enemyTarget)
    {
        if (enemyTarget == null)
        {
            return;
        }

        /*
         * Pool로 반환될 때 Enemy를 비활성화합니다.
         * 비활성화되면 EnemyTarget.OnDisable()이 호출되어 ActiveTargets 목록에서도 제거됩니다.
         */
        enemyTarget.gameObject.SetActive(false);

        /*
         * Hierarchy 정리를 위해 enemyRoot 아래로 다시 배치합니다.
         */
        enemyTarget.transform.SetParent(enemyRoot, worldPositionStays: false);
    }

    private void OnDestroyEnemy(EnemyTarget enemyTarget)
    {
        if (enemyTarget == null)
        {
            return;
        }

        /*
         * Pool의 maxSize를 초과해 더 이상 보관할 수 없는 경우에만 Destroy됩니다.
         * 일반적인 사망 처리에서는 호출되지 않습니다.
         */
        Destroy(enemyTarget.gameObject);
    }

    private bool TryGetOneEnemy()
    {
        if (enemyPool == null)
        {
            return false;
        }

        /*
         * 기존 spawnPoints 대신 EnemySpawner가 붙어 있는 게임 오브젝트의 위치를 사용합니다.
         */
        Vector3 spawnPosition = transform.position;

        /*
         * 설명:
         * 기본 회전값은 EnemySpawner 자신의 회전값입니다.
         */
        Quaternion spawnRotation = transform.rotation;

        /*
         * 설명:
         * lookAtCenter가 있으면 Enemy가 해당 위치를 바라보도록 회전값을 계산합니다.
         */
        if (lookAtCenter != null)
        {
            Vector3 toCenter = lookAtCenter.position - spawnPosition;

            if (toCenter.sqrMagnitude > 0.0001f)
            {
                spawnRotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
            }
        }

        /*
         * Instantiate 대신 Pool에서 EnemyTarget을 꺼냅니다.
         */
        EnemyTarget enemyTarget = enemyPool.Get();

        if (enemyTarget == null)
        {
            return false;
        }

        /*
         * EnemySpawner의 Inspector에서 설정한 deathReactionDurationSeconds를
         * EnemyTarget.Initialize()로 전달합니다.
         *
         * 이 값은 Enemy가 사망했을 때
         * - 제자리에 멈춤
         * - 붉은색으로 변경
         * - 피격 판정 비활성화
         * 상태를 유지하는 시간입니다.
         */
        enemyTarget.Initialize(
            spawnPosition,
            spawnRotation,
            enemyRoot,
            ReleaseEnemyToPool,
            deathReactionDurationSeconds
        );

        EnemyMove mover = enemyTarget.GetComponent<EnemyMove>();
        if (mover != null)
        {
            /*
             * 중요:
             * Pooling 구조에서는 EnemyMove가 Destroy(gameObject)를 호출하면 안 됩니다.
             *
             * 이 수정본에서는 EnemyTarget이 enemyLifeTimeSeconds 후
             * ReleaseToPool()을 호출해 Pool로 반환합니다.
             *
             * 따라서 EnemyMove가 수명 종료 시 Destroy를 호출하는 구조라면
             * EnemyMove 쪽도 반드시 수정해야 합니다.
             */
            mover.Initialize(
                enemyMoveSpeedUnitsPerSecond,
                enemyLifeTimeSeconds
            );
        }

        aliveEnemies.Add(enemyTarget);
        return true;
    }

    private void ReleaseEnemyToPool(EnemyTarget enemyTarget)
    {
        if (enemyTarget == null)
        {
            return;
        }

        /*
         * 살아있는 Enemy 목록에서 먼저 제거합니다.
         */
        aliveEnemies.Remove(enemyTarget);

        /*
         * Destroy가 아니라 Pool로 반환합니다.
         */
        enemyPool.Release(enemyTarget);
    }

    private void RemoveInactiveOrDestroyedEntries()
    {
        /*
         * 기존에는 Destroy된 GameObject만 null 체크했습니다.
         *
         * Pooling 구조에서는 Enemy가 죽어도 Destroy되지 않고 비활성화됩니다.
         * 따라서 null뿐 아니라 activeSelf도 확인해야 합니다.
         */
        for (int index = aliveEnemies.Count - 1; index >= 0; index--)
        {
            EnemyTarget enemyTarget = aliveEnemies[index];

            if (enemyTarget == null)
            {
                aliveEnemies.RemoveAt(index);
                continue;
            }

            if (enemyTarget.gameObject.activeSelf == false)
            {
                aliveEnemies.RemoveAt(index);
            }
        }
    }
}
