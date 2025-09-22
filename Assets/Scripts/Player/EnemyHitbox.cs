using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHitbox : MonoBehaviour, IParryable
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockback = 5f;
    [SerializeField] private bool parryable = true;
    [SerializeField] private float attackInterval = 1f; // �� 1�� ����

    [Header("Optional (for parry reaction)")]
    [SerializeField] private Rigidbody2D enemyRb; // �и� ������ �� �˹��(����)

    // Ʈ���� ���� ������ ����
    private readonly HashSet<PlayerCombat> targets = new HashSet<PlayerCombat>();
    private Coroutine attackLoop;

    private void Reset()
    {
        // ���� �¾�: ��Ʈ�ڽ��� Trigger Collider�� �پ� �־�� ��
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;

        if (!enemyRb)
            enemyRb = GetComponentInParent<Rigidbody2D>(); // �θ�(�� ��ü)�� RB�� ����
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
            // Ʈ���� ���� ��� �÷��̾�� Ÿ��
            foreach (var pc in targets)
            {
                if (pc == null) continue;
                Vector2 hitDir = ((Vector2)pc.transform.position - (Vector2)transform.position).normalized; // �����÷��̾�
                pc.OnHit(damage, knockback, hitDir, parryable, gameObject);
            }
            yield return wait;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerCombat>();
        if (pc != null) targets.Add(pc);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerCombat>();
        if (pc != null) targets.Remove(pc);
    }

    // �и������� ��(����): ��¦ �ڷ� �и���
    public void OnParried(Vector3 parrySourcePosition)
    {
        if (enemyRb == null) return;
        Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized; // �÷��̾� �ݴ����
        enemyRb.AddForce(dir * 6f, ForceMode2D.Impulse);
        // ���⼭ ����/�ִ� ��� �� �߰� ���� ����
    }
}
