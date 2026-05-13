using SystemicOverload.Phase1;
using UnityEngine;

namespace SystemicOverload.Combat
{
    /// <summary>
    /// 카메라 중앙 SphereCast로 두께가 있는 투사 판정을 수행합니다.
    /// </summary>
    [RequireComponent(typeof(InputProvider))]
    public sealed class TpsMagicSphereCastComponent : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private float range = 30.0f;
        [SerializeField] private float radius = 0.35f;
        [SerializeField] private float damage = 20.0f;
        [SerializeField] private float cooldown = 0.25f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private GameObject hitFxPrefab;
        [SerializeField] private bool drawGizmo = true;

        private InputProvider inputProvider;
        private float nextAllowedCastTime;
        private Vector3 lastCastOrigin;
        private Vector3 lastCastDirection = Vector3.forward;
        private float lastCastDistance;
        private bool hasLastCast;

        private void Awake()
        {
            inputProvider = GetComponent<InputProvider>();
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void OnValidate()
        {
            range = Mathf.Max(0.5f, range);
            radius = Mathf.Max(0.01f, radius);
            damage = Mathf.Max(0.0f, damage);
            cooldown = Mathf.Max(0.0f, cooldown);
        }

        private void Update()
        {
            if (!inputProvider.WasMagicPressedThisFrame)
            {
                return;
            }

            if (Time.time < nextAllowedCastTime)
            {
                return;
            }

            nextAllowedCastTime = Time.time + cooldown;
            CastMagic();
        }

        private void CastMagic()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
                if (aimCamera == null)
                {
                    return;
                }
            }

            Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
            if (!TrySphereCastExcludeSelf(centerRay, out RaycastHit hitInfo))
            {
                lastCastOrigin = centerRay.origin;
                lastCastDirection = centerRay.direction;
                lastCastDistance = range;
                hasLastCast = true;
                return;
            }

            Debug.DrawRay(centerRay.origin, centerRay.direction * hitInfo.distance, Color.cyan, 0.5f);
            lastCastOrigin = centerRay.origin;
            lastCastDirection = centerRay.direction;
            lastCastDistance = hitInfo.distance;
            hasLastCast = true;

            IDamageable targetDamageable = hitInfo.collider.GetComponentInParent<IDamageable>();
            if (targetDamageable != null && targetDamageable.IsAlive)
            {
                DamagePayload payload = new DamagePayload
                {
                    Amount = damage,
                    Attacker = transform
                };
                targetDamageable.ApplyDamage(in payload);
            }

            if (hitFxPrefab != null)
            {
                Quaternion rotation = Quaternion.LookRotation(hitInfo.normal);
                Instantiate(hitFxPrefab, hitInfo.point, rotation);
            }

            Debug.Log($"Magic hit: {hitInfo.collider.name} / {hitInfo.distance:F1}m");
        }

        private bool TrySphereCastExcludeSelf(Ray ray, out RaycastHit bestHit)
        {
            RaycastHit[] allHits = Physics.SphereCastAll(ray, radius, range, hitMask, QueryTriggerInteraction.Ignore);
            bestHit = default;
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int index = 0; index < allHits.Length; index++)
            {
                RaycastHit candidateHit = allHits[index];
                if (candidateHit.collider == null)
                {
                    continue;
                }

                if (candidateHit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (candidateHit.distance < bestDistance)
                {
                    bestDistance = candidateHit.distance;
                    bestHit = candidateHit;
                    found = true;
                }
            }

            return found;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo || !hasLastCast)
            {
                return;
            }

            Vector3 castEnd = lastCastOrigin + lastCastDirection * lastCastDistance;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(lastCastOrigin, castEnd);
            Gizmos.DrawWireSphere(lastCastOrigin, radius);
            Gizmos.DrawWireSphere(castEnd, radius);
        }
    }
}
