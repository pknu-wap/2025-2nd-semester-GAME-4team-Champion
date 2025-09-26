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
    [SerializeField] private float windup = 0.15f;   // 선딜
    [SerializeField] private float active = 0.06f;   // 공격 활성
    [SerializeField] private float recovery = 0.12f; // 후딜
    [SerializeField] private float minRecovery = 0.05f;  // 최소 후딜 유지

    [Header("Charge Attack")]
    [SerializeField] private string chargeActionName = "Charge"; // Input Actions 안 액션 이름
    [SerializeField] private float chargeTime = 0.5f;            // 풀차지까지 시간
    [SerializeField] private float minChargeHold = 0.12f;        // 이 미만으로 눌렀다 떼면 발동 안 함
    [SerializeField] private bool lockMoveDuringCharge = true;   // 차지 대기 동안 이동금지
    [SerializeField] private float chargeDamageMul = 5.0f;
    [SerializeField] private float chargeKnockMul = 2.0f;
    [SerializeField] private float chargeRangeMul = 1.2f;
    [SerializeField] private float chargeRadiusMul = 1.2f;

    [Header("Counter (Riposte)")]
    [SerializeField] private string counterTriggerName = "Counter";
    [SerializeField] private float counterWindup = 0.02f;
    [SerializeField] private float counterActive = 0.06f;
    [SerializeField] private float counterRecovery = 0.10f;
    [SerializeField] private float counterDamageMul = 2.0f;
    [SerializeField] private float counterKnockMul = 2.0f;
    [SerializeField] private float counterRangeMul = 1.1f;
    [SerializeField] private float counterRadiusMul = 1.0f;

    [Header("Debug Draw")]
    [SerializeField] private bool debugDrawHitbox = true;
    [SerializeField] private Color comboGizmoColor = Color.cyan;
    [SerializeField] private Color counterGizmoColor = Color.yellow;
    [SerializeField] private float gizmoDrawDuration = 0.15f;

    private bool counterArmed = false;
    private float counterExpireTime = -999f;

    // ==== 상태 ====
    private PlayerMove inputWrapper;
    private InputAction attackAction;
    private InputAction chargeAction;

    private int comboIndex = 0;
    private bool isAttacking = false;
    private bool nextBuffered = false;
    private float lastAttackEndTime = -999f;
    private bool chargeLocked = false;

    private bool attackHeld = false;
    private float attackPressedTime = -999f;
    private Coroutine chargeCo;           // (일반 차지 대기)  기존 콤보용은 사용 안 함
    private Coroutine attackCo;
    private Coroutine attackMoveLockCo;

    // === Charge 전용 상태 ===
    private Coroutine chargeWaitCo;       // 풀차지까지 기다리다 즉시 발동
    private bool isCharging = false;      // 차지 대기 중인지
    private float chargeStartTime = 0f;   // 누른 시각(길이 계산)

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

        // Charge 액션 구독
        chargeAction = inputWrapper.asset.FindAction("Charge");
        if (chargeAction != null)
        {
            chargeAction.started += OnChargeStarted;
            chargeAction.canceled += OnChargeCanceled;
        }
        else
        {
            Debug.LogWarning($"[PlayerAttack] '{chargeActionName}' 액션이 없습니다.");
        }
    }

    private void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
        }

        if (chargeAction != null)
        {
            chargeAction.started -= OnChargeStarted;
            chargeAction.canceled -= OnChargeCanceled;
        }

        inputWrapper.Disable();

        if (chargeCo != null) StopCoroutine(chargeCo);
        if (attackCo != null) StopCoroutine(attackCo);
        if (chargeWaitCo != null) StopCoroutine(chargeWaitCo);
    }

    // ==== 일반 공격 입력 ====
    private void OnAttackStarted(InputAction.CallbackContext _)
    {
        // 리포스트 우선
        if (counterArmed && Time.time <= counterExpireTime)
        {
            Debug.Log($"[COUNTER-TRY] armed={counterArmed}, now={Time.time:F2} <= exp={counterExpireTime:F2}, isAttacking={isAttacking}");
            Debug.Log("[RIPOSTE] Triggered by Attack input");
            if (!isAttacking)
                if (!isAttacking)
                attackCo = StartCoroutine(DoCounterAttack());
            return;
        }


        if (combat.IsStaminaBroken || combat.InHitstun || combat.IsParryLocked || isCharging) return;

        attackHeld = true;
        attackPressedTime = Time.time;

        // 일반 콤보  차지와 별개 키이므로 그대로 사용
        // (여기선 Charging bool 사용 안 함)
    }

    private void OnAttackCanceled(InputAction.CallbackContext _)
    {
        attackHeld = false;

        if (combat.IsParryLocked || isCharging) return;

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

    // ==== Charge 입력 ====
    private void OnChargeStarted(InputAction.CallbackContext _)
    {
        if (combat.IsStaminaBroken || combat.InHitstun || combat.IsParryLocked || isAttacking) return;

        isCharging = true;
        chargeStartTime = Time.time;

        animator?.SetBool("Charging", true);

        // 차지 동안 이동 금지 (대기 포함)
        if (lockMoveDuringCharge) moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: true);

        // 풀차지 도달을 감시 도달 즉시 자동 발동
        if (chargeWaitCo != null) StopCoroutine(chargeWaitCo);
        chargeWaitCo = StartCoroutine(ChargeWaitThenFire());
    }

    private void OnChargeCanceled(InputAction.CallbackContext _)
    {
        if (!isCharging) return;

        float held = Time.time - chargeStartTime;

        // 임계 이하로 짧게 눌렀으면 아무 것도 하지 않음(발동 X)
        // (풀차지 이전에 떼면 무조건 취소)
        if (animator) animator.SetBool("Charging", false);

        // 차지 대기 해제
        isCharging = false;
        if (chargeWaitCo != null) { StopCoroutine(chargeWaitCo); chargeWaitCo = null; }

        // 이동 잠금 해제 (공격이 실제로 시작되지 않은 경우)
        if (lockMoveDuringCharge) moveRef?.SetMovementLocked(false, hardFreezePhysics: false);

    }

    private IEnumerator ChargeWaitThenFire()
    {
        // 풀차지까지 대기  도달 "즉시" DoChargeAttack 실행
        while (isCharging)
        {
            if (Time.time - chargeStartTime >= chargeTime)
            {
                // 차지 대기는 끝, 실제 공격 코루틴 시작
                isCharging = false;

                // Charging Bool은 DoChargeAttack 내부에서 종료 타이밍에 맞춰 끕니다.
                // 이동락은 공격 전체가 끝날 때까지 유지되도록 여기서는 해제 안 함.
                attackCo = StartCoroutine(DoChargeAttack());
                break;
            }
            yield return null;
        }
        chargeWaitCo = null;
    }

    private IEnumerator DoCounterAttack()
    {
        isAttacking = true;
        counterArmed = false; // 창 소모

        // 애니가 없어도 트리거는 남겨둠(나중에 연결할 때 편함)
        if (animator) animator.SetTrigger(counterTriggerName);

        // 이동 금지  미끄럼 방지(즉시 속도 0)
        if (lockMoveDuringAttack)
        {
            float lockTime = counterWindup + counterActive + counterRecovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime, zeroVelocity: true));
        }

        // 짧은 선딜
        yield return new WaitForSeconds(counterWindup);


        DoHitbox(baseDamage * counterDamageMul,
                 baseKnockback * counterKnockMul,
                 baseRange * counterRangeMul,
                 baseRadius * counterRadiusMul);

        // 눈에 보이는 로그(데미지 확인용)
        Debug.Log($"[COUNTER] dmg≈{baseDamage * counterDamageMul}, r={baseRadius * counterRadiusMul}");

        // 전투 진입 보장
        combat.EnterCombat("Counter");

        // 활성 후딜
        yield return new WaitForSeconds(counterActive + counterRecovery);

        isAttacking = false;
        attackCo = null;
    }

    private IEnumerator DoAttackStep(int step)
    {
        isAttacking = true;
        nextBuffered = false;

        if (step == 0) chargeLocked = true;

        animator?.SetTrigger($"Atk{Mathf.Clamp(step + 1, 1, maxCombo)}");

        // 이동락 + 미끄럼 방지
        if (lockMoveDuringAttack)
        {
            float lockTime = windup + active + recovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime, true));
        }

        yield return new WaitForSeconds(windup);

        DoHitbox(baseDamage * DamageMulByStep(step),
                 baseKnockback * KnockbackMulByStep(step),
                 baseRange * RangeMulByStep(step),
                 baseRadius * RadiusMulByStep(step));

        yield return new WaitForSeconds(active);

        float t = 0f;
        while (t < minRecovery) { t += Time.deltaTime; yield return null; }
        while (t < recovery)
        {
            if (nextBuffered && step < maxCombo - 1) break;
            t += Time.deltaTime;
            yield return null;
        }

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

        // 이미 차지 대기에서 이동을 잠궜으니, 공격 중 락도 그대로 유지
        // (원하시면 아래 LockMoveFor로 '공격 구간’만 별도 락을 덮어씌워도 됩니다)
        if (lockMoveDuringAttack)
        {
            float lockTime = (windup + 0.07f) + active + recovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime, true));
        }

        // 선딜 + 약간의 추가 연출
        yield return new WaitForSeconds(windup + 0.07f);

        DoHitbox(baseDamage * chargeDamageMul,
                 baseKnockback * chargeKnockMul,
                 baseRange * chargeRangeMul,
                 baseRadius * chargeRadiusMul);

        if (animator) animator.SetBool("Charging", false);

        yield return new WaitForSeconds(active + recovery);

        isAttacking = false;
        lastAttackEndTime = Time.time;
        comboIndex = 0;
        attackCo = null;

        // 차지/공격 전체 종료 지점에서 이동 잠금 해제
        if (lockMoveDuringCharge) moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
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

    public void ArmCounter(float windowSeconds)
    {
        counterArmed = windowSeconds > 0f;
        counterExpireTime = Time.time + Mathf.Max(0f, windowSeconds);
        Debug.Log($"[COUNTER-ARM] window={windowSeconds:F2}s, until={counterExpireTime:F2}, now={Time.time:F2}");
    }

}
