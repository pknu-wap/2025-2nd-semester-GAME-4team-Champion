using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHitbox : MonoBehaviour, IParryable
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockback = 5f;
    [SerializeField] private bool parryable = true;
    [SerializeField] private float attackInterval = 1f; // 1초 간격

    [Header("Optional (for parry reaction)")]
    [SerializeField] private Rigidbody2D enemyRb; // 패링 시 넉백용(선택)

    // 트리거 안의 플레이어(PlayerHit)들을 추적
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
            // 스냅샷을 돌면서 null 정리
            foreach (var ph in new List<PlayerHit>(targets))
            {
                if (ph == null)
                {
                    targets.Remove(ph);
                    continue;
                }

                // 적 → 플레이어 방향
                Vector2 hitDir = ((Vector2)ph.transform.position - (Vector2)transform.position).normalized;
                ph.OnHit(damage, knockback, hitDir, parryable, gameObject);
            }
            yield return wait;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 루트/자식 어디에 붙어 있어도 찾도록 InParent 우선
        var ph = other.GetComponentInParent<PlayerHit>() ?? other.GetComponent<PlayerHit>();
        if (ph != null) targets.Add(ph);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var ph = other.GetComponentInParent<PlayerHit>() ?? other.GetComponent<PlayerHit>();
        if (ph != null) targets.Remove(ph);
    }

    // 패링(Weaving) 당했을 때(선택): 살짝 뒤로 밀리게
    public void OnParried(Vector3 parrySourcePosition)
    {
        if (enemyRb == null) return;
        Vector2 dir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized; // 플레이어 반대방향
        enemyRb.AddForce(dir * 6f, ForceMode2D.Impulse);
        // TODO: 스턴/애니 취소 등 추가 연출
    }
}
