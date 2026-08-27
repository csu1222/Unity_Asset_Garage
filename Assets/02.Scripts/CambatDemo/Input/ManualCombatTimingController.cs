using System;
using UnityEngine;

namespace AssetGarage.CombatDemo
{
    public readonly struct TimingState
    {
        public readonly float NormalizedTime, RemainingTime, NormalEnd, GreatStart, ExtremeStart;
        public readonly TimingGrade CurrentGrade; public readonly bool IsInputAccepted;
        public TimingState(float normalized, float remaining, float great, float extreme, TimingGrade grade, bool accepted) { NormalizedTime = normalized; RemainingTime = remaining; NormalEnd = great; GreatStart = great; ExtremeStart = extreme; CurrentGrade = grade; IsInputAccepted = accepted; }
    }

    public sealed class ManualCombatTimingController
    {
        private readonly ManualCombatBalanceConfig config; private float elapsed; private bool running, accepted;
        public TimingState State { get; private set; }
        public event Action<TimingState> Changed;
        public event Action Expired;
        public ManualCombatTimingController(ManualCombatBalanceConfig c) { config = c; }
        public void Start() { elapsed = 0; running = true; accepted = false; Publish(false); }
        public void Tick(float delta)
        {
            if (!running) return; elapsed += Mathf.Max(0, delta); if (elapsed >= config.DecisionDuration) { elapsed = config.DecisionDuration; running = false; Publish(true); Expired?.Invoke(); } else Publish(false);
        }
        public bool TryAccept(out TimingGrade grade)
        { if (!running || accepted) { grade = State.CurrentGrade; return false; } accepted = true; running = false; grade = TimingRules.Grade(elapsed / config.DecisionDuration, false, config.GreatStart, config.ExtremeStart); Publish(false); return true; }
        private void Publish(bool expired) { float n = Mathf.Clamp01(elapsed / Mathf.Max(.01f, config.DecisionDuration)); TimingGrade grade = TimingRules.Grade(n, expired, config.GreatStart, config.ExtremeStart); State = new TimingState(n, Mathf.Max(0, config.DecisionDuration - elapsed), config.GreatStart, config.ExtremeStart, grade, accepted); Changed?.Invoke(State); }
    }
}
