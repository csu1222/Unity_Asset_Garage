using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyTarget : MonoBehaviour
{
    private static readonly List<EnemyTarget> ActiveTargetsInternal = new List<EnemyTarget>(64);

    [SerializeField]
    [Tooltip("향후 진영 분리 확장을 위한 팀 식별자입니다.")]
    private int teamId;

    [SerializeField]
    [Tooltip("최대 체력입니다.")]
    private float maxHealth = 10f;

    [SerializeField]
    [Tooltip("사망 위치 표시용 marker를 잠깐 남길지 여부입니다.")]
    private bool showDeathMarker = true;

    [SerializeField]
    [Tooltip("사망 marker 유지 시간(초).")]
    private float deathMarkerLifeTimeSeconds = 0.35f;

    [SerializeField]
    [Tooltip("비워두면 transform.position을 조준점으로 사용합니다.")]
    private Transform aimPoint;

    private float currentHealth;

    /*
     * 수정점:
     * Enemy가 죽었을 때 Destroy(gameObject)를 호출하지 않고,
     * EnemySpawner가 가진 ObjectPool로 반환하기 위한 콜백입니다.
     *
     * EnemyTarget은 ObjectPool을 직접 알 필요가 없습니다.
     * 대신 EnemySpawner가 넘겨준 release 콜백만 호출합니다.
     */
    private Action<EnemyTarget> releaseToPool;

    /*
     * 수정점:
     * 중복 반환 방지용 플래그입니다.
     *
     * 예를 들어,
     * - Projectile 충돌로 사망
     * - EnemyMove의 수명 종료
     *
     * 두 처리가 같은 프레임에 겹치면 ReleaseToPool()이 두 번 호출될 수 있습니다.
     * ObjectPool.Release()가 같은 오브젝트에 두 번 호출되면 오류가 날 수 있으므로
     * 이 플래그로 막습니다.
     */
    private bool isReleased;

    public static IReadOnlyList<EnemyTarget> ActiveTargets => ActiveTargetsInternal;

    public int TeamId => teamId;

    public float CurrentHealth => currentHealth;

    public Transform CachedTransform => transform;

    public Vector3 AimWorldPosition => aimPoint != null ? aimPoint.position : transform.position;

    private void OnEnable()
    {
        /*
         * 설명:
         * Pool에서 다시 꺼내져 활성화될 때마다 현재 체력을 최대 체력으로 초기화합니다.
         */
        currentHealth = maxHealth;

        /*
         * 설명:
         * 현재 활성화된 EnemyTarget 목록에 등록합니다.
         * CheckAiming 같은 타겟 탐색 로직에서 ActiveTargets를 사용할 수 있습니다.
         */
        if (!ActiveTargetsInternal.Contains(this))
        {
            ActiveTargetsInternal.Add(this);
        }
    }

    private void OnDisable()
    {
        /*
         * 설명:
         * Enemy가 Pool로 반환되어 비활성화되면 활성 타겟 목록에서 제거합니다.
         */
        ActiveTargetsInternal.Remove(this);
    }

    /*
     * 수정점:
     * 기존 Initialization 메서드를 ObjectPool 구조에 맞게 수정했습니다.
     *
     * 기존 역할:
     * - 위치 설정
     * - 회전 설정
     * - 부모 설정
     *
     * 추가된 역할:
     * - 체력 초기화
     * - 중복 반환 플래그 초기화
     * - Pool 반환 콜백 저장
     * - 초기화 완료 후 SetActive(true)
     */
    public void Initialize(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        Transform root,
        Action<EnemyTarget> releaseCallback)
    {
        /*
         * 수정점:
         * Pool로 돌아갈 때 호출할 함수를 EnemySpawner로부터 받아 저장합니다.
         */
        releaseToPool = releaseCallback;

        /*
         * 수정점:
         * Pool에서 재사용될 때 이전 상태가 남아 있으면 안 되므로 초기화합니다.
         */
        isReleased = false;
        currentHealth = maxHealth;

        /*
         * 설명:
         * 부모를 먼저 설정합니다.
         * worldPositionStays를 false로 두면 부모 기준 Transform 영향이 정리됩니다.
         */
        transform.SetParent(root, worldPositionStays: false);

        /*
         * 설명:
         * EnemySpawner 위치에서 생성되고,
         * lookAtCenter를 바라보도록 계산된 회전값을 적용받습니다.
         */
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        /*
         * 수정점:
         * ObjectPool의 OnGetEnemy에서 바로 SetActive(true)를 하지 않는 구조라면
         * Initialize가 끝난 뒤 여기서 활성화합니다.
         *
         * 이유:
         * 활성화가 먼저 되면 OnEnable이 먼저 호출되어
         * 위치, 회전, 콜백 등이 세팅되기 전 상태로 등록될 수 있습니다.
         */
        gameObject.SetActive(true);
    }

    public bool ApplyDamage(float damageAmount)
    {
        /*
         * 수정점:
         * 이미 Pool로 반환 처리된 Enemy라면 데미지를 받지 않습니다.
         */
        if (isReleased)
        {
            return false;
        }

        if (damageAmount <= 0f)
        {
            return false;
        }

        currentHealth -= damageAmount;

        if (currentHealth <= 0f)
        {
            Die();
        }

        return true;
    }

    private void Die()
    {
        if (isReleased)
        {
            return;
        }

        if (showDeathMarker)
        {
            CreateDeathMarker();
        }

        /*
         * 수정점:
         * 기존 코드에서는 Destroy(gameObject)를 호출했습니다.
         * ObjectPool 구조에서는 Destroy하지 않고 Pool로 반환해야 합니다.
         */
        ReleaseToPool();
    }

    /*
     * 수정점:
     * EnemyMove에서도 호출할 수 있도록 public 메서드로 둡니다.
     *
     * 사용 예:
     * - 체력이 0 이하가 되었을 때
     * - 수명이 끝났을 때
     * - 목적지에 도달했을 때
     */
    public void ReleaseToPool()
    {
        if (isReleased)
        {
            return;
        }

        isReleased = true;

        if (releaseToPool != null)
        {
            /*
             * 설명:
             * 실제 Pool 반환은 EnemySpawner가 담당합니다.
             * EnemyTarget은 직접 enemyPool.Release(this)를 호출하지 않습니다.
             */
            releaseToPool.Invoke(this);
        }
        else
        {
            /*
             * 설명:
             * Pool 없이 단독 테스트할 경우를 위한 안전장치입니다.
             * 정상적인 ObjectPool 구조에서는 이 분기로 들어오지 않아야 합니다.
             */
            Destroy(gameObject);
        }
    }

    private void CreateDeathMarker()
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "EnemyDeathMarker";
        marker.transform.position = AimWorldPosition;
        marker.transform.localScale = Vector3.one * 0.45f;

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        Renderer markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            /*
             * 설명:
             * 사망 지점을 눈에 띄게 보여주기 위한 임시 색상입니다.
             * 테스트용 시각 효과에 가깝습니다.
             */
            markerRenderer.material.color = new Color(1f, 0.2f, 0.2f, 1f);
        }

        Destroy(marker, deathMarkerLifeTimeSeconds);
    }
}