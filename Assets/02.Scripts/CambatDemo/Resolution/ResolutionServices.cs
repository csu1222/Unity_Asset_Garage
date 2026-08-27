using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AssetGarage.CombatDemo
{
    public static class DeterministicRoll
    {
        public static double Value(int worldSeed, string encounterId, string caravanId, long createdTick, string salt)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                Mix(ref hash, worldSeed.ToString()); Mix(ref hash, encounterId); Mix(ref hash, caravanId); Mix(ref hash, createdTick.ToString()); Mix(ref hash, salt);
                return (hash >> 11) * (1.0 / 9007199254740992.0);
            }
        }
        private static void Mix(ref ulong hash, string value) { foreach (char c in value ?? string.Empty) { hash ^= c; hash *= 1099511628211UL; } hash ^= 255; hash *= 1099511628211UL; }
    }

    public sealed class AutoCombatResolver
    {
        private readonly AutoCombatBalanceConfig config;
        public AutoCombatResolver(AutoCombatBalanceConfig value) { config = value; config.Sanitize(); }
        public float Power(CombatStats s) => s.CurrentHP * config.CurrentHPWeight + s.Attack * config.AttackWeight + s.Defense * config.DefenseWeight + s.Recovery * config.RecoveryWeight;
        public double WinProbability(float playerPower, float enemyPower)
        {
            if (enemyPower <= 0) return playerPower <= 0 ? .5 : 1;
            float ratio = playerPower / enemyPower;
            if (ratio >= config.GuaranteedWinRatio) return 1;
            if (ratio <= config.GuaranteedLoseRatio) return 0;
            return 1.0 / (1.0 + Math.Pow(10, -(playerPower - enemyPower) / config.WinProbabilityScale));
        }
        public float VictoryDamageRate(float pp, float ep)
        { float sum = pp + ep; float raw = sum <= 0 ? config.MinimumVictoryDamageRate : ep / sum * config.VictoryDamageScale; return Mathf.Clamp(raw, config.MinimumVictoryDamageRate, config.MaximumVictoryDamageRate); }
        public CombatResolutionResult Resolve(CombatStats player, EncounterData encounter, int worldSeed, ResolutionReason reason)
        {
            int before = player.CurrentHP; float pp = Power(player), ep = Power(encounter.Enemy); double probability = WinProbability(pp, ep); double roll = DeterministicRoll.Value(worldSeed, encounter.EncounterId, encounter.CaravanId, encounter.CreatedTick, "AutoCombat"); bool won = roll < probability;
            if (won) player.Damage(Mathf.CeilToInt(player.MaxHP * VictoryDamageRate(pp, ep))); else player.Damage(player.CurrentHP);
            return Result(encounter, ResolveMethod.Auto, won ? CombatOutcome.Victory : CombatOutcome.Defeat, reason, before, player.CurrentHP, 0, probability, roll);
        }
        internal static CombatResolutionResult Result(EncounterData e, ResolveMethod method, CombatOutcome outcome, ResolutionReason reason, int before, int after, int gold, double? p, double? roll) => new CombatResolutionResult { EncounterId = e.EncounterId, RouteId = e.RouteId, ResolveMethod = method, Outcome = outcome, ResolutionReason = reason, PlayerHPBefore = before, PlayerHPAfter = after, GoldCost = gold, SuccessProbability = p, RandomRoll = roll };
    }

    public sealed class NegotiationResolver
    {
        private readonly ResolutionBalanceConfig config; private readonly AutoCombatResolver auto;
        public NegotiationResolver(ResolutionBalanceConfig c, AutoCombatResolver a) { config = c; auto = a; }
        public int Cost(CombatStats player, CombatStats enemy) => Mathf.Clamp(Mathf.RoundToInt(config.BaseNegotiationCost + (auto.Power(enemy) - auto.Power(player)) * config.CostPerPowerDifference), config.MinimumNegotiationCost, config.MaximumNegotiationCost);
        public bool TryResolve(CombatStats player, EncounterData encounter, ref int gold, out CombatResolutionResult result)
        { int cost = Cost(player, encounter.Enemy); if (gold < cost) { result = null; return false; } gold -= cost; result = AutoCombatResolver.Result(encounter, ResolveMethod.Negotiation, CombatOutcome.Negotiated, ResolutionReason.PlayerChoice, player.CurrentHP, player.CurrentHP, cost, 1, null); return true; }
    }

    public sealed class EscapeResolver
    {
        private readonly ResolutionBalanceConfig config;
        public EscapeResolver(ResolutionBalanceConfig c) { config = c; }
        public double Chance(float modifiers) => Mathf.Clamp(config.BaseEscapeChance + modifiers, config.MinimumEscapeChance, config.MaximumEscapeChance);
        public CombatResolutionResult Resolve(CombatStats player, EncounterData encounter, int worldSeed, float modifiers = 0)
        { double chance = Chance(modifiers), roll = DeterministicRoll.Value(worldSeed, encounter.EncounterId, encounter.CaravanId, encounter.CreatedTick, "Escape"); bool success = roll < chance; return AutoCombatResolver.Result(encounter, ResolveMethod.Escape, success ? CombatOutcome.Escaped : CombatOutcome.Defeat, success ? ResolutionReason.PlayerChoice : ResolutionReason.EscapeFailed, player.CurrentHP, player.CurrentHP, 0, chance, roll); }
    }

    public sealed class OfflineCombatResolver
    {
        private readonly AutoCombatResolver auto; public OfflineCombatResolver(AutoCombatResolver resolver) { auto = resolver; }
        public IReadOnlyList<CombatResolutionResult> Resolve(CombatStats player, IEnumerable<EncounterData> encounters, int seed)
        { var results = new List<CombatResolutionResult>(); foreach (EncounterData e in encounters.OrderBy(x => x.OccurrenceTime).ThenBy(x => x.EncounterId, StringComparer.Ordinal)) results.Add(auto.Resolve(player, e, seed, ResolutionReason.Offline)); return results; }
    }
}
