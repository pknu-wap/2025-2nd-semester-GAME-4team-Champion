using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    // ==== 공격 수치/설정 ====
    [Header("Combo (Config)")]
    [SerializeField] private int maxCombo = 4;
    [SerializeField] private float comboGapMax = 1f;   // 콤보 끊김 허용 간격
    [SerializeField] private bool lockMoveDuringAttack = true;
    
    [Header("Hitbox (Config)")]
    [SerializeField] private LayerMask enemyMask;        // 히트박스용 마스크
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float baseKnockback = 6f;
    [SerializeField] private float baseRange = 0.9f;
    [SerializeField] private float baseRadius = 0.6f;
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;

    [Header("Timings (sec)")]
    [SerializeField] private float windup = 0.20f;   // 선딜
    [SerializeField] private float active = 0.06f;   // 공격 활성
    [SerializeField] private float recovery = 0.12f; // 후딜
    [SerializeField] private float minRecovery = 0.15f;  // 최소 후딜 유지

    [Header("Charge Attack")]
    [SerializeField] private float chargeTime = 0.5f;
    [SerializeField] private float chargeDamageMul = 3.0f;
    [SerializeField] private float chargeKnockMul = 2.0f;
    [SerializeField] private float chargeRangeMul = 1.2f;
    [SerializeField] private float chargeRadiusMul = 1.2f;

    // ==== 상태 ====
    private PlayerMove inputWrapper;
    private InputAction attackAction;

    private int comboIndex = 0;
    private bool isAttacking = false;
    private bool nextBuffered = false;
    private float lastAttackEndTime = -999f;
    private bool chargeLocked = false;

    private bool attackHeld = false;
    private float attackPressedTime = -999f;
    private Coroutine chargeCo;
    private Coroutine attackCo;
    private Coroutine attackMoveLockCo;

    private Collider2D[] myColliders;

    public bool IsAttacking => isAttacking;

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

        attackPressedTime = Time.time;

        // Atk1 이전에만 Charging 허용
        if (chargeLocked || isAttacking) return;

        attackHeld = true;
        animator?.SetBool("Charging", true);
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
            if (Time.time - lastAttackEndTime > comboGapMax)
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
            if (held >= chargeTime)
            {
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

        if (step == 0) chargeLocked = true;

        animator?.SetTrigger($"Atk{Mathf.Clamp(step + 1, 1, maxCombo)}");

        // 이동락 + 미끄럼 방지(시작 즉시 속도 0)
        if (lockMoveDuringAttack)
        {
            float lockTime = windup + active + recovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime, zeroVelocity: true));
        }

        // 선딜
        yield return new WaitForSeconds(windup);

        // 히트박스
        DoHitbox(baseDamage * DamageMulByStep(step),
                 baseKnockback * KnockbackMulByStep(step),
                 baseRange * RangeMulByStep(step),
                 baseRadius * RadiusMulByStep(step));

        // 활성
        yield return new WaitForSeconds(active);

        // 후딜(최소 유지 + 캔슬 가능)
        float t = 0f;
        while (t < minRecovery) { t += Time.deltaTime; yield return null; }
        while (t < recovery)
        {
            if (nextBuffered && step < maxCombo - 1) break;
            t += Time.deltaTime;
            yield return null;
        }

        // 종료/연계
        isAttacking = false;
        lastAttackEndTime = Time.time;

        if (nextBuffered && step < maxCombo - 1)
        {
            comboIndex = step + 1;
            attackCo = StartCoroutine(DoAttackStep(comboIndex));
        }
        else
        {
            comboIndex = 0;
            nextBuffered = false;
            chargeLocked = false;
        }

        attackCo = null;
    }

    private IEnumerator DoChargeAttack()
    {
        isAttacking = true;

        if (animator)
        {
            animator.ResetTrigger("Atk1"); animator.ResetTrigger("Atk2");
            animator.ResetTrigger("Atk3"); animator.ResetTrigger("Atk4");
            animator.SetTrigger("AtkCharge");
        }

        if (lockMoveDuringAttack)
        {
            float lockTime = (windup + 0.07f) + active + recovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime, zeroVelocity: true));
        }

        // 선딜 + 약간의 추가 연출
        yield return new WaitForSeconds(windup + 0.07f);

        // 차지 히트박스
        DoHitbox(baseDamage * chargeDamageMul,
                 baseKnockback * chargeKnockMul,
                 baseRange * chargeRangeMul,
                 baseRadius * chargeRadiusMul);

        if (animator) animator.SetBool("Charging", false);

        // 활성 + 후딜
        yield return new WaitForSeconds(active + recovery);

        isAttacking = false;
        lastAttackEndTime = Time.time;
        comboIndex = 0;
        attackCo = null;
    }

    private IEnumerator LockMoveFor(float seconds, bool zeroVelocity = true)
    {
        if (moveRef) moveRef.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: zeroVelocity);
        yield return new WaitForSeconds(seconds);
        if (moveRef) moveRef.SetMovementLocked(false, hardFreezePhysics: false);
        attackMoveLockCo = null;
    }

    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        if (combat.DebugLogs)
            Debug.Log($"[HITBOX] dmg={dmg}, knock={knock}, center={center}, r={radius}");

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMask);
        var seen = new HashSet<Collider2D>();
        bool anyHit = false;

        foreach (var h in hits)
        {
            if (!h || seen.Contains(h)) continue;
            seen.Add(h);

            if (ignoreEnemyCollisionDuringActive)
                IgnoreCollisionsWith(h.transform.root, active + extraIgnoreTime);

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                dmgTarget.ApplyHit(dmg, knock, toEnemy, gameObject);
                anyHit = true;
            }
            else if (combat.DebugLogs)
            {
                Debug.Log($"[HIT] {h.name} take {dmg}, knock={knock}");
                anyHit = true;
            }
        }

        if (anyHit) combat.EnterCombat("HitEnemy");
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
