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

    // ����
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
    // �浹 ���ÿ�
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
            Debug.LogWarning("[PlayerAttack] 'Attack' �׼��� �����ϴ�.");
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

        // �� �׻� ���� ������ ���� Ŭ���ð� ����
        attackPressedTime = Time.time;

        // �� Atk1 ����(�޺� ���� ��)�� ������ ����: Charging/�ڷ�ƾ ���۸� ���´�
        if (chargeLocked)
        {
            // �޺� �߿��� ���⼭ ��. (���۴� OnAttackCanceled���� nextBuffered�� ó��)
            return;
        }

        // ���� ��� ���������� Ȧ��/���� ����
        attackHeld = true;
        animator?.SetBool("Charging", true);
        // (Charging ǥ�ø� ���� �ִٸ�) animator?.SetBool("Charging", true);
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


        animator?.SetTrigger($"Atk{Mathf.Clamp(step + 1, 1, combat.MaxCombo)}");
        if (combat.LockMoveDuringAttack)
        {
            float lockTime = combat.Windup + combat.Active + combat.Recovery; // �� �ν����ͷ� ����
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime));
        }

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

        if (combat.LockMoveDuringAttack)
        {
            float lockTime = (combat.Windup + 0.07f) + combat.Active + combat.Recovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime));
        }

        yield return new WaitForSeconds(combat.Windup + 0.07f);
        DoHitbox(combat.BaseDamage * combat.ChargeDamageMul,
                 combat.BaseKnockback * combat.ChargeKnockMul,
                 combat.BaseRange * combat.ChargeRangeMul,
                 combat.BaseRadius * combat.ChargeRadiusMul);
        if (animator) animator.SetBool("Charging", false);
        yield return new WaitForSeconds(combat.Active + combat.Recovery);
        
        if (combat.LockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(false, false);

        isAttacking = false;
        lastAttackEndTime = Time.time;
        comboIndex = 0;
        attackCo = null;
    }

    private IEnumerator LockMoveFor(float seconds)
    {
        if (moveRef) moveRef.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);
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
                anyHit = true; // �� ���� �÷���
            }
            else if (combat.DebugLogs)
            {
                Debug.Log($"[HIT] {h.name} take {dmg}, knock={knock}");
                anyHit = true; // �� ��Ʈ�� ����(�׽�Ʈ��)
            }
        }

        if (anyHit) combat.EnterCombat("HitEnemy"); // �� ù ��Ʈ �� ���� ����
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

    // ���ܺ� ����ġ(�ʿ�� Ŀ���͸�����)
    private float DamageMulByStep(int step) => 1f + 0.1f * step;
    private float KnockbackMulByStep(int step) => 1f + 0.1f * step;
    private float RangeMulByStep(int step) => 1f;
    private float RadiusMulByStep(int step) => 1f;


}
