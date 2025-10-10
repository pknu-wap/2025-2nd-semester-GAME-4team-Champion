using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CG_ATK : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerDefense defense;
    [SerializeField] private PlayerHit hit;

    [Header("Timings")]
    [SerializeField] private float windup = 0.15f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.3f;

    [Header("Charge")]
    [SerializeField] private float chargeTime = 0.5f;   // 풀차지까지
    [SerializeField] private float minHold = 0.12f;     // 이 미만이면 미발동
    [SerializeField] private bool lockMoveDuringCharge = true;

    [Header("Hitbox")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float baseKnockback = 6f;
    [SerializeField] private float baseRange = 0.9f;
    [SerializeField] private float baseRadius = 0.6f;
    [SerializeField] private float chargeDamageMul = 2.0f;
    [SerializeField] private float chargeKnockMul = 2.0f;
    [SerializeField] private float chargeRangeMul = 1.2f;
    [SerializeField] private float chargeRadiusMul = 1.2f;

    public bool IsAttacking { get; private set; }

    private const string LOCK_CGATK = "CG_ATK";

    // charge state
    private bool isCharging = false;
    private float chargeStartTime = 0f;
    private Coroutine waitCo;
    private Coroutine attackMoveLockCo;
    private Collider2D[] myCols;

    public void Bind(PlayerAttack atk, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        attack = atk;
        combat = c; moveRef = m; animator = a;
        myCols = GetComponents<Collider2D>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!hit) hit = GetComponent<PlayerHit>();
    }

    public void OnChargeStarted()
    {
        if ((defense != null && (defense.IsStaminaBroken || defense.IsParryLocked)) ||
            (hit != null && hit.InHitstun) || IsAttacking) return;

        isCharging = true;
        chargeStartTime = Time.time;

        animator?.SetBool("Charging", true);

        if (lockMoveDuringCharge && moveRef)
        {
            moveRef.SetMovementLocked(true, false, true);
        }

        if (waitCo != null) StopCoroutine(waitCo);
        waitCo = StartCoroutine(WaitThenFire());
    }

    public void OnChargeCanceled()
    {
        if (!isCharging) return;

        float held = Time.time - chargeStartTime;

        isCharging = false;
        animator?.SetBool("Charging", false);

        if (waitCo != null) { StopCoroutine(waitCo); waitCo = null; }

        // 미니멈 미만이면 해제만
        if (held < minHold)
        {
            if (lockMoveDuringCharge) moveRef?.SetMovementLocked(false, false);
            return;
        }

        // 풀차지 이전에 떼면 발동 안됨(풀차지는 WaitThenFire에서 즉시 발동)
        if (lockMoveDuringCharge) moveRef?.SetMovementLocked(false, false);
    }

    private IEnumerator WaitThenFire()
    {
        while (isCharging)
        {
            if (Time.time - chargeStartTime >= chargeTime)
            {
                isCharging = false;
                yield return StartCoroutine(DoChargeAttack());
                break;
            }
            yield return null;
        }
        waitCo = null;
    }

    private IEnumerator DoChargeAttack()
    {
        IsAttacking = true;

        if (animator)
        {
            animator.ResetTrigger("Atk1"); animator.ResetTrigger("Atk2");
            animator.ResetTrigger("Atk3"); animator.ResetTrigger("Atk4");
            animator.SetTrigger("AtkCharge");
        }

        // 공격 구간 동안 이동락 유지
        if (lockMoveDuringCharge && moveRef)
        {
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(windup + 0.07f + active + recovery));
        }

        yield return new WaitForSeconds(windup + 0.07f);

        DoHitbox(baseDamage * chargeDamageMul,
                 baseKnockback * chargeKnockMul,
                 baseRange * chargeRangeMul,
                 baseRadius * chargeRadiusMul);

        animator?.SetBool("Charging", false);

        yield return new WaitForSeconds(active + recovery);

        IsAttacking = false;

        if (lockMoveDuringCharge) moveRef?.SetMovementLocked(false, false);
    }

    private IEnumerator LockMoveFor(float seconds, bool zeroVelocity = true)
    {
        moveRef?.AddMovementLock(LOCK_CGATK, false, zeroVelocity);
        yield return new WaitForSeconds(seconds);
        moveRef?.RemoveMovementLock(LOCK_CGATK, false);
        attackMoveLockCo = null;
        float lockTime = windup + active + recovery;
        combat?.BlockStaminaRegenFor(lockTime);
    }

    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f)
            ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMask);
        var seen = new HashSet<Collider2D>();
        bool any = false;

        foreach (var h in hits)
        {
            if (!h || seen.Contains(h)) continue;
            seen.Add(h);

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;

            // ★ 여기! IDamageable 로 수정
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                dmgTarget.ApplyHit(dmg, knock, toEnemy, gameObject);
                any = true;
            }
            else { any = true; }
        }

        if (any) combat.EnterCombat("HitEnemy");
    }
}
