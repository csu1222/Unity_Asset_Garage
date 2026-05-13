using UnityEngine;

namespace SystemicOverload.Interaction
{
    /// <summary>
    /// 수업용 테스트에 사용하는 간단한 상호작용 구현입니다.
    /// </summary>
    public sealed class DebugInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "[E] 상호작용";
        [SerializeField] private string interactionLogMessage = "상호작용 실행";

        public string GetPrompt()
        {
            return promptText;
        }

        public void Interact(GameObject actor)
        {
            string actorName = actor != null ? actor.name : "Unknown";
            Debug.Log($"{interactionLogMessage} / Actor: {actorName} / Target: {name}");
        }
    }
}
