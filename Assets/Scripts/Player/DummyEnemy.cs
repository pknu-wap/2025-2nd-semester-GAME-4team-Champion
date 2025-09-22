using UnityEngine;

public class DummyEnemy : MonoBehaviour, IDamageable, IParryable
{
    [SerializeField] private float hp = 100f;
    [SerializeField] private Rigidbody2D rb;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker)
    {
        hp -= damage;
        Debug.Log($"[DummyEnemy] -{damage} HP={hp}");
        if (rb) rb.AddForce(hitDirFromPlayer.normalized * knockback, ForceMode2D.Impulse);
        if (hp <= 0f) Destroy(gameObject);
    }

    public void OnParried(Vector3 parrySourcePosition)
    {
        // 플레이어에 패링 당했을 때 반응 (선택)
        if (rb)
        {
            Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized;
            rb.AddForce(dir * 6f, ForceMode2D.Impulse);
        }
        Debug.Log("[DummyEnemy] Parried!");
    }
}
