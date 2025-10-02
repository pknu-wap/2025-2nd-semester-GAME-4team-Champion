using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class C_ATK : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerAttack attack;

    [Header("Timings")]
    [SerializeField] private float windup = 0.02f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.10f;
    [SerializeField] private bool lockMoveDuringCounter = true;

    [Header("Multipliers")]
    [SerializeField] private float damageMul = 2.0f;
    [SerializeField] private float knockMul = 2.0f;
    [SerializeField] private float rangeMul = 1.1f;
    [SerializeField] private float radiusMul = 1.0f;

    [Header("Hitbox")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;

    // state
    private bool counterArmed = false;
    private float counterExpireTime = -999f;
    private bool isCountering = false;
    private Coroutine moveLockCo;
    private Collider2D[] myCols;

    public void Bind(PlayerAttack atk, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        attack = atk; combat = c; moveRef = m; animator = a;
        myCols = GetComponents<Collider2D>();
    }

    // PlayerAttack에서 호출: 카운터 창 열기
    public void ArmCounter(float windowSeconds)
    {
        counterArmed = windowSeconds > 0f;
        counterExpireTime = Time.time + Mathf.Max(0f, windowSeconds);
        Debug.Log($"[COUNTER-ARM] window={windowSeconds:F2}s, until={counterExpireTime:F2}, now={Time.time:F2}");
    }

    // PlayerAttack.OnAttackStarted 에서 먼저 호출됨
    public bool TryTriggerCounterOnAttackPress() => TryFireOnAttackPress();

    // 내부: 실제 발동 판정
    public bool TryFireOnAttackPress()
    {
        if (!counterArmed || Time.time > counterExpireTime || isCountering)
            return false;

        StartCoroutine(DoCounter());
        return true;
    }

    private IEnumerator DoCounter()
    {
        isCountering = true;
        counterArmed = false; // 창 소모

        // Weaving 번호와 매칭해서 Counter1~3 중 하나 재생
        int idx = (attack != null) ? attack.GetWeavingIndexForCounter() : 1;
        idx = Mathf.Clamp(idx, 1, 3);
        animator?.SetTrigger($"Counter{idx}"); 
        if (lockMoveDuringCounter)
        {
            float lockTime = windup + active + recovery;
            if (moveLockCo != null) StopCoroutine(moveLockCo);
            moveLockCo = StartCoroutine(LockMoveFor(lockTime, true));
        }

        // 선딜
        yield return new WaitForSeconds(windup);

        // 히트박스
        float dmg = attack ? attack.baseStats.baseDamage * damageMul : 10f * damageMul;
        float knock = attack ? attack.baseStats.baseKnockback * knockMul : 6f * knockMul;
        float range = attack ? attack.baseStats.baseRange * rangeMul : 0.9f * rangeMul;
        float radius = attack ? attack.baseStats.baseRadius * radiusMul : 0.6f * radiusMul;
        DoHitbox(dmg, knock, range, radius);

        // 전투 진입 보장
        combat?.EnterCombat("Counter");

        // 활성+후딜
        yield return new WaitForSeconds(active + recovery);

        isCountering = false;
    }

    private IEnumerator LockMoveFor(float seconds, bool zeroVelocity)
    {
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: zeroVelocity);
        yield return new WaitForSeconds(seconds);
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        moveLockCo = null;
        float lockTime = windup + active + recovery;
        combat?.BlockStaminaRegenFor(lockTime);
    }

    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMask);
        var seen = new HashSet<Collider2D>();
        bool any = false;

        foreach (var h in hits)
        {
            if (!h || seen.Contains(h)) continue;
            seen.Add(h);

            if (ignoreEnemyCollisionDuringActive)
                IgnoreForSeconds(h.transform.root, active + extraIgnoreTime);

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;

            // ★ 오타 수정: IDamageable (NOT "IDamageABLE")
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                dmgTarget.ApplyHit(dmg, knock, toEnemy, gameObject);
                any = true;
            }
            else any = true;
        }

        if (any) combat?.EnterCombat("CounterHit");
    }

    private void IgnoreForSeconds(Transform enemyRoot, float seconds)
    {
        if (myCols == null) myCols = GetComponents<Collider2D>();
        var enemyCols = enemyRoot.GetComponentsInChildren<Collider2D>();
        foreach (var my in myCols)
        {
            if (!my) continue;
            foreach (var ec in enemyCols)
            {
                if (!ec) continue;
                Physics2D.IgnoreCollision(my, ec, true);
                StartCoroutine(ReenableLater(my, ec, seconds));
            }
        }
    }

    private IEnumerator ReenableLater(Collider2D a, Collider2D b, float t)
    {
        yield return new WaitForSeconds(t);
        if (a && b) Physics2D.IgnoreCollision(a, b, false);
    }
}
