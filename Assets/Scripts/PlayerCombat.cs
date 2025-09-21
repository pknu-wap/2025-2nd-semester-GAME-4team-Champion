using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour moveRef; // ���� ������Ʈ�� �ڵ� �Ҵ� ����
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    // --------- Vitals (ü�� / ���¹̳�) ---------
    [Header("Vitals")]
    [SerializeField] private float hpMax = 100f;
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float staminaRegenPerSec = 25f; // ���� �� �ʴ� ȸ��
    [SerializeField] private float staminaBreakTime = 1.5f;  // �극��ũ(����) ����

    public float Hp01 => Mathf.Clamp01(hp / Mathf.Max(1f, hpMax));
    public float Stamina01 => Mathf.Clamp01(stamina / Mathf.Max(1f, staminaMax));

    public event Action<float, float> OnHealthChanged;   // (current, max)
    public event Action<float, float> OnStaminaChanged;  // (current, max)

    private float hp;
    private float stamina;

    private bool staminaBroken;
    private float staminaBreakEndTime;

    // --------- Guard / Parry (���� ���� ����) ---------
    [Header("Guard / Parry")]
    [SerializeField] private float guardAngle = 120f;     // ���� �� ����
    [SerializeField] private float parryWindow = 0.3f;   // ���� ���� �и� ������
    [SerializeField] private float blockDamageMul = 0f;
    [SerializeField] private float blockKnockMul = 0.3f;  // ���� �˹� ���

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool isBlocking;
    private float blockPressedTime = -999f;

    // --------- Attack (�޺�/����) ---------
    [Header("Attack - Combo")]
    [SerializeField] private int maxCombo = 5;
    [SerializeField] private float comboGapMax = 0.6f;
    [SerializeField] private float bufferWindow = 0.35f;
    [SerializeField] private bool lockMoveDuringAttack = true;

    [Header("Attack - Hitbox")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float baseKnockback = 6f;
    [SerializeField] private float baseRange = 0.9f;
    [SerializeField] private float baseRadius = 0.6f;

    [Header("Combo Timings (sec)")]
    [SerializeField] private float windup = 0.08f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.12f;

    [Header("Charge Attack")]
    [SerializeField] private float chargeTime = 0.5f;
    [SerializeField] private float chargeDamageMul = 3.0f;
    [SerializeField] private float chargeKnockMul = 2.0f;
    [SerializeField] private float chargeRangeMul = 1.2f;
    [SerializeField] private float chargeRadiusMul = 1.2f;

    private int comboIndex = 0;
    private bool isAttacking = false;
    private bool nextBuffered = false;
    private float lastAttackEndTime = -999f;

    private bool attackHeld = false;
    private float attackPressedTime = -999f;
    private Coroutine chargeCo;

    // === Hit Reaction ===
    [Header("Hit Reaction")]
    [SerializeField] private float baseHitstun = 0.20f; // �Ϲ� �ǰ� ���� �ð�
    [SerializeField] private float blockHitstunMul = 0.5f; // ����� ���� ���� ���
    private float hitstunEndTime = 0f;

    private bool inHitstun = false; 
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;
    private Coroutine attackCo; // ���� �ڷ�ƾ Ʈ��ŷ(���� �� ��ҿ�)


    // --------- Collision (����: ���� Ȱ�� �� �浹 ����) ---------
    [Header("Collision (optional)")]
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;
    private Collider2D[] myColliders;

    // --------- Input ---------
    private PlayerMove inputWrapper; // .inputactions ���� ����
    private InputAction blockAction; // "Block"
    private InputAction attackAction; // "Attack"

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();

        // ü��/���¹̳� �ʱ�ȭ
        hp = hpMax;
        stamina = staminaMax;
        OnHealthChanged?.Invoke(hp, hpMax);
        OnStaminaChanged?.Invoke(stamina, staminaMax);

        inputWrapper = new PlayerMove();
        myColliders = GetComponents<Collider2D>();
    }

    private void OnEnable()
    {
        inputWrapper.Enable();

        // Block
        blockAction = inputWrapper.asset.FindAction("Block");
        if (blockAction != null)
        {
            blockAction.started += OnBlockStarted;
            blockAction.canceled += OnBlockCanceled;
        }
        else
        {
            Debug.LogWarning("[PlayerCombat] 'Block' �׼��� �����ϴ�. .inputactions�� �߰��ϼ���.");
        }

        // Attack
        attackAction = inputWrapper.asset.FindAction("Attack");
        if (attackAction != null)
        {
            attackAction.started += OnAttackStarted;
            attackAction.canceled += OnAttackCanceled;
        }
        else
        {
            Debug.LogWarning("[PlayerCombat] 'Attack' �׼��� �����ϴ�. .inputactions�� �߰��ϼ���.");
        }

        StartCoroutine(VitalsTick());
    }

    private void OnDisable()
    {
        if (blockAction != null)
        {
            blockAction.started -= OnBlockStarted;
            blockAction.canceled -= OnBlockCanceled;
        }
        if (attackAction != null)
        {
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
        }
        inputWrapper.Disable();
        StopAllCoroutines();
    }

    // ====== Block / Parry ======
    private void OnBlockStarted(InputAction.CallbackContext ctx)
    {
        // ���¹̳� �극��ũ ���� ����, ��Ʈ���� �߿��� ����� �����ϰ� ��
        if (staminaBroken) return;

        isBlocking = true;
        blockPressedTime = Time.time;
        if (animator) animator.SetBool("isBlocking", true);
    }


    private void OnBlockCanceled(InputAction.CallbackContext ctx)
    {
        isBlocking = false;
        if (animator) animator.SetBool("isBlocking", false);
    }

    // ====== ü��/���¹̳� ƽ ======
    private IEnumerator VitalsTick()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            // ���¹̳� �극��ũ ����
            if (staminaBroken && Time.time >= staminaBreakEndTime)
            {
                staminaBroken = false;

                // �̵� ��� ����
                if (moveRef) moveRef.SetMovementLocked(false, true);

                if (animator) animator.SetBool("GuardBroken", false); // ���� �Ķ���� �̸� ����
            }

            // ���� �� ���¹̳� ȸ��
            if (!isBlocking && !staminaBroken && stamina < staminaMax)
            {
                stamina = Mathf.Min(staminaMax, stamina + staminaRegenPerSec * Time.deltaTime);
                OnStaminaChanged?.Invoke(stamina, staminaMax);
            }

            yield return wait;
        }
    }

    private void StaminaBreak()
    {
        staminaBroken = true;
        isBlocking = false;
        staminaBreakEndTime = Time.time + staminaBreakTime;

        if (moveRef) moveRef.SetMovementLocked(true, true);

        if (animator)
        {
            animator.SetBool("GuardBroken", true);   // �ִ� �Ķ���� �̸��� �ٲٰ� �ʹٸ� Animator������ ��ü
            animator.SetTrigger("GuardBreak");
        }
    }

    // hitDir: "�� �� �÷��̾�" ����, hitstun: ���� �ִ� ����(��). ������ �⺻�� ���.
    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable, GameObject attacker = null, float hitstun = -1f)
    {
        // i-������ ������ ����
        if (Time.time < iFrameEndTime) return;

        // ���� �� ����
        Vector2 facing = (moveRef != null && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 inFrontDir = -hitDir.normalized; // �����÷��̾��� �ݴ� = �÷��̾����
        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing, inFrontDir) >= cosHalf;

        // ���� ���� �б�
        if (!staminaBroken && isBlocking && inFront)
        {
            bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;

            if (canParry)
            {
                if (debugLogs)
                    Debug.Log($"[PARRY OK] t={Time.time:F2}s, dt={(Time.time - blockPressedTime):F3}s, attacker={(attacker ? attacker.name : "null")}");

                if (animator) animator.SetTrigger("Parry");

                var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
                if (parryableTarget != null)
                    parryableTarget.OnParried(transform.position);

                // (����) �и� ���� ª�� i-�������� �ְ� ������ ����μ���.
                iFrameEndTime = Time.time + 0.05f;
                return;
            }
            else
            {
                // �Ϲ� ����: Ĩ ������ + �˹� ���� + ���¹̳� �Ҹ�
                float finalDamage = damage * blockDamageMul;
                float finalKnock = knockback * blockKnockMul;

                ApplyDamage(finalDamage);
                ApplyKnockback(inFrontDir, finalKnock);

                stamina -= damage; // Ʃ�� ����(��� �� ��)
                OnStaminaChanged?.Invoke(stamina, staminaMax);

                if (stamina <= 0f)
                {
                    StaminaBreak();
                }
                else
                {
                    float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                    StartHitstun(stun);
                }

                if (animator) animator.SetTrigger("BlockHit");
                return;
            }
        }

        // ���� ����/���Ĺ�/�극��ũ/����
        ApplyDamage(damage);
        ApplyKnockback(inFrontDir, knockback);
        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw);
    }


    private void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Max(0f, hp - amount);
        OnHealthChanged?.Invoke(hp, hpMax);

        if (debugLogs)
            Debug.Log($"[HP] -{amount} (now {hp}/{hpMax})");

        if (hp <= 0f)
            OnDeath();
    }

    private void OnDeath()
    {
        // TODO: ��� ó��(������, ���ӿ��� UI ��)
        if (debugLogs) Debug.Log("[Player] DEAD");
        // ����: ���� ��� + �ִ� Ʈ����
        if (moveRef) moveRef.SetMovementLocked(true, true);
        if (animator) animator.SetTrigger("Die");
        // Destroy(gameObject); // ���ϸ� �ı�
    }

    // PlayerCombat.cs
    private void ApplyKnockback(Vector2 dirToEnemy, float force)
    {
        if (force <= 0f || rb == null) return;
        float x = -Mathf.Sign(dirToEnemy.x);

        // ���� ���� �������� �¾� X�� 0�� �����ٸ�, ������ �ٶ󺸴� �������� �б�
        if (Mathf.Abs(x) < 0.0001f)
            x = (moveRef && Mathf.Abs(moveRef.LastFacing.x) > 0.0001f) ? Mathf.Sign(moveRef.LastFacing.x) : 1f;

        rb.linearVelocity = new Vector2(x * force, 0f);
    }



    // ====== Attack �Է� ======
    private void OnAttackStarted(InputAction.CallbackContext ctx)
    {
        // ��Ʈ����/���¹̳� �극��ũ �߿��� ���� ���� �Ұ�
        if (staminaBroken || inHitstun) return;

        attackHeld = true;
        attackPressedTime = Time.time;

        if (chargeCo != null) StopCoroutine(chargeCo);
        chargeCo = StartCoroutine(CheckChargeReady());
    }


    private void OnAttackCanceled(InputAction.CallbackContext ctx)
    {
        attackHeld = false;
        if (chargeCo != null) { StopCoroutine(chargeCo); chargeCo = null; }
        if (animator) animator.SetBool("Charging", false);

        if (staminaBroken) return;

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
                if (animator) animator.SetBool("Charging", true);
                if (!isAttacking)
                    attackCo = StartCoroutine(DoChargeAttack());

                yield break;
            }
            yield return null;
        }
    }

    // ====== Attack ���� ======
    private IEnumerator DoAttackStep(int step)
    {
        isAttacking = true;
        nextBuffered = false;

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(true, false);
        if (animator) animator.SetTrigger($"Atk{Mathf.Clamp(step + 1, 1, maxCombo)}");

        // ����
        yield return new WaitForSeconds(windup);

        // Ȱ��: ��Ʈ�ڽ�
        DoHitbox(baseDamage * DamageMulByStep(step),
                 baseKnockback * KnockbackMulByStep(step),
                 baseRange * RangeMulByStep(step),
                 baseRadius * RadiusMulByStep(step));

        yield return new WaitForSeconds(active);

        // �ĵ�
        yield return new WaitForSeconds(recovery);

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(false, false);

        isAttacking = false;
        lastAttackEndTime = Time.time;

        if (nextBuffered && step < maxCombo - 1 && (Time.time - attackPressedTime) <= (active + recovery + bufferWindow + 0.2f))
        {
            comboIndex = step + 1;
            StartCoroutine(DoAttackStep(comboIndex));
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

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(true, false);
        if (animator)
        {
            animator.ResetTrigger("Atk1"); animator.ResetTrigger("Atk2");
            animator.ResetTrigger("Atk3"); animator.ResetTrigger("Atk4");
            animator.ResetTrigger("Atk5");
            animator.SetBool("Charging", false);
            animator.SetTrigger("AtkCharge");
        }

        yield return new WaitForSeconds(windup + 0.07f);

        DoHitbox(baseDamage * chargeDamageMul,
                 baseKnockback * chargeKnockMul,
                 baseRange * chargeRangeMul,
                 baseRadius * chargeRadiusMul);

        yield return new WaitForSeconds(active + recovery);

        if (lockMoveDuringAttack && moveRef) moveRef.SetMovementLocked(false, false);

        isAttacking = false;
        lastAttackEndTime = Time.time;
        comboIndex = 0;
        attackCo = null;
    }

    private void CancelOffense()
    {
        // ����/���� ���̸� �ߴ�
        if (attackCo != null) { StopCoroutine(attackCo); attackCo = null; }
        if (chargeCo != null) { StopCoroutine(chargeCo); chargeCo = null; }
        isAttacking = false;
        nextBuffered = false;
        comboIndex = 0;

        // ���� �ִ� ���� ����
        if (animator)
        {
            animator.SetBool("Charging", false);
            // �ʿ��ϸ� ���� Ʈ���� ���� �߰� ����
            // animator.ResetTrigger("Atk1"); ... ��
        }

        // �̵� ��� ����(���� �� �ᰬ�� ���)
        if (moveRef) moveRef.SetMovementLocked(false, false);
    }

    private void StartHitstun(float duration)
    {
        if (duration <= 0f) return;

        CancelOffense(); // ����/���� �ߴ�

        // ���� ���� �ð��� ���� ����(��ø ��Ʈ ����)
        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);

        if (hitstunCo == null)
            hitstunCo = StartCoroutine(HitstunRoutine());

        // �� �̵��� ���� ������ �츮��: hardFreezePhysics=false, zeroVelocity=false
        if (moveRef) moveRef.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);

        if (animator) animator.SetTrigger("Hit");
    }


    // HitstunRoutine ���� �κ�
    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        // hitstunEndTime ���� ���(���� �� �¾Ƶ� �� �ڷ�ƾ�� ����)
        while (Time.time < hitstunEndTime)
            yield return null;

        inHitstun = false;
        if (moveRef) moveRef.SetMovementLocked(false, hardFreezePhysics: false); // ����
        hitstunCo = null;
    }




    // ====== ��Ʈ�ڽ� & �浹 ���� ======
    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        Vector2 facing = (moveRef != null && moveRef.LastFacing.sqrMagnitude > 0f)
            ? moveRef.LastFacing
            : Vector2.right;

        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        if (debugLogs)
            Debug.Log($"[HITBOX] dmg={dmg}, knock={knock}, center={center}, r={radius}");

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMask);
        var hitSet = new HashSet<Collider2D>();

        foreach (var h in hits)
        {
            if (h == null || hitSet.Contains(h)) continue;
            hitSet.Add(h);

            if (ignoreEnemyCollisionDuringActive)
                IgnoreCollisionsWith(h.transform.root, active + extraIgnoreTime);

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;

            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                dmgTarget.ApplyHit(dmg, knock, toEnemy, gameObject);
            }
            else if (debugLogs)
            {
                Debug.Log($"[HIT] {h.name} take {dmg}, knock={knock}");
            }
        }
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

    // ====== ���ܺ� ����ġ ======
    private float DamageMulByStep(int step) => 1f + 0.1f * step;
    private float KnockbackMulByStep(int step) => 1f + 0.1f * step;
    private float RangeMulByStep(int step) => 1f;
    private float RadiusMulByStep(int step) => 1f;
}

// ����: �� �и� ����
public interface IParryable
{
    void OnParried(Vector3 parrySourcePosition);
}

// ������ �������̽�(�� � ����)
public interface IDamageable
{
    void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker);
}
