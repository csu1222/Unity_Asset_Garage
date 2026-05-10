using System.Collections;
using UnityEngine;

/// <summary>
/// 포신 메쉬를 로컬 Z축 방향으로 뒤로 밀었다가 원위치로 되돌리는 반동 연출 컴포넌트입니다.
/// 실제 발사 판단은 TurretManager가 담당하고, 이 스크립트는 시각적 연출만 담당합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BarrelRecoil : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField]
    [Tooltip("반동을 적용할 포신 메쉬 Transform입니다. 비워두면 이 스크립트가 붙은 Transform을 사용합니다.")]
    private Transform recoilTarget;

    [Header("Recoil Setting")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("로컬 Z축 음수 방향으로 이동할 거리입니다. 예: 0.3이면 localPosition.z가 -0.3만큼 이동합니다.")]
    private float recoilDistance = 0.3f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("반동 왕복에 걸리는 전체 시간입니다. TurretManager에서 MissileLaunch.FireInterval 값으로 설정합니다.")]
    private float recoilDuration = 0.5f;

    private Vector3 originalLocalPosition;
    private Coroutine recoilCoroutine;

    private void Awake()
    {
        ResolveReference();
        CacheOriginalLocalPosition();
    }

    private void OnEnable()
    {
        /*
         * 변경점:
         * Pooling이나 비활성화/활성화 상황에서 포신 위치가 어긋나지 않도록
         * 활성화될 때 원래 위치로 복구합니다.
         */
        RestoreOriginalPosition();
    }

    private void OnDisable()
    {
        StopRecoil();
    }

    private void OnValidate()
    {
        recoilDistance = Mathf.Max(0f, recoilDistance);
        recoilDuration = Mathf.Max(0f, recoilDuration);

        if (recoilTarget == null)
        {
            recoilTarget = transform;
        }
    }

    private void ResolveReference()
    {
        if (recoilTarget == null)
        {
            recoilTarget = transform;
        }
    }

    private void CacheOriginalLocalPosition()
    {
        if (recoilTarget == null)
        {
            return;
        }

        originalLocalPosition = recoilTarget.localPosition;
    }

    public void SetRecoilDistance(float newRecoilDistance)
    {
        recoilDistance = Mathf.Max(0f, newRecoilDistance);
    }

    public void SetRecoilDuration(float newRecoilDuration)
    {
        recoilDuration = Mathf.Max(0f, newRecoilDuration);
    }

    public void PlayRecoil()
    {
        if (recoilTarget == null)
        {
            return;
        }

        /*
         * 변경점:
         * 반동 중 다시 발사 요청이 들어오면 기존 반동을 정리하고 새 반동을 시작합니다.
         * 현재 구조에서는 fireInterval과 recoilDuration이 같아서 보통 겹치지 않지만,
         * Inspector 값 변경이나 테스트 상황을 대비한 안전 처리입니다.
         */
        StopRecoil();

        recoilCoroutine = StartCoroutine(RecoilRoutine());
    }

    public void StopRecoil()
    {
        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
            recoilCoroutine = null;
        }

        RestoreOriginalPosition();
    }

    /*
     * 변경점:
     * 반동 전체 시간은 TurretManager가 MissileLaunch.FireInterval 값을 읽어
     * SetRecoilDuration()으로 넣어줍니다.
     *
     * 전체 시간의 절반:
     * - 원래 위치 → local Z축 -recoilDistance 위치
     *
     * 나머지 절반:
     * - local Z축 -recoilDistance 위치 → 원래 위치
     */

    private IEnumerator RecoilRoutine()
    {
        if (recoilDuration <= 0f || recoilDistance <= 0f)
        {
            RestoreOriginalPosition();
            recoilCoroutine = null;
            yield break;
        }

        Vector3 startPosition = originalLocalPosition;
        Vector3 recoilPosition = originalLocalPosition + Vector3.back * recoilDistance;

        float halfDuration = recoilDuration * 0.5f;

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float ratio = Mathf.Clamp01(elapsed / halfDuration);
            recoilTarget.localPosition = Vector3.Lerp(startPosition, recoilPosition, ratio);

            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float ratio = Mathf.Clamp01(elapsed / halfDuration);
            recoilTarget.localPosition = Vector3.Lerp(recoilPosition, startPosition, ratio);

            yield return null;
        }

        RestoreOriginalPosition();
        recoilCoroutine = null;
    }

    private void RestoreOriginalPosition()
    {
        if (recoilTarget == null)
        {
            return;
        }

        recoilTarget.localPosition = originalLocalPosition;
    }
}
