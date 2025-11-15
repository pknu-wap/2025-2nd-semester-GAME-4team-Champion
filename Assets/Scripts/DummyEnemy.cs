using UnityEngine;

public class DummyEnemy : MonoBehaviour, IDamageable, IParryable
{
    [SerializeField] private float hp = 1000f;
    [SerializeField] private Rigidbody2D rb;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void ApplyHit(float damage, Vector2 hitDirFromPlayer, GameObject attacker)
    {
        hp -= damage;
        Debug.Log($"[DummyEnemy] -{damage} HP={hp}");
        if (hp <= 0f) Destroy(gameObject);
    }

    public void OnParried(Vector3 parrySourcePosition)
    {
        // �÷��̾ �и� ������ �� ���� (����)
        if (rb)
        {
            Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized;
            rb.AddForce(dir * 6f, ForceMode2D.Impulse);
        }
        Debug.Log("[DummyEnemy] Parried!");
    }
}
