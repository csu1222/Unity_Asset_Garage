namespace SystemicOverload.Combat
{
    /// <summary>
    /// 데미지를 받을 수 있는 대상에 대한 최소 계약입니다. Player/Enemy/Prop 등 구현체를 통일합니다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 외부에서 전달된 데미지를 적용합니다.
        /// </summary>
        void ApplyDamage(in DamagePayload payload);

        /// <summary>
        /// 생존 여부를 반환합니다.
        /// </summary>
        bool IsAlive { get; }
    }
}
