using System;
using UnityEngine;

namespace SystemicOverload.Combat
{
    /// <summary>
    /// 체력과 사망 상태를 관리하고, <see cref="IDamageable"/> 계약을 구현합니다.
    /// </summary>
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100.0f;
        [SerializeField] private float currentHealth = 100.0f;

        /// <summary>
        /// 피해 적용 직후(사망 전)에 호출됩니다. UI/VFX 연동에 사용합니다.
        /// </summary>
        public event Action<float, float> Damaged;

        /// <summary>
        /// 사망 시 한 번 호출됩니다.
        /// </summary>
        public event Action Died;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsAlive => currentHealth > 0.0f;

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0.0f, maxHealth);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1.0f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0.0f, maxHealth);
        }
        public void ApplyDamage(in DamagePayload payload)
        {
            if (!IsAlive)
            {
                return;
            }

            float appliedAmount = Mathf.Max(0.0f, payload.Amount);
            if (appliedAmount <= 0.0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0.0f, currentHealth - appliedAmount);
            Damaged?.Invoke(appliedAmount, currentHealth);

            if (currentHealth <= 0.0f)
            {
                Died?.Invoke();
            }
        }
        public void ResetHealthToFull()
        {
            currentHealth = maxHealth;
        }
    }
}
