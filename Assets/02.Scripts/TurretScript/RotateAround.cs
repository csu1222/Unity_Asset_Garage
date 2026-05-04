using UnityEngine;

public class RotateAround : MonoBehaviour
{
    [Header("Rotate Center")]
    [SerializeField] private Transform center;

    [Header("Rotate Speed")]
    [SerializeField] private float speed = 30f;

    private void Update()
    {
        if (center == null)
        {
            Debug.Log($"[RotateAround] {name} 의 센터가 할당되지 않았습니다.");
            return;
        }

        transform.RotateAround(center.position, Vector3.up, speed * Time.deltaTime);
    }
}
