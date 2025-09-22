using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerDefense defense;
    [SerializeField] private PlayerAttack attack;

    // --------- Vitals (체력/스태미나) ---------
    [Header("Vitals")]
    [SerializeField] private float hpMax = 100f;
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float staminaRegenPerSec = 25f;
    [SerializeField] private float staminaBreakTime = 1.5f;

    public float Hp01 => Mathf.Clamp01(hp / Mathf.Max(1f, hpMax));
    public float Stamina01 => Mathf.Clamp01(stamina / Mathf.Max(1f, staminaMax));
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;

    private float hp;
    private float stamina;
    private bool staminaBroken;
    private float staminaBreakEndTime;

    // === Combat State ===
    [Header("Combat State")]
    [SerializeField] private float disengageDistance = 150f;     // 비전투 해제 거리
    [SerializeField] private float enemyScanInterval = 0.25f;    // 거리 체크 간격
    [SerializeField, Range(0f, 1f)] private float combatYSpeedMultiplier = 0.7f; // 전투 중 Y축 속도 배수

    private bool inCombat = false;
    private Coroutine combatMonitorCo;

    public bool IsInCombat => inCombat;
    public float CombatYSpeedMul => combatYSpeedMultiplier;


    // --------- Guard / Parry (수치조정 가능) ---------
    [Header("Guard / Parry (Config)")]
    [SerializeField] private float guardAngle = 120f;
    [SerializeField] private float parryWindow = 0.3f;
    [SerializeField] private float blockDamageMul = 0f;
    [SerializeField] private float blockKnockMul = 0.3f;

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

    private float parryLockEndTime = 0f;
    private Coroutine parryLockCo;
    public bool IsParryLocked => Time.time < parryLockEndTime;

    // --------- Attack (모든 수치/설정은 여기, 로직은 PlayerAttack) ---------
    [Header("Attack - Combo (Config)")]
    [SerializeField] private int maxCombo = 5;
    [SerializeField] private float comboGapMax = 0.6f;
    [SerializeField] private float bufferWindow = 0.35f;
    [SerializeField] private bool lockMoveDuringAttack = true;

    [Header("Attack - Hitbox (Config)")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float baseKnockback = 6f;
    [SerializeField] private float baseRange = 0.9f;
    [SerializeField] private float baseRadius = 0.6f;

    [Header("Attack - Timings (sec)")]
    [SerializeField] private float windup = 0.08f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.12f;

    [Header("Charge Attack (Config)")]
    [SerializeField] private float chargeTime = 0.5f;
    [SerializeField] private float chargeDamageMul = 3.0f;
    [SerializeField] private float chargeKnockMul = 2.0f;
    [SerializeField] private float chargeRangeMul = 1.2f;
    [SerializeField] private float chargeRadiusMul = 1.2f;

    [Header("Collision (optional)")]
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;

    // === 공개 Getter (서브 컴포넌트가 읽음) ===
    public bool DebugLogs => debugLogs;
    public bool IsStaminaBroken => staminaBroken;
    public bool InHitstun => inHitstun;
    public PlayerMoveBehaviour MoveRef => moveRef;
    public Animator Anim => animator;
    public Rigidbody2D RB => rb;

    // Guard/Parry
    public float GuardAngle => guardAngle;
    public float ParryWindow => parryWindow;
    public float BlockDamageMul => blockDamageMul;
    public float BlockKnockMul => blockKnockMul;
    public float BaseHitstun => baseHitstun;
    public float BlockHitstunMul => blockHitstunMul;

    // Attack Config
    public int MaxCombo => maxCombo;
    public float ComboGapMax => comboGapMax;
    public float BufferWindow => bufferWindow;
    public bool LockMoveDuringAttack => lockMoveDuringAttack;
    public LayerMask EnemyMask => enemyMask;
    public float BaseDamage => baseDamage;
    public float BaseKnockback => baseKnockback;
    public float BaseRange => baseRange;
    public float BaseRadius => baseRadius;
    public float Windup => windup;
    public float Active => active;
    public float Recovery => recovery;
    public float ChargeTime => chargeTime;
    public float ChargeDamageMul => chargeDamageMul;
    public float ChargeKnockMul => chargeKnockMul;
    public float ChargeRangeMul => chargeRangeMul;
    public float ChargeRadiusMul => chargeRadiusMul;
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

        // 양방향 참조 설정(선택)
        if (defense) defense.Bind(this, moveRef, animator);
        if (attack) attack.Bind(this, moveRef, animator);
    }

    private void OnEnable()
    {
        StartCoroutine(VitalsTick());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void EnterCombat(string reason = null)
    {
        if (inCombat) return;
        inCombat = true;
        if (animator) animator.SetBool("InCombat", true);

        // 모니터 시작
        if (combatMonitorCo != null) StopCoroutine(combatMonitorCo);
        combatMonitorCo = StartCoroutine(CombatMonitor());
    }

    public void EnterCombatByInteraction() => EnterCombat("Interaction");

    public void ExitCombat()
    {
        if (!inCombat) return;
        inCombat = false;
        if (animator) animator.SetBool("InCombat", false);

        if (combatMonitorCo != null) { StopCoroutine(combatMonitorCo); combatMonitorCo = null; }
    }

    // 적 사망 알림(선택적으로 적에서 호출 가능)
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
        // 전투 중 주기적으로 거리 체크 → 적이 멀어지면 비전투
        var wait = new WaitForSeconds(enemyScanInterval);
        while (inCombat)
        {
            if (!HasEnemyWithin(disengageDistance))
            {
                ExitCombat();
                yield break;
            }
            yield return wait;
        }
    }


    private IEnumerator VitalsTick()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            if (staminaBroken && Time.time >= staminaBreakEndTime)
            {
                staminaBroken = false;
                moveRef?.SetMovementLocked(false, true);
                animator?.SetBool("GuardBroken", false);
            }

            if (!staminaBroken && defense && !defense.IsBlocking && stamina < staminaMax)
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
        staminaBreakEndTime = Time.time + staminaBreakTime;
        // 가드 상태 종료 통지
        defense?.ForceUnblock();

        moveRef?.SetMovementLocked(true, true);
        if (animator)
        {
            animator.SetBool("GuardBroken", true);
            animator.SetTrigger("GuardBreak");
        }
    }

    // === 외부(적 히트박스)에서 호출 ===
    // hitDir: "적 → 플레이어" 방향, hitstun: 적이 주는 경직(초). <0이면 기본값 사용
    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable, GameObject attacker = null, float hitstun = -1f)
    {

        // i-프레임 중이면 무시
        if (Time.time < iFrameEndTime) return;
        EnterCombat("GotHit");

        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 inFrontDir = -hitDir.normalized;  // 플레이어→적
        bool inFront = Vector2.Dot(facing, inFrontDir) >= Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);

        // 방어 시스템에 판정 위임
        var outcome = defense ? defense.Evaluate(inFront, parryable) : DefenseOutcome.None;

        if (outcome == DefenseOutcome.Parry)
        {
            if (debugLogs) Debug.Log($"[PARRY OK] t={Time.time:F2}s, attacker={(attacker ? attacker.name : "null")}");
            animator?.SetTrigger("Parry");

            var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
            parryableTarget?.OnParried(transform.position);

            // === 패링 후 딜레이 계산: (패링 윈도우 종료 시각 + 0.1) - 지금
            float windowEnd = (defense ? defense.LastBlockPressedTime : Time.time) + ParryWindow;
            float lockDuration = Mathf.Max(0f, (windowEnd + 0.1f) - Time.time);

            // 1) 이동/공격 입력 봉인(조작 잠금)
            StartParryLock(lockDuration);

            // 2) 가드 유지: 패링 윈도우 끝 ~ +0.1초까지 포함해서 전체 lockDuration 동안 유지
            defense?.ForceBlockFor(lockDuration);

            // (선택) 아주 짧은 i-frame
            iFrameEndTime = Time.time + 0.05f;
            return;
        }

        else if (outcome == DefenseOutcome.Block)
        {
            float finalDamage = damage * blockDamageMul;
            float finalKnock = knockback * blockKnockMul;

            ApplyDamage(finalDamage);
            ApplyKnockbackXOnly(inFrontDir, finalKnock);

            stamina -= damage; // 필요하면 계수로 바꿔도 됨
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

            animator?.SetTrigger("BlockHit");
            return;
        }

        // 가드 실패(측/후방 포함)
        ApplyDamage(damage);
        ApplyKnockbackXOnly(inFrontDir, knockback);

        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw);
    }

    public void StartParryLock(float duration)
    {
        if (duration <= 0f) return;
        parryLockEndTime = Mathf.Max(parryLockEndTime, Time.time + duration);
        if (parryLockCo == null) parryLockCo = StartCoroutine(ParryLockRoutine());

        // 이동은 잠그되, 현재 넉백 속도는 살림
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);
    }

    private IEnumerator ParryLockRoutine()
    {
        while (Time.time < parryLockEndTime)
            yield return null;

        // 잠금 해제(다른 상태가 잠그고 있지 않다는 가정)
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        parryLockCo = null;
    }


    // === 공유 유틸 ===
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
        // TODO: 리스폰 / 게임오버
    }

    // X축으로만 넉백
    public void ApplyKnockbackXOnly(Vector2 dirToEnemy, float force)
    {
        if (!rb || force <= 0f) return;
        float x = -Mathf.Sign(dirToEnemy.x);
        if (Mathf.Abs(x) < 0.0001f)
            x = (moveRef && Mathf.Abs(moveRef.LastFacing.x) > 0.0001f) ? Mathf.Sign(moveRef.LastFacing.x) : 1f;

        rb.linearVelocity = new Vector2(x * force, 0f);
    }

    public void StartHitstun(float duration)
    {
        if (duration <= 0f) return;

        // 공격은 각자 스크립트에서 취소하지만, 조작 잠금은 여기서 통일
        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);
        if (hitstunCo == null) hitstunCo = StartCoroutine(HitstunRoutine());

        // 물리는 살리고 조작만 잠금
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);
        animator?.SetTrigger("Hit");
    }

    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        while (Time.time < hitstunEndTime) yield return null;
        inHitstun = false;
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        hitstunCo = null;
    }
}

// 선택: 적이 패링되었을 때 반응
public interface IParryable { void OnParried(Vector3 parrySourcePosition); }
// 플레이어 공격이 적에게 전달하는 인터페이스
public interface IDamageable
{
    void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker);
}
