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
    [SerializeField] private float recovery = 0.12f;

    [Header("Charge")]
    [SerializeField] private float chargeTime = 0.5f;   // Ǯ��������
    [SerializeField] private float minHold = 0.12f;     // �� �̸��̸� �̹ߵ�
    [SerializeField] private bool lockMoveDuringCharge = true;

    [Header("Hitbox")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float baseKnockback = 6f;
    [SerializeField] private float baseRange = 0.9f;
    [SerializeField] private float baseRadius = 0.6f;
    [SerializeField] private float chargeDamageMul = 5.0f;
    [SerializeField] private float chargeKnockMul = 2.0f;
    [SerializeField] private float chargeRangeMul = 1.2f;
    [SerializeField] private float chargeRadiusMul = 1.2f;

    public bool IsAttacking { get; private set; }

    // charge state
    private bool isCharging = false;
    private float chargeStartTime = 0f;
    private Coroutine waitCo;
    private Coroutine moveLockCo;
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

        // �̴ϸ� �̸��̸� ������
        if (held < minHold)
        {
            if (lockMoveDuringCharge) moveRef?.SetMovementLocked(false, false);
            return;
        }

        // Ǯ���� ������ ���� �ߵ� �ȵ�(Ǯ������ WaitThenFire���� ��� �ߵ�)
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

        // ���� ���� ���� �̵��� ����
        if (lockMoveDuringCharge && moveRef)
        {
            if (moveLockCo != null) StopCoroutine(moveLockCo);
            moveLockCo = StartCoroutine(LockMoveFor(windup + 0.07f + active + recovery));
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

    private IEnumerator LockMoveFor(float seconds)
    {
        moveRef?.SetMovementLocked(true, false, true);
        yield return new WaitForSeconds(seconds);
        moveRef?.SetMovementLocked(false, false);
        moveLockCo = null;
        float lockTime = (windup + 0.07f) + active + recovery;
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

            // �� ����! IDamageable �� ����
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
