using UnityEngine;

namespace SystemicOverload.Interaction
{
    /// <summary>
    /// 상호작용 가능한 월드 오브젝트의 최소 계약입니다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 현재 오브젝트의 상호작용 프롬프트를 반환합니다.
        /// </summary>
        string GetPrompt();

        /// <summary>
        /// 상호작용 실행 로직입니다.
        /// </summary>
        void Interact(GameObject actor);
    }
}
