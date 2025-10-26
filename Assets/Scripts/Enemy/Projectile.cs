using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float lifeTime = 5f;
    public float speed = 10f;
    public float damage = 10f;
    public float slow = 3f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * speed;
        }

        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerMoveBehaviour move = other.GetComponent<PlayerMoveBehaviour>();
            if (move != null)
            {
                move.moveSpeed -= slow;
            }

            Destroy(gameObject);
        }
    }
}
