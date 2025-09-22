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
    [Header("Facing (Auto)")]
    [SerializeField] private bool autoFaceOnCombatEnter = true;   // 전투 진입 시 자동 바라보기
    [SerializeField] private float autoFaceSearchRadius = 15f;    // 탐색 반경
    [SerializeField] private float disengageDistance = 150f;     // 비전투 해제 거리
    [SerializeField] private float enemyScanInterval = 0.25f;    // 거리 체크 간격
    [SerializeField, Range(0f, 1f)] private float combatYSpeedMultiplier = 0.3f; // 전투 중 Y축 속도 배수
    [SerializeField] private bool autoFaceDuringCombat = true;   //  전투 중 지속 추적
    [SerializeField] private bool autoFaceEvenWhileAttacking = false; // 공격 중에도 돌릴지
    [SerializeField] private float autoFaceInputDeadzoneX = 0.2f; // 플레이어가 이 이상 좌/우로 입력 주면 자동 전환 잠시 무시
    private bool inCombat = false;
    private Coroutine combatMonitorCo;

    public bool IsInCombat => inCombat;
    public float CombatYSpeedMul => combatYSpeedMultiplier;


    // --------- Guard / Weaving (수치조정 가능) ---------
    [Header("Guard / Parry (Config)")]
    [Header("Weaving (Parry) Lock")]
    [SerializeField] private float weavingPostHold = 0.10f;
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
    [SerializeField] private float windup = 0.20f;  
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


    // === 에니메이션 가짓수 ===
    [Header("Animation Variants")]
    [SerializeField] private int hitVariants = 3;      // Hit1..HitN
    [SerializeField] private int weavingVariants = 3;  // Weaving1..WeavingN


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

        // ★ 비전투로 돌아오면 다시 이동으로 X플립 허용
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
            if (d2 < bestSqr)
            {
                bestSqr = d2;
                nearest = h.transform;
            }
        }

        if (nearest && moveRef)
            moveRef.FaceTargetX(nearest.position.x);
    }


    public void EnterCombatByInteraction() => EnterCombat("Interaction");

    

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
        var wait = new WaitForSeconds(enemyScanInterval);
        while (inCombat)
        {
            // ★ 전투 중 자동 바라보기
            if (autoFaceDuringCombat)
                AutoFaceTick();

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

        // 플레이어가 수평 입력을 크게 주고 있으면, 의도 우선 → 자동 전환 잠시 보류
        if (Mathf.Abs(moveRef.CurrentInput.x) >= autoFaceInputDeadzoneX)
            return;

        // 공격 중 자동 전환 끄기 옵션
        if (!autoFaceEvenWhileAttacking && attack != null && attack.IsAttacking)
            return;

        // 필요 시: 가드브레이크/사망 등 예외 추가 가능
        // if (IsStaminaBroken) return;

        // 가장 가까운 적을 향해 X-플립 (이 함수는 이전에 추가한 그대로 사용)
        TryAutoFaceNearestEnemyX();
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

        // 전투 상태 진입
        EnterCombat("GotHit");

        // 정면 콘 판정
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 inFrontDir = -hitDir.normalized;  // 플레이어→적
        bool inFront = Vector2.Dot(facing, inFrontDir) >= Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);

        // 방어 시스템 판정
        var outcome = defense ? defense.Evaluate(inFront, parryable) : DefenseOutcome.None;

        // --- 패링 성공 ---
        if (outcome == DefenseOutcome.Parry)
        {
            if (debugLogs) Debug.Log($"[WEAVING OK] t={Time.time:F2}s, attacker={(attacker ? attacker.name : "null")}");
            PlayRandomWeaving();

            var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
            parryableTarget?.OnParried(transform.position);

            // 패링 윈도우 잔여시간 + 0.1초 동안 조작잠금 & 가드 유지
            float windowEnd = (defense ? defense.LastBlockPressedTime : Time.time) + ParryWindow;
            float lockDuration = Mathf.Max(0f, (windowEnd + weavingPostHold) - Time.time);

            StartParryLock(lockDuration);         // 이동/공격 입력 잠금, 물리속도 유지
            defense?.ForceBlockFor(lockDuration); // 가드 유지

            // 짧은 i-frame (선택)
            iFrameEndTime = Time.time + 0.05f;
            return;
        }
        // --- 가드 성공(패링 아님) ---
        else if (outcome == DefenseOutcome.Block)
        {
            float finalDamage = damage * blockDamageMul;
            float finalKnock = knockback * blockKnockMul;

            ApplyDamage(finalDamage);
            ApplyKnockbackXOnly(inFrontDir, finalKnock);

            stamina -= damage; // 필요시 계수 조정
            OnStaminaChanged?.Invoke(stamina, staminaMax);

            if (stamina <= 0f)
            {
                StaminaBreak();
            }
            else
            {
                float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                StartHitstun(stun, playHitAnim: false); // ★ 가드시 Hit 애니 금지
            }

            animator?.SetTrigger("BlockHit");
            return;
        }

        // --- 가드 실패 / 측·후방 / 비방어 ---
        ApplyDamage(damage);
        ApplyKnockbackXOnly(inFrontDir, knockback);

        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw); // 기본값: Hit 애니 재생
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

    // 변경
    public void StartHitstun(float duration, bool playHitAnim = true)
    {
        if (duration <= 0f) return;

        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);
        if (hitstunCo == null) hitstunCo = StartCoroutine(HitstunRoutine());

        // 물리는 살리고 조작만 잠금
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);

        // ★ 여기서 조건부로만 Hit 트리거
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

    private void PlayRandomWeaving()
    {
        if (!animator) return;
        int idx = Mathf.Clamp(Random.Range(1, weavingVariants + 1), 1, weavingVariants);
        animator.SetTrigger($"Weaving{idx}");
    }


}


// 선택: 적이 패링되었을 때 반응
public interface IParryable { void OnParried(Vector3 parrySourcePosition); }
// 플레이어 공격이 적에게 전달하는 인터페이스
public interface IDamageable
{
    void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker);
}
