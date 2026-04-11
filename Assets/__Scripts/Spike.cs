using UnityEngine;

public class Spike : MonoBehaviour
{
    Vector2 moveDir = Vector2.right;
    float moveSpeed = 6f;

    [Header("Bounds")]
    [SerializeField] float minX = -8f;
    [SerializeField] float maxX = 7f;
    [SerializeField] float minY = -4f;
    [SerializeField] float maxY = 3f;
    [SerializeField] float offscreenMargin = 2f;

    public void Initialize(Vector2 direction, float speed)
    {
        moveDir = direction.normalized;
        moveSpeed = speed;
    }

    void Update()
    {
        transform.position += (Vector3)(moveDir * moveSpeed * Time.deltaTime);

        if (IsOutOfBounds())
            Destroy(gameObject);
    }

    bool IsOutOfBounds()
    {
        Vector3 p = transform.position;
        return p.x < minX - offscreenMargin ||
               p.x > maxX + offscreenMargin ||
               p.y < minY - offscreenMargin ||
               p.y > maxY + offscreenMargin;
    }
}