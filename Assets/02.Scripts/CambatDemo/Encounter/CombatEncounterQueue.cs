using System;
using System.Collections.Generic;

namespace AssetGarage.CombatDemo
{
    public sealed class CombatEncounterQueue
    {
        private readonly List<EncounterData> items = new List<EncounterData>();
        public IReadOnlyList<EncounterData> Items => items;
        public EncounterData Active { get; private set; }
        public void Enqueue(EncounterData encounter) { encounter.State = EncounterState.Queued; items.Add(encounter); ActivateNext(); }
        public void Update(float deltaTime, Action<EncounterData> timeoutResolver)
        {
            var expired = new List<EncounterData>();
            foreach (EncounterData item in items) if (item.State == EncounterState.Queued) { item.RemainingWait -= Math.Max(0, deltaTime); if (item.RemainingWait <= 0) expired.Add(item); }
            foreach (EncounterData item in expired) { item.State = EncounterState.Resolving; timeoutResolver(item); item.State = EncounterState.Resolved; items.Remove(item); }
        }
        public void ResolveActive() { if (Active == null) return; Active.State = EncounterState.Resolved; items.Remove(Active); Active = null; ActivateNext(); }
        private void ActivateNext() { if (Active != null) return; foreach (EncounterData item in items) if (item.State == EncounterState.Queued) { Active = item; item.State = EncounterState.Active; break; } }
    }
}
