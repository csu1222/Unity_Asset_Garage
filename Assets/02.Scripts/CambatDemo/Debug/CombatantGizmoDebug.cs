using UnityEngine;

namespace AssetGarage.CombatDemo
{
    public sealed class CombatantGizmoDebug : MonoBehaviour
    {
        [SerializeField] private string label = "Combatant";
        [SerializeField] private CombatStats stats = new CombatStats(100,100,20,20,15);
        private void OnDrawGizmos() { Gizmos.color = name.Contains("Player") ? Color.cyan : Color.red; Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.4f, .35f); }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected() { UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f, $"{label}\nHP {stats.CurrentHP}/{stats.MaxHP}\nATK {stats.Attack} DEF {stats.Defense} REC {stats.Recovery}"); }
#endif
    }
}
