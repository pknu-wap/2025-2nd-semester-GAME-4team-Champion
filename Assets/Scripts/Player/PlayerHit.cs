using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;          // HP/STM/전투/전역락
    [SerializeField] private PlayerDefense defense;        // 가드/위빙(패링) 판정 & 락
    [SerializeField] private PlayerAttack attack;          // Weaving 인덱스 전달/카운터 창
    [SerializeField] private Player_Heal healer;           // (선택) 치유 중단용
    [SerializeField] private PlayerMoveBehaviour moveRef;  // 이동락/바라보는 방향
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    [Header("Hit Reaction (Config)")]
    [SerializeField] private float baseHitstun = 1.5f;     // 기본 경직
    [SerializeField] private float blockHitstunMul = 0.5f;  // 가드시 경직 배수

    [Header("On-Hit Locks / Stamina")]
    [SerializeField] private float actionLockOnUnguardedHit = 2f; // 비가드 피격 시 전역 행동락 시간
    [SerializeField] private float staminaLossOnHitMul = 1.0f;       // 피격 시 스태미나 추가 감소 배수

    [Header("Animation Variants")]
    [SerializeField] private int weavingVariants = 3;       // Weaving1..N
    [SerializeField] private int hitVariants = 3;           // Hit1..N

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // === 상태 ===
    private bool inHitstun = false;
    private float hitstunEndTime = 0f;
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;

    public bool InHitstun => inHitstun;

    private void Awake()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!attack) attack = GetComponent<PlayerAttack>();
    }

    private void Reset()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!attack) attack = GetComponent<PlayerAttack>();
    }

    public void Bind(PlayerCombat c, PlayerMoveBehaviour m, Animator a, Rigidbody2D rigid = null)
    {
        combat = c; moveRef = m; animator = a; rb = rigid ? rigid : rb;
    }

    private Vector2 FacingOrRight()
    {
        return (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
    }

    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable,
                      GameObject attacker = null, float hitstun = -1f)
    {
        // i-프레임 중이면 무시
        if (Time.time < iFrameEndTime) return;

        // 치유 중이면 즉시 중단
        if (healer && healer.IsHealing) healer.CancelHealing();

        // 전투 진입
        combat?.EnterCombat("GotHit");

        // 방향 준비
        Vector2 facing = FacingOrRight();
        Vector2 toEnemy = -hitDir.normalized; // 플레이어→적

        // 방어/위빙 판정 (정면 콘/패링 윈도는 PlayerDefense가 담당)
        var outcome = defense ? defense.Evaluate(facing, toEnemy, parryable) : DefenseOutcome.None; // PlayerDefense 기준

        // ===== 위빙(패링) 성공 =====
        if (outcome == DefenseOutcome.Parry)
        {
            if (debugLogs) Debug.Log($"[WEAVING OK] t={Time.time:F2}s, attacker={(attacker ? attacker.name : "null")}");

            // ★ 인덱스는 '한 번만' 뽑는다
            int idx = PickWeavingIndex();

            // 위빙 애니도, 매칭용 인덱스도 같은 값으로
            if (animator) animator.SetTrigger($"Weaving{idx}");
            if (attack) attack.SetLastWeavingIndex(idx);

            // (선택) 패링 콜백
            var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
            parryableTarget?.OnParried(transform.position);

            // 락/가드 유지/리게인
            float windowEnd = defense.LastBlockPressedTime + defense.ParryWindow;
            float lockDur = Mathf.Max(0f, (windowEnd + defense.PostHold) - Time.time);
            defense.StartParryLock(lockDur, true);
            defense.ForceBlockFor(lockDur);
            defense.OnWeavingSuccessRegain();

            // ★ 같은 인덱스를 유지한 상태로 카운터 창 오픈 (중복 SetLastWeavingIndex 제거)
            if (attack)
            {
                attack.ArmCounter(lockDur * 2f);
                Debug.Log($"[COUNTER-ARM] idx={idx}, window={(lockDur * 2f):F2}s");
            }

            iFrameEndTime = Time.time + 0.05f;
            return;
        }

        // ===== 일반 가드 성공 =====
        if (outcome == DefenseOutcome.Block)
        {
            float finalDamage = damage * defense.BlockDamageMul;
            float finalKnock = knockback * defense.BlockKnockMul;

            combat?.ApplyDamage(finalDamage);
            ApplyKnockbackXOnly(toEnemy, finalKnock);

            // 스태미나 감소 & 브레이크 처리
            float guardSpend = damage * defense.GuardHitStaminaCostMul;
            combat.AddStamina(-damage * defense.GuardHitStaminaCostMul);
            defense.RegisterGuardHitStaminaCost(guardSpend);
            if (combat && combat.Stamina <= 0f) defense.TriggerStaminaBreak();
            else
            {
                float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                StartHitstun(stun, playHitAnim: false); // 가드 중 Hit 애니 금지
            }

            animator?.SetTrigger("BlockHit");
            return;
        }

        // ===== 가드 실패 / 측·후방 / 비방어 =====
        combat?.ApplyDamage(damage);
        // 비가드 피격 시 전역 행동 잠금(공격 연타 방지 용도) — PlayerCombat에 맞춤
        combat?.StartActionLock(actionLockOnUnguardedHit, false);

        ApplyKnockbackXOnly(toEnemy, knockback);

        // 피격 시 스태미나 감소(배수는 이 클래스에서 직렬화로 조정)
        if (combat) combat.AddStamina(-damage * staminaLossOnHitMul);
        if (combat && combat.Stamina <= 0f) defense.TriggerStaminaBreak();
        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw, playHitAnim: true);
    }

    // X축 넉백만 적용(상하 미끄러짐 방지)
    private void ApplyKnockbackXOnly(Vector2 dirToEnemy, float force)
    {
        if (!rb || force <= 0f) return;
        float x = -Mathf.Sign(dirToEnemy.x);
        if (Mathf.Abs(x) < 0.0001f)
            x = (moveRef && Mathf.Abs(moveRef.LastFacing.x) > 0.0001f)
              ? Mathf.Sign(moveRef.LastFacing.x) : 1f;

        rb.linearVelocity = new Vector2(x * force, 0f);
    }

    public void StartHitstun(float duration, bool playHitAnim = true)
    {
        combat?.BlockStaminaRegenFor(duration);
        if (duration <= 0f) return;

        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);
        if (hitstunCo == null) hitstunCo = StartCoroutine(HitstunRoutine());

        // 물리는 살리고 조작만 잠금
        moveRef?.AddMovementLock("HITSTUN", hardFreezePhysics: false, zeroVelocity: true);

        if (playHitAnim) PlayRandomHit();
    }

    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        while (Time.time < hitstunEndTime) yield return null;
        inHitstun = false;
        moveRef?.RemoveMovementLock("HITSTUN", hardFreezePhysics: false);
        hitstunCo = null;
    }

    private void PlayRandomHit()
    {
        if (!animator || hitVariants <= 0) return;
        int idx = Mathf.Clamp(Random.Range(1, hitVariants + 1), 1, hitVariants);
        animator.SetTrigger($"Hit{idx}");
    }

    // Weaving 트리거와 Counter 매칭을 위해 인덱스를 뽑아 전달
    private int PickWeavingIndex()
    {
        int max = Mathf.Max(1, weavingVariants);
        return Mathf.Clamp(Random.Range(1, max + 1), 1, max);
    }

    public void PlayWeaving(int idx)
    {
        if (!animator) return;
        idx = Mathf.Clamp(idx, 1, Mathf.Max(1, weavingVariants));
        animator.SetTrigger($"Weaving{idx}");
    }
}
