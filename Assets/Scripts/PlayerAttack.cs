using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    private PlayerMove inputWrapper;
    private InputAction attackAction;

    // 상태
    private int comboIndex = 0;
    private bool isAttacking = false;
    private bool nextBuffered = false;
    private float lastAttackEndTime = -999f;

    private bool attackHeld = false;
    private float attackPressedTime = -999f;
    private Coroutine chargeCo;
    private Coroutine attackCo;

    // 충돌 무시용
    private Collider2D[] myColliders;

    public void Bind(PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        combat = c; moveRef = m; animator = a;
    }

    private void Reset()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();

        inputWrapper = new PlayerMove();
        myColliders = GetComponents<Collider2D>();
    }

    private void OnEnable()
    {
        inputWrapper.Enable();
        attackAction = inputWrapper.asset.FindAction("Attack");
        if (attackAction != null)
        {
            attackAction.started += OnAttackStarted;
            attackAction.canceled += OnAttackCanceled;
        }
        else
        {
            Debug.LogWarning("[PlayerAttack] 'Attack' 액션이 없습니다.");
        }
    }

    private void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
        }
        inputWrapper.Disable();
        if (chargeCo != null) StopCoroutine(chargeCo);
        if (attackCo != null) StopCoroutine(attackCo);
    }

    private void OnAttackStarted(InputAction.CallbackContext _)
    {
        if (combat.IsStaminaBroken || combat.InHitstun || combat.IsParryLocked) return;
        attackHeld = true;
        attackPressedTime = Time.time;

        if (chargeCo != null) StopCoroutine(chargeCo);
        chargeCo = StartCoroutine(CheckChargeReady());
    }

    private void OnAttackCanceled(InputAction.CallbackContext _)
    {
        attackHeld = false;
        if (chargeCo != null) { StopCoroutine(chargeCo); chargeCo = null; }
        animator?.SetBool("Charging", false);

        if (combat.IsParryLocked) return;

        if (isAttacking)
        {
            nextBuffered = true;
        }
        else
        {
            if (Time.time - lastAttackEndTime > combat.ComboGapMax)
                comboIndex = 0;

            attackCo = StartCoroutine(DoAttackStep(comboIndex));
        }
    }

    private IEnumerator CheckChargeReady()
    {
        float t0 = Time.time;
        while (attackHeld && !isAttacking)
        {
            float held = Time.time - t0;
            if (held >= combat.ChargeTime)
            {
                animator?.SetBool("Charging", true);
                if (!isAttacking) attackCo = StartCoroutine(DoChargeAttack());
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator DoAttackStep(int step)
    {
        isAttacking = true;
        nextBuffered = false;

        if (combat.LockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(true, false);
        animator?.SetTrigger($"Atk{Mathf.Clamp(step + 1, 1, combat.MaxCombo)}");

        yield return new WaitForSeconds(combat.Windup);

        DoHitbox(combat.BaseDamage * DamageMulByStep(step),
                 combat.BaseKnockback * KnockbackMulByStep(step),
                 combat.BaseRange * RangeMulByStep(step),
                 combat.BaseRadius * RadiusMulByStep(step));

        yield return new WaitForSeconds(combat.Active);
        yield return new WaitForSeconds(combat.Recovery);

        if (combat.LockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(false, false);

        isAttacking = false;
        lastAttackEndTime = Time.time;

        if (nextBuffered && step < combat.MaxCombo - 1 && (Time.time - attackPressedTime) <= (combat.Active + combat.Recovery + combat.BufferWindow + 0.2f))
        {
            comboIndex = step + 1;
            attackCo = StartCoroutine(DoAttackStep(comboIndex));
        }
        else
        {
            comboIndex = 0;
            nextBuffered = false;
        }
        attackCo = null;
    }

    private IEnumerator DoChargeAttack()
    {
        isAttacking = true;

        if (combat.LockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(true, false);
        if (animator)
        {
            animator.ResetTrigger("Atk1"); animator.ResetTrigger("Atk2");
            animator.ResetTrigger("Atk3"); animator.ResetTrigger("Atk4");
            animator.SetBool("Charging", false);
            animator.SetTrigger("AtkCharge");
        }

        yield return new WaitForSeconds(combat.Windup + 0.07f);

        DoHitbox(combat.BaseDamage * combat.ChargeDamageMul,
                 combat.BaseKnockback * combat.ChargeKnockMul,
                 combat.BaseRange * combat.ChargeRangeMul,
                 combat.BaseRadius * combat.ChargeRadiusMul);

        yield return new WaitForSeconds(combat.Active + combat.Recovery);

        if (combat.LockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(false, false);

        isAttacking = false;
        lastAttackEndTime = Time.time;
        comboIndex = 0;
        attackCo = null;
    }

    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        if (combat.DebugLogs)
            Debug.Log($"[HITBOX] dmg={dmg}, knock={knock}, center={center}, r={radius}");

        var hits = Physics2D.OverlapCircleAll(center, radius, combat.EnemyMask);
        var seen = new HashSet<Collider2D>();
        bool anyHit = false; 

        foreach (var h in hits)
        {
            if (!h || seen.Contains(h)) continue;
            seen.Add(h);

            if (combat.IgnoreEnemyCollisionDuringActive)
                IgnoreCollisionsWith(h.transform.root, combat.Active + combat.ExtraIgnoreTime);

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                dmgTarget.ApplyHit(dmg, knock, toEnemy, gameObject);
                anyHit = true; // ★ 적중 플래그
            }
            else if (combat.DebugLogs)
            {
                Debug.Log($"[HIT] {h.name} take {dmg}, knock={knock}");
                anyHit = true; // ★ 히트로 간주(테스트용)
            }
        }

        if (anyHit) combat.EnterCombat("HitEnemy"); // ★ 첫 히트 시 전투 진입
    }


    private void IgnoreCollisionsWith(Transform enemyRoot, float seconds)
    {
        if (myColliders == null) return;
        var enemyCols = enemyRoot.GetComponentsInChildren<Collider2D>();
        foreach (var my in myColliders)
        {
            if (!my) continue;
            foreach (var ec in enemyCols)
            {
                if (!ec) continue;
                Physics2D.IgnoreCollision(my, ec, true);
                StartCoroutine(ReenableCollisionLater(my, ec, seconds));
            }
        }
    }

    private IEnumerator ReenableCollisionLater(Collider2D a, Collider2D b, float t)
    {
        yield return new WaitForSeconds(t);
        if (a && b) Physics2D.IgnoreCollision(a, b, false);
    }

    // 스텝별 가중치(필요시 커스터마이즈)
    private float DamageMulByStep(int step) => 1f + 0.1f * step;
    private float KnockbackMulByStep(int step) => 1f + 0.1f * step;
    private float RangeMulByStep(int step) => 1f;
    private float RadiusMulByStep(int step) => 1f;


}
