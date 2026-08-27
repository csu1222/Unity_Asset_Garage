using UnityEngine;

namespace AssetGarage.CombatDemo
{
    [CreateAssetMenu(menuName = "CombatDemo/Manual Balance")]
    public sealed class ManualCombatBalanceConfig : ScriptableObject
    {
        [Min(0)] public float ReadyTime = 0.5f;
        [Min(0.01f)] public float DecisionDuration = 1.5f;
        [Range(0, 1)] public float GreatStart = 0.85f;
        [Range(0, 1)] public float ExtremeStart = 0.925f;
        [Min(0)] public float GreatMultiplier = 1.5f;
        [Min(0)] public float ExtremeMultiplier = 3f;
        [Min(0.01f)] public float DefenseScale = 100f;
        [Min(1)] public int PressureThreshold = 3;
        [Min(0)] public float EnemyPressureMultiplier = 1.5f;
        [Min(1)] public int MaxTurn = 10;
        [Min(0)] public float AttackWeight = 1;
        [Min(0)] public float DefenseWeight = 1;
        [Min(0)] public float RecoveryWeight = 1;

        public void Sanitize()
        {
            ReadyTime = Safe(ReadyTime, 0);
            DecisionDuration = Mathf.Max(0.01f, Safe(DecisionDuration, 1.5f));
            GreatStart = Mathf.Clamp01(Safe(GreatStart, .85f));
            ExtremeStart = Mathf.Clamp(Safe(ExtremeStart, .925f), GreatStart, 1);
            GreatMultiplier = Safe(GreatMultiplier, 1.5f);
            ExtremeMultiplier = Safe(ExtremeMultiplier, 3);
            DefenseScale = Mathf.Max(.01f, Safe(DefenseScale, 100));
            PressureThreshold = Mathf.Max(1, PressureThreshold);
            EnemyPressureMultiplier = Safe(EnemyPressureMultiplier, 1.5f);
            MaxTurn = Mathf.Max(1, MaxTurn);
        }
        private void OnValidate() => Sanitize();
        private static float Safe(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0, value);
    }

    [CreateAssetMenu(menuName = "CombatDemo/Auto Balance")]
    public sealed class AutoCombatBalanceConfig : ScriptableObject
    {
        [Header("Combat Power")] public float CurrentHPWeight = .25f;
        public float AttackWeight = 1; public float DefenseWeight = 1; public float RecoveryWeight = .75f;
        [Header("Win Probability")] public float GuaranteedWinRatio = 2; public float GuaranteedLoseRatio = .5f; public float WinProbabilityScale = 100;
        [Header("Victory Damage")] public float VictoryDamageScale = .5f; public float MinimumVictoryDamageRate = .05f; public float MaximumVictoryDamageRate = .4f;
        public void Sanitize()
        {
            CurrentHPWeight = Safe(CurrentHPWeight, .25f); AttackWeight = Safe(AttackWeight, 1); DefenseWeight = Safe(DefenseWeight, 1); RecoveryWeight = Safe(RecoveryWeight, .75f);
            GuaranteedLoseRatio = Mathf.Clamp(Safe(GuaranteedLoseRatio, .5f), 0, .999f); GuaranteedWinRatio = Mathf.Max(1.001f, Safe(GuaranteedWinRatio, 2)); WinProbabilityScale = Mathf.Max(.01f, Safe(WinProbabilityScale, 100));
            VictoryDamageScale = Safe(VictoryDamageScale, .5f); MinimumVictoryDamageRate = Mathf.Clamp01(Safe(MinimumVictoryDamageRate, .05f)); MaximumVictoryDamageRate = Mathf.Clamp(Safe(MaximumVictoryDamageRate, .4f), MinimumVictoryDamageRate, 1);
        }
        private void OnValidate() => Sanitize();
        private static float Safe(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Max(0, v);
    }

    [CreateAssetMenu(menuName = "CombatDemo/Resolution Balance")]
    public sealed class ResolutionBalanceConfig : ScriptableObject
    {
        public int BaseNegotiationCost = 500; public float CostPerPowerDifference = 10; public int MinimumNegotiationCost = 100; public int MaximumNegotiationCost = 10000;
        [Range(0, 1)] public float BaseEscapeChance = .5f; [Range(0, 1)] public float MinimumEscapeChance; [Range(0, 1)] public float MaximumEscapeChance = 1;
        [Min(.1f)] public float DefaultQueueWaitDuration = 12;
        private void OnValidate()
        {
            MinimumNegotiationCost = Mathf.Max(0, MinimumNegotiationCost); MaximumNegotiationCost = Mathf.Max(MinimumNegotiationCost, MaximumNegotiationCost);
            CostPerPowerDifference = float.IsFinite(CostPerPowerDifference) ? CostPerPowerDifference : 10;
            MinimumEscapeChance = Mathf.Clamp01(MinimumEscapeChance); MaximumEscapeChance = Mathf.Clamp(MaximumEscapeChance, MinimumEscapeChance, 1); BaseEscapeChance = Mathf.Clamp(BaseEscapeChance, MinimumEscapeChance, MaximumEscapeChance); DefaultQueueWaitDuration = Mathf.Max(.1f, DefaultQueueWaitDuration);
        }
    }

    [CreateAssetMenu(menuName = "CombatDemo/Timing Presentation")]
    public sealed class TimingPresentationConfig : ScriptableObject
    {
        public TimingViewKind DefaultTimingView;
        public float LinearWidth = 540; public float StartRadius = 145; public float TargetRadius = 45; public float RingThickness = 8; public float PinLength = 125; public float PinWidth = 5; public float StartAngle = 90;
        [Header("Grade Guides")]
        public Color NormalGradeColor = new Color(.2f, .55f, .8f, 1f);
        public Color GreatGradeColor = new Color(1f, .7f, .1f, 1f);
        public Color ExtremeGradeColor = Color.magenta;
        [Range(0, 1)] public float GuideOpacity = .32f;
    }
}
