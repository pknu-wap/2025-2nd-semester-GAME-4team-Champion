using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour
{
    public float lifeTime = 3f;
    public float speed = 10f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Collider2D col;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector2 dir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = dir * speed;

        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(ApplySlow(other));
        }
    }

    IEnumerator ApplySlow(Collider2D other)
    {
        sr.enabled = false;
        col.enabled = false;
        rb.linearVelocity = Vector2.zero;

        PlayerMoveBehaviour player = other.GetComponent<PlayerMoveBehaviour>();
        player.StartCoroutine(player.Slow());
        
        yield return null;
        Destroy(gameObject);
    }
}
