using System;
using UnityEngine;

namespace AssetGarage.CombatDemo
{
    public enum CombatAction { None, Attack, Defense, Recovery }
    public enum TimingGrade { Normal, Great, Extreme, Failed }
    public enum ResolveMethod { Manual, Auto, Negotiation, Escape }
    public enum CombatOutcome { Victory, Defeat, Negotiated, Escaped }
    public enum ResolutionReason { PlayerChoice, Timeout, Offline, TurnLimit, EscapeFailed }
    public enum EncounterState { Queued, Active, Resolving, Resolved }
    public enum TimingViewKind { Linear, ConvergingCircle, RadialPin }

    [Serializable]
    public sealed class CombatStats
    {
        [SerializeField, Min(1)] private int maxHP = 100;
        [SerializeField] private int currentHP = 100;
        [SerializeField, Min(0)] private float attack = 25;
        [SerializeField, Min(0)] private float defense = 20;
        [SerializeField, Min(0)] private float recovery = 15;

        public int MaxHP => maxHP;
        public int CurrentHP => currentHP;
        public float Attack => FiniteNonNegative(attack);
        public float Defense => FiniteNonNegative(defense);
        public float Recovery => FiniteNonNegative(recovery);
        public bool IsDead => currentHP <= 0;

        public CombatStats() { }
        public CombatStats(int maxHp, int currentHp, float attackValue, float defenseValue, float recoveryValue)
        {
            maxHP = Math.Max(1, maxHp);
            currentHP = Mathf.Clamp(currentHp, 0, maxHP);
            attack = FiniteNonNegative(attackValue);
            defense = FiniteNonNegative(defenseValue);
            recovery = FiniteNonNegative(recoveryValue);
        }

        public int Damage(float amount)
        {
            int applied = Mathf.Clamp(Mathf.CeilToInt(FiniteNonNegative(amount)), 0, currentHP);
            currentHP -= applied;
            return applied;
        }

        public int Heal(float amount)
        {
            int applied = Mathf.Clamp(Mathf.CeilToInt(FiniteNonNegative(amount)), 0, maxHP - currentHP);
            currentHP += applied;
            return applied;
        }

        public void Restore() => currentHP = maxHP;
        public CombatStats Copy() => new CombatStats(maxHP, currentHP, attack, defense, recovery);

        private static float FiniteNonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
    }

    [Serializable]
    public sealed class EncounterData
    {
        public string EncounterId = "encounter";
        public string RouteId = "demo-route";
        public string CaravanId = "demo-caravan";
        public string DisplayName = "Greybox Enemy";
        public long CreatedTick;
        public double OccurrenceTime;
        public float RemainingWait = 12;
        public CombatStats Enemy = new CombatStats(90, 90, 20, 18, 12);
        public EncounterState State = EncounterState.Queued;
    }

    public sealed class CombatResolutionResult
    {
        public string EncounterId { get; set; }
        public string RouteId { get; set; }
        public ResolveMethod ResolveMethod { get; set; }
        public CombatOutcome Outcome { get; set; }
        public ResolutionReason ResolutionReason { get; set; }
        public int PlayerHPBefore { get; set; }
        public int PlayerHPAfter { get; set; }
        public int HPDamage => Math.Max(0, PlayerHPBefore - PlayerHPAfter);
        public int GoldCost { get; set; }
        public double? SuccessProbability { get; set; }
        public double? RandomRoll { get; set; }
    }
}
