using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHitbox : MonoBehaviour, IParryable
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockback = 5f;
    [SerializeField] private bool parryable = true;
    [SerializeField] private float attackInterval = 1f; // 1�� ����

    [Header("Optional (for parry reaction)")]
    [SerializeField] private Rigidbody2D enemyRb; // �и� �� �˹��(����)

    // Ʈ���� ���� �÷��̾�(PlayerHit)���� ����
    private readonly HashSet<PlayerHit> targets = new HashSet<PlayerHit>();
    private Coroutine attackLoop;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;

        if (!enemyRb)
            enemyRb = GetComponentInParent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        attackLoop = StartCoroutine(AttackLoop());
    }

    private void OnDisable()
    {
        if (attackLoop != null) StopCoroutine(attackLoop);
    }

    private IEnumerator AttackLoop()
    {
        var wait = new WaitForSeconds(attackInterval);
        while (true)
        {
            // �������� ���鼭 null ����
            foreach (var ph in new List<PlayerHit>(targets))
            {
                if (ph == null)
                {
                    targets.Remove(ph);
                    continue;
                }

                // �� �� �÷��̾� ����
                Vector2 hitDir = ((Vector2)ph.transform.position - (Vector2)transform.position).normalized;
                ph.OnHit(damage, knockback, hitDir, parryable, gameObject);
            }
            yield return wait;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // �÷��̾� ��Ʈ/�ڽ� ��� �پ� �־ ã���� InParent �켱
        var ph = other.GetComponentInParent<PlayerHit>() ?? other.GetComponent<PlayerHit>();
        if (ph != null) targets.Add(ph);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var ph = other.GetComponentInParent<PlayerHit>() ?? other.GetComponent<PlayerHit>();
        if (ph != null) targets.Remove(ph);
    }

    // �и�(Weaving) ������ ��(����): ��¦ �ڷ� �и���
    public void OnParried(Vector3 parrySourcePosition)
    {
        if (enemyRb == null) return;
        Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized; // �÷��̾� �ݴ����
        enemyRb.AddForce(dir * 6f, ForceMode2D.Impulse);
        // TODO: ����/�ִ� ��� �� �߰� ����
    }
}
