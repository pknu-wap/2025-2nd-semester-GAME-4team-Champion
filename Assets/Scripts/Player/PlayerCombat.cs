using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerDefense defense;
    [SerializeField] private PlayerAttack attack;

    // --------- Vitals (ü��/���¹̳�) ---------
    [Header("Vitals")]
    [SerializeField] private float hpMax = 100f;
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float staminaRegenPerSec = 25f;

    public float Hp01 => Mathf.Clamp01(hp / Mathf.Max(1f, hpMax));
    public float Stamina01 => Mathf.Clamp01(stamina / Mathf.Max(1f, staminaMax));
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;

    private float hp;
    private float stamina;

    // === Combat State ===
    [Header("Facing (Auto)")]
    [SerializeField] private bool autoFaceOnCombatEnter = true;
    [SerializeField] private float autoFaceSearchRadius = 15f;

    [Header("Combat State")]
    [SerializeField] private float disengageDistance = 150f;
    [SerializeField] private float enemyScanInterval = 0.25f;
    [SerializeField, Range(0f, 1f)] private float combatYSpeedMultiplier = 0.3f;
    [SerializeField] private bool autoFaceDuringCombat = true;
    [SerializeField] private bool autoFaceEvenWhileAttacking = false;
    [SerializeField] private float autoFaceInputDeadzoneX = 0.2f;
    [SerializeField] private LayerMask enemyMask;

    private bool inCombat = false;
    private Coroutine combatMonitorCo;

    public bool IsInCombat => inCombat;
    public float CombatYSpeedMul => combatYSpeedMultiplier;

    // --------- Hit Reaction ---------
    [Header("Hit Reaction")]
    [SerializeField] private float baseHitstun = 0.20f;
    [SerializeField] private float blockHitstunMul = 0.5f;

    private bool inHitstun = false;
    private float hitstunEndTime = 0f;
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Collision (optional)")]
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;

    // === �ִϸ��̼� ������ ===
    [Header("Animation Variants")]
    [SerializeField] private int hitVariants = 3;      // Hit1..HitN
    [SerializeField] private int weavingVariants = 3;  // Weaving1..WeavingN

    // === ���� Getter (���� ������Ʈ�� ����) ===
    public bool DebugLogs => debugLogs;
    public bool InHitstun => inHitstun;
    public PlayerMoveBehaviour MoveRef => moveRef;
    public Animator Anim => animator;
    public Rigidbody2D RB => rb;

    // Defense ����/��ġ ������
    public bool IsParryLocked => defense != null && defense.IsParryLocked;
    public bool IsStaminaBroken => defense != null && defense.IsStaminaBroken;

    public LayerMask EnemyMask => enemyMask;

    public bool IgnoreEnemyCollisionDuringActive => ignoreEnemyCollisionDuringActive;
    public float ExtraIgnoreTime => extraIgnoreTime;

    private void Reset()
    {
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!attack) attack = GetComponent<PlayerAttack>();
    }

    private void Awake()
    {
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!attack) attack = GetComponent<PlayerAttack>();

        hp = hpMax;
        stamina = staminaMax;
        OnHealthChanged?.Invoke(hp, hpMax);
        OnStaminaChanged?.Invoke(stamina, staminaMax);

        if (defense) defense.Bind(this, moveRef, animator);
        if (attack) attack.Bind(this, moveRef, animator);
    }

    private void OnEnable() => StartCoroutine(VitalsTick());
    private void OnDisable() => StopAllCoroutines();

    public void EnterCombat(string reason = null)
    {
        if (inCombat) return;
        inCombat = true;
        animator?.SetBool("InCombat", true);

        moveRef?.SetFlipFromMovementBlocked(true);

        if (autoFaceOnCombatEnter) TryAutoFaceNearestEnemyX();
        if (combatMonitorCo != null) StopCoroutine(combatMonitorCo);
        combatMonitorCo = StartCoroutine(CombatMonitor());
    }

    public void ExitCombat()
    {
        if (!inCombat) return;
        inCombat = false;
        animator?.SetBool("InCombat", false);

        moveRef?.SetFlipFromMovementBlocked(false);

        if (combatMonitorCo != null) { StopCoroutine(combatMonitorCo); combatMonitorCo = null; }
    }

    private void TryAutoFaceNearestEnemyX()
    {
        float radius = (autoFaceSearchRadius > 0f) ? autoFaceSearchRadius : disengageDistance;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius, enemyMask);
        if (hits == null || hits.Length == 0) return;

        Transform nearest = null;
        float bestSqr = float.PositiveInfinity;
        Vector2 myPos = transform.position;

        foreach (var h in hits)
        {
            if (!h) continue;
            float d2 = ((Vector2)h.transform.position - myPos).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; nearest = h.transform; }
        }

        if (nearest && moveRef) moveRef.FaceTargetX(nearest.position.x);
    }

    public void EnterCombatByInteraction() => EnterCombat("Interaction");

    public void NotifyEnemyKilled(Transform enemy = null)
    {
        if (!inCombat) return;
        if (!HasEnemyWithin(disengageDistance)) ExitCombat();
    }

    private bool HasEnemyWithin(float dist)
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, dist, enemyMask);
        return hits != null && hits.Length > 0;
    }

    private IEnumerator CombatMonitor()
    {
        var wait = new WaitForSeconds(enemyScanInterval);
        while (inCombat)
        {
            if (autoFaceDuringCombat) AutoFaceTick();

            if (!HasEnemyWithin(disengageDistance))
            {
                ExitCombat();
                yield break;
            }
            yield return wait;
        }
    }

    private void AutoFaceTick()
    {
        if (!moveRef) return;
        if (Mathf.Abs(moveRef.CurrentInput.x) >= autoFaceInputDeadzoneX) return;
        if (!autoFaceEvenWhileAttacking && attack != null && attack.IsAttacking) return;
        TryAutoFaceNearestEnemyX();
    }

    private IEnumerator VitalsTick()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            // ���¹̳� ȸ��(�񰡵�, ��극��ũ)
            if (defense && !defense.IsStaminaBroken && !defense.IsBlocking && stamina < staminaMax)
            {
                stamina = Mathf.Min(staminaMax, stamina + staminaRegenPerSec * Time.deltaTime);
                OnStaminaChanged?.Invoke(stamina, staminaMax);
            }
            yield return wait;
        }
    }

    // === �ܺ�(�� ��Ʈ�ڽ�)���� ȣ�� ===
    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable, GameObject attacker = null, float hitstun = -1f)
    {
        if (Time.time < iFrameEndTime) return; // i-������ �� ����

        EnterCombat("GotHit");

        // ���� �غ�
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 inFrontDir = -hitDir.normalized; // �÷��̾����

        // ��� ����(���� ��/�и� ������ ��� ����)
        var outcome = defense ? defense.Evaluate(facing, inFrontDir, parryable) : DefenseOutcome.None;

        // --- Weaving(�и�) ���� ---
        if (outcome == DefenseOutcome.Parry)
        {
            if (debugLogs) Debug.Log($"[WEAVING OK] t={Time.time:F2}s, attacker={(attacker ? attacker.name : "null")}");
            PlayRandomWeaving();

            var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
            parryableTarget?.OnParried(transform.position);

            // �и� ������ ���� ���� + ����Ʈ Ȧ����� ���� ��� & ���� ����
            float windowEnd = (defense ? defense.LastBlockPressedTime : Time.time) + defense.ParryWindow;
            float lockDuration = Mathf.Max(0f, (windowEnd + defense.WeavingPostHold) - Time.time);

            defense.StartParryLock(lockDuration, true); // �̵� ��(�ӵ� 0)
            defense.ForceBlockFor(lockDuration);        // ���� ����

            iFrameEndTime = Time.time + 0.05f; // ª�� i-frame(����)
            return;
        }
        // --- �Ϲ� ���� ���� ---
        else if (outcome == DefenseOutcome.Block)
        {
            float finalDamage = damage * defense.BlockDamageMul;
            float finalKnock = knockback * defense.BlockKnockMul;

            ApplyDamage(finalDamage);
            ApplyKnockbackXOnly(inFrontDir, finalKnock);

            stamina -= damage; // �ʿ� �� ���ȭ ����
            OnStaminaChanged?.Invoke(stamina, staminaMax);

            if (stamina <= 0f) defense.TriggerStaminaBreak();
            else
            {
                float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                StartHitstun(stun, playHitAnim: false); // ���� �� Hit �ִ� ����
            }

            animator?.SetTrigger("BlockHit");
            return;
        }

        // --- ���� ���� / ���Ĺ� ---
        ApplyDamage(damage);
        ApplyKnockbackXOnly(inFrontDir, knockback);

        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw); // �⺻ Hit �ִ� ���
    }

    // === ���� ��ƿ ===
    public void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Max(0f, hp - amount);
        OnHealthChanged?.Invoke(hp, hpMax);
        if (debugLogs) Debug.Log($"[HP] -{amount} (now {hp}/{hpMax})");
        if (hp <= 0f) OnDeath();
    }

    private void OnDeath()
    {
        if (debugLogs) Debug.Log("[Player] DEAD");
        moveRef?.SetMovementLocked(true, true);
        animator?.SetTrigger("Die");
    }

    public void ApplyKnockbackXOnly(Vector2 dirToEnemy, float force)
    {
        if (!rb || force <= 0f) return;
        float x = -Mathf.Sign(dirToEnemy.x);
        if (Mathf.Abs(x) < 0.0001f)
            x = (moveRef && Mathf.Abs(moveRef.LastFacing.x) > 0.0001f) ? Mathf.Sign(moveRef.LastFacing.x) : 1f;

        rb.linearVelocity = new Vector2(x * force, 0f);
    }

    public void StartHitstun(float duration, bool playHitAnim = true)
    {
        if (duration <= 0f) return;

        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);
        if (hitstunCo == null) hitstunCo = StartCoroutine(HitstunRoutine());

        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);

        if (playHitAnim) PlayRandomHit();
    }

    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        while (Time.time < hitstunEndTime) yield return null;
        inHitstun = false;
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        hitstunCo = null;
    }

    private void PlayRandomHit()
    {
        if (!animator) return;
        int idx = Mathf.Clamp(Random.Range(1, hitVariants + 1), 1, hitVariants);
        animator.SetTrigger($"Hit{idx}");
    }

    // ��û��� �� �Լ��� Combat�� ����
    private void PlayRandomWeaving()
    {
        if (!animator) return;
        int idx = Mathf.Clamp(Random.Range(1, weavingVariants + 1), 1, weavingVariants);
        animator.SetTrigger($"Weaving{idx}");
    }
}

// ����: ���� �и��Ǿ��� �� ����
public interface IParryable { void OnParried(Vector3 parrySourcePosition); }

// �÷��̾� ������ ������ �����ϴ� �������̽�
public interface IDamageable
{
    void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker);
}
