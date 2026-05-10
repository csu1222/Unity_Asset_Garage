using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyTarget : MonoBehaviour
{
    private static readonly List<EnemyTarget> ActiveTargetsInternal = new List<EnemyTarget>(64);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField]
    [Tooltip("향후 진영 분리 확장을 위한 팀 식별자입니다.")]
    private int teamId;

    [SerializeField]
    [Tooltip("최대 체력입니다.")]
    private float maxHealth = 10f;

    [SerializeField]
    [Tooltip("비워두면 transform.position을 조준점으로 사용합니다.")]
    private Transform aimPoint;

    [Header("Death Reaction")]
    [SerializeField]
    [Tooltip("사망 연출 중 색상을 변경할 Renderer 목록입니다. 비워두면 자식 Renderer를 자동으로 찾습니다.")]
    private Renderer[] reactionRenderers;

    [SerializeField]
    [Tooltip("사망 연출 중 Enemy에 적용할 색상입니다.")]
    private Color deathReactionColor = Color.red;

    private float currentHealth;

    /*
     * EnemySpawner의 Inspector에서 설정한 피격/사망 연출 시간을 Initialize()를 통해 전달받습니다.
     * 기본값은 요구사항에 맞춰 0.3초입니다.
     */
    private float deathReactionDurationSeconds = 0.3f;

    /*
     * 사망 연출 중에는 피격 판정을 막아야 하므로 별도 플래그를 둡니다.
     *
     * isDamageLocked == true
     * - ApplyDamage()가 false를 반환합니다.
     * - Collider도 꺼서 Projectile 충돌 자체가 들어오지 않도록 합니다.
     */
    private bool isDamageLocked;

    /*
     * Die()가 여러 번 호출되어 코루틴이 중복 실행되는 것을 막기 위한 플래그입니다.
     */
    private bool isDeathReactionPlaying;

    /*
     * Enemy가 죽었을 때 Destroy(gameObject)를 호출하지 않고,
     * EnemySpawner가 가진 ObjectPool로 반환하기 위한 콜백입니다.
     *
     * EnemyTarget은 ObjectPool을 직접 알 필요가 없습니다.
     * 대신 EnemySpawner가 넘겨준 release 콜백만 호출합니다.
     */
    private Action<EnemyTarget> releaseToPool;

    /*
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

    private Coroutine deathReactionCoroutine;

    /*
     * 사망 연출 동안 이동을 멈추기 위해 EnemyMove를 캐싱합니다.
     */
    private EnemyMove cachedEnemyMove;

    /*
     * 사망 연출 동안 피격 판정을 비활성화하기 위해 Collider들을 캐싱합니다.
     */
    private Collider[] cachedColliders;

    /*
     * 사망 연출 후 원래 색상으로 돌리기 위해 Renderer별 원본 색상을 저장합니다.
     */
    private Color[] originalRendererColors;

    private int[] rendererColorPropertyIds;

    public static IReadOnlyList<EnemyTarget> ActiveTargets => ActiveTargetsInternal;

    public int TeamId => teamId;

    public float CurrentHealth => currentHealth;

    public Transform CachedTransform => transform;

    public Vector3 AimWorldPosition => aimPoint != null ? aimPoint.position : transform.position;

    private void Awake()
    {
        CacheComponents();
        CacheOriginalRendererColors();
    }

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

        /*
         * 변경점:
         * Pool로 반환될 때 붉은색이나 비활성화된 Collider 상태가 남지 않도록 복구합니다.
         */
        if (deathReactionCoroutine != null)
        {
            StopCoroutine(deathReactionCoroutine);
            deathReactionCoroutine = null;
        }

        RestoreOriginalRendererColors();
        SetCollidersEnabled(true);
        SetMovementEnabled(true);
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
     * - 피격/사망 연출 시간 저장
     * - 사망 연출 관련 상태 복구
     * - 초기화 완료 후 SetActive(true)
     */
    public void Initialize(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        Transform root,
        Action<EnemyTarget> releaseCallback,
        float deathReactionDuration)
    {
        releaseToPool = releaseCallback;

        deathReactionDurationSeconds = Mathf.Max(0f, deathReactionDuration);

        /*
         * 수정점:
         * Pool에서 재사용될 때 이전 상태가 남아 있으면 안 되므로 초기화합니다.
         */
        ResetRuntimeState();

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

    private void ResetRuntimeState()
    {
        isReleased = false;
        isDamageLocked = false;
        isDeathReactionPlaying = false;
        currentHealth = maxHealth;

        if (deathReactionCoroutine != null)
        {
            StopCoroutine(deathReactionCoroutine);
            deathReactionCoroutine = null;
        }

        RestoreOriginalRendererColors();
        SetCollidersEnabled(true);
        SetMovementEnabled(true);
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

        /*
         * 변경점:
         * 사망 연출 중에는 붉은색 상태이며 피격 판정이 비활성화되어야 합니다.
         */
        if (isDamageLocked)
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
        if (isReleased || isDeathReactionPlaying)
        {
            return;
        }

        /*
         * 기존 CreateDeathMarker() 호출을 제거했습니다.
         *
         * 기존 방식:
         * - 사망 위치에 새 Sphere 생성
         *
         * 변경 방식:
         * - Enemy 자신이 제자리에 멈춤
         * - Enemy 색상을 붉은색으로 변경
         * - 붉은색 상태 동안 피격 판정 비활성화
         * - deathReactionDurationSeconds 이후 Pool로 반환
         */
        deathReactionCoroutine = StartCoroutine(DeathReactionRoutine());
    }

    private IEnumerator DeathReactionRoutine()
    {
        isDeathReactionPlaying = true;
        isDamageLocked = true;

        /*
         * 죽은 Enemy가 더 이상 터렛의 타겟 후보로 잡히지 않도록 ActiveTargets에서 제거합니다.
         * OnDisable에서도 Remove를 다시 호출하지만, List.Remove는 없어도 안전합니다.
         */
        ActiveTargetsInternal.Remove(this);

        /*
         * Enemy를 제자리에 멈춥니다.
         */
        SetMovementEnabled(false);
        StopRigidbodyMotion();

        /*
         * 붉은색 상태 동안 Projectile과의 피격 판정을 비활성화합니다.
         */
        SetCollidersEnabled(false);

        /*
         * 사망 위치에 새 Sphere를 만들지 않고, Enemy 자신의 색상을 변경합니다.
         */
        ApplyDeathReactionColor();

        if (deathReactionDurationSeconds > 0f)
        {
            yield return new WaitForSeconds(deathReactionDurationSeconds);
        }

        /*
         * 연출 시간이 끝난 뒤 Pool로 반환합니다.
         * 실제 비활성화는 EnemySpawner.ReleaseEnemyToPool()에서 처리합니다.
         */
        deathReactionCoroutine = null;
        ReleaseToPool();
    }

    /*
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
             * 실제 Pool 반환은 EnemySpawner가 담당합니다.
             * EnemyTarget은 직접 enemyPool.Release(this)를 호출하지 않습니다.
             */
            releaseToPool.Invoke(this);
        }
        else
        {
            /*
             * Pool 없이 단독 테스트할 경우를 위한 안전장치입니다.
             * 정상적인 ObjectPool 구조에서는 이 분기로 들어오지 않아야 합니다.
             */
            Destroy(gameObject);
        }
    }

    private void CacheComponents()
    {
        cachedEnemyMove = GetComponent<EnemyMove>();
        cachedColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        if (reactionRenderers == null || reactionRenderers.Length == 0)
        {
            reactionRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }
    }

    private void CacheOriginalRendererColors()
    {
        if (reactionRenderers == null || reactionRenderers.Length == 0)
        {
            return;
        }

        originalRendererColors = new Color[reactionRenderers.Length];
        rendererColorPropertyIds = new int[reactionRenderers.Length];

        for (int index = 0; index < reactionRenderers.Length; index++)
        {
            Renderer targetRenderer = reactionRenderers[index];

            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            {
                originalRendererColors[index] = Color.white;
                rendererColorPropertyIds[index] = ColorId;
                continue;
            }

            Material sharedMaterial = targetRenderer.sharedMaterial;

            /*
             * URP Lit Shader는 보통 _BaseColor를 사용하고,
             * Built-in Standard Shader는 보통 _Color를 사용합니다.
             */
            if (sharedMaterial.HasProperty(BaseColorId))
            {
                originalRendererColors[index] = sharedMaterial.GetColor(BaseColorId);
                rendererColorPropertyIds[index] = BaseColorId;
            }
            else if (sharedMaterial.HasProperty(ColorId))
            {
                originalRendererColors[index] = sharedMaterial.GetColor(ColorId);
                rendererColorPropertyIds[index] = ColorId;
            }
            else
            {
                originalRendererColors[index] = Color.white;
                rendererColorPropertyIds[index] = ColorId;
            }
        }
    }

    private void ApplyDeathReactionColor()
    {
        if (reactionRenderers == null)
        {
            return;
        }

        for (int index = 0; index < reactionRenderers.Length; index++)
        {
            Renderer targetRenderer = reactionRenderers[index];

            if (targetRenderer == null)
            {
                continue;
            }

            Material targetMaterial = targetRenderer.material;
            if (targetMaterial == null)
            {
                continue;
            }

            int propertyId = GetColorPropertyId(targetMaterial, index);
            targetMaterial.SetColor(propertyId, deathReactionColor);
        }
    }

    private void RestoreOriginalRendererColors()
    {
        if (reactionRenderers == null || originalRendererColors == null || rendererColorPropertyIds == null)
        {
            return;
        }

        for (int index = 0; index < reactionRenderers.Length; index++)
        {
            Renderer targetRenderer = reactionRenderers[index];

            if (targetRenderer == null)
            {
                continue;
            }

            Material targetMaterial = targetRenderer.material;
            if (targetMaterial == null)
            {
                continue;
            }

            int propertyId = rendererColorPropertyIds[index];

            if (targetMaterial.HasProperty(propertyId))
            {
                targetMaterial.SetColor(propertyId, originalRendererColors[index]);
            }
        }
    }

    private int GetColorPropertyId(Material material, int rendererIndex)
    {
        if (material.HasProperty(BaseColorId))
        {
            return BaseColorId;
        }

        if (material.HasProperty(ColorId))
        {
            return ColorId;
        }

        if (rendererColorPropertyIds != null &&
            rendererIndex >= 0 &&
            rendererIndex < rendererColorPropertyIds.Length)
        {
            return rendererColorPropertyIds[rendererIndex];
        }

        return ColorId;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (cachedColliders == null)
        {
            return;
        }

        for (int index = 0; index < cachedColliders.Length; index++)
        {
            Collider targetCollider = cachedColliders[index];

            if (targetCollider == null)
            {
                continue;
            }

            targetCollider.enabled = enabled;
        }
    }

    private void SetMovementEnabled(bool enabled)
    {
        if (cachedEnemyMove != null)
        {
            cachedEnemyMove.enabled = enabled;
        }
    }

    private void StopRigidbodyMotion()
    {
        Rigidbody targetRigidbody = GetComponent<Rigidbody>();

        if (targetRigidbody == null)
        {
            return;
        }

        /*
         * Kinematic Rigidbody는 linearVelocity / angularVelocity 설정을 지원하지 않습니다.
         *
         * 현재 Enemy 이동은 EnemyMove를 비활성화해서 멈추고 있으므로,
         * Rigidbody가 Kinematic이면 속도를 직접 건드릴 필요가 없습니다.
         */
        if (targetRigidbody.isKinematic)
        {
            return;
        }

        /*
         * Non-Kinematic Rigidbody일 때만 물리 속도를 제거합니다.
         */
        targetRigidbody.linearVelocity = Vector3.zero;
        targetRigidbody.angularVelocity = Vector3.zero;
    }
}
