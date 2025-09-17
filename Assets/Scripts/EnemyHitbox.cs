using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHitbox : MonoBehaviour, IParryable
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockback = 5f;
    [SerializeField] private bool parryable = true;
    [SerializeField] private float attackInterval = 1f; // ★ 1초 간격

    [Header("Optional (for parry reaction)")]
    [SerializeField] private Rigidbody2D enemyRb; // 패링 당했을 때 넉백용(선택)

    // 트리거 안의 대상들을 추적
    private readonly HashSet<PlayerCombat> targets = new HashSet<PlayerCombat>();
    private Coroutine attackLoop;

    private void Reset()
    {
        // 권장 셋업: 히트박스에 Trigger Collider가 붙어 있어야 함
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;

        if (!enemyRb)
            enemyRb = GetComponentInParent<Rigidbody2D>(); // 부모(적 본체)의 RB를 참조
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
            // 트리거 안의 모든 플레이어에게 타격
            foreach (var pc in targets)
            {
                if (pc == null) continue;
                Vector2 hitDir = ((Vector2)pc.transform.position - (Vector2)transform.position).normalized; // 적→플레이어
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

    // 패링당했을 때(선택): 살짝 뒤로 밀리게
    public void OnParried(Vector3 parrySourcePosition)
    {
        if (enemyRb == null) return;
        Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized; // 플레이어 반대방향
        enemyRb.AddForce(dir * 6f, ForceMode2D.Impulse);
        // 여기서 스턴/애니 취소 등 추가 연출 가능
    }
}
