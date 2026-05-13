using UnityEngine;

namespace SystemicOverload.Combat
{
    /// <summary>
    /// 한 번의 피해 적용에 필요한 최소 데이터입니다. 추후 치명타/속성 등을 확장할 수 있습니다.
    /// </summary>
    public struct DamagePayload
    {
        /// <summary>
        /// 최종 적용할 피해량입니다.
        /// </summary>
        public float Amount;

        /// <summary>
        /// 공격 주체의 Transform(선택). 피격 방향 계산 등에 사용할 수 있습니다.
        /// </summary>
        public Transform Attacker;
    }
}
