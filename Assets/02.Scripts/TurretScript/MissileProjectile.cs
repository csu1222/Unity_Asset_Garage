using UnityEngine;

public class MissileProjectile : MonoBehaviour
{
    private Vector3 moveDirection = Vector3.forward;
    private float moveSpeed = 12f;
    private float lifeTime = 3f;

    private float elapsedTime = 0f;

    public void Initialize(Vector3 direction, float speed, float projectileLifeTime)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        lifeTime = projectileLifeTime;
    }

    private void Update()
    {
        Move();
        CheckLifeTime();
    }

    private void Move()
    {
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void CheckLifeTime()
    {
        elapsedTime += Time.deltaTime;

        if (elapsedTime >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}