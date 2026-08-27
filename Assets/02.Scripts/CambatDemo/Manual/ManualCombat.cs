using System;
using UnityEngine;

namespace AssetGarage.CombatDemo
{
    public readonly struct TurnResolution
    {
        public readonly int PlayerDamage, EnemyDamage, PlayerHeal, EnemyHeal, CounterDamage;
        public readonly float IncomingDamage, EffectiveDefense;
        public readonly bool PlayerInterrupted, EnemyInterrupted, EnemyEmpowered;
        public TurnResolution(int pd, int ed, int ph, int eh, int counter, float incoming, float defense, bool pi, bool ei, bool empowered)
        { PlayerDamage = pd; EnemyDamage = ed; PlayerHeal = ph; EnemyHeal = eh; CounterDamage = counter; IncomingDamage = incoming; EffectiveDefense = defense; PlayerInterrupted = pi; EnemyInterrupted = ei; EnemyEmpowered = empowered; }
    }

    public static class TimingRules
    {
        public static TimingGrade Grade(float normalized, bool expired, float greatStart = .85f, float extremeStart = .925f)
        {
            if (expired) return TimingGrade.Failed;
            float t = Mathf.Clamp01(normalized);
            if (t + 0.000001f >= extremeStart) return TimingGrade.Extreme;
            if (t + 0.000001f >= greatStart) return TimingGrade.Great;
            return TimingGrade.Normal;
        }
    }

    public sealed class PressureState
    {
        public int Value { get; private set; }
        public void Record(TimingGrade grade) => Value = grade == TimingGrade.Great || grade == TimingGrade.Extreme ? 0 : Value + 1;
        public bool ShouldEmpower(int threshold) => Value >= Math.Max(1, threshold);
        public void ConsumeEmpowerment() => Value = 0;
    }

    public sealed class ManualCombatResolver
    {
        private readonly ManualCombatBalanceConfig config;
        public ManualCombatResolver(ManualCombatBalanceConfig balance) { config = balance; config.Sanitize(); }

        public TurnResolution Resolve(CombatStats player, CombatStats enemy, CombatAction playerAction, CombatAction enemyAction, TimingGrade grade, bool enemyEmpowered)
        {
            float playerMultiplier = grade == TimingGrade.Great ? config.GreatMultiplier : grade == TimingGrade.Extreme ? config.ExtremeMultiplier : 1;
            float enemyMultiplier = enemyEmpowered ? config.EnemyPressureMultiplier : 1;
            int pd = 0, ed = 0, ph = 0, eh = 0, counter = 0; float incoming = 0, effectiveDefense = 0; bool pi = false, ei = false;
            if (grade == TimingGrade.Failed || playerAction == CombatAction.None)
                ResolveEnemyOnly(player, enemy, enemyAction, enemyMultiplier, ref pd, ref eh, ref incoming);
            else if (playerAction == CombatAction.Attack)
                ResolvePlayerAttack(player, enemy, enemyAction, playerMultiplier, enemyMultiplier, ref pd, ref ed, ref eh, ref counter, ref incoming, ref effectiveDefense, ref ei);
            else if (playerAction == CombatAction.Defense)
                ResolvePlayerDefense(player, enemy, enemyAction, grade, playerMultiplier, enemyMultiplier, ref pd, ref eh, ref counter, ref incoming, ref effectiveDefense);
            else
                ResolvePlayerRecovery(player, enemy, enemyAction, playerMultiplier, enemyMultiplier, ref pd, ref ph, ref eh, ref incoming, ref pi);
            return new TurnResolution(pd, ed, ph, eh, counter, incoming, effectiveDefense, pi, ei, enemyEmpowered);
        }

        private void ResolvePlayerAttack(CombatStats p, CombatStats e, CombatAction ea, float pm, float em, ref int pd, ref int ed, ref int eh, ref int counter, ref float incoming, ref float defense, ref bool interrupted)
        {
            float attack = p.Attack * pm;
            if (ea == CombatAction.Defense) { defense = e.Defense * em; ed = e.Damage(Defended(attack, defense)); counter = p.Damage(e.Defense * em); }
            else { ed = e.Damage(attack); interrupted = ea == CombatAction.Recovery; if (!e.IsDead) ResolveEnemyOnly(p, e, ea, em, ref pd, ref eh, ref incoming); }
        }

        private void ResolvePlayerDefense(CombatStats p, CombatStats e, CombatAction ea, TimingGrade grade, float pm, float em, ref int pd, ref int eh, ref int counter, ref float incoming, ref float defense)
        {
            if (ea == CombatAction.Attack) { incoming = e.Attack * em; defense = p.Defense * pm; pd = grade == TimingGrade.Extreme ? 0 : p.Damage(Defended(incoming, defense)); counter = e.Damage(p.Defense * pm); }
            else if (ea == CombatAction.Recovery) eh = e.Heal(e.Recovery * em);
        }

        private void ResolvePlayerRecovery(CombatStats p, CombatStats e, CombatAction ea, float pm, float em, ref int pd, ref int ph, ref int eh, ref float incoming, ref bool interrupted)
        {
            if (ea == CombatAction.Attack) { interrupted = true; incoming = e.Attack * em; pd = p.Damage(incoming); }
            else { ph = p.Heal(p.Recovery * pm); if (ea == CombatAction.Recovery) eh = e.Heal(e.Recovery * em); }
        }

        private void ResolveEnemyOnly(CombatStats p, CombatStats e, CombatAction action, float multiplier, ref int pd, ref int eh, ref float incoming)
        { if (action == CombatAction.Attack) { incoming = e.Attack * multiplier; pd = p.Damage(incoming); } else if (action == CombatAction.Recovery) eh = e.Heal(e.Recovery * multiplier); }
        private float Defended(float incoming, float defense) => Mathf.Max(1, Mathf.Ceil(incoming * config.DefenseScale / (Mathf.Max(0, defense) + config.DefenseScale)));
    }
}
