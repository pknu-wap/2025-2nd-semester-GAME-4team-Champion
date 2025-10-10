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
    [SerializeField] private float baseHitstun = 1.5f;      // 기본 경직
    [SerializeField] private float blockHitstunMul = 0.5f;  // 가드시 경직 배수

    [Header("On-Hit Locks / Stamina")]
    [SerializeField] private float actionLockOnUnguardedHit = 2f; // 비가드 피격 시 전역 행동락 시간
    [SerializeField] private float staminaLossOnHitMul = 1.0f;    // 피격 시 스태미나 추가 감소 배수

    [Header("Animation Variants")]
    [SerializeField] private int weavingVariants = 3;       // Weaving1..N
    [SerializeField] private int hitVariants = 3;           // Hit1..N

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // === 태그 이벤트(외부 인식용) ===
    public const string TAG_WEAVING_SUCCESS = "Tag.WeavingSuccess";
    public const string TAG_GUARD_SUCCESS = "Tag.GuardSuccess";
    public const string TAG_GOT_HIT = "Tag.GotHit";
    public event System.Action<string> OnTag; // GameManager/Enemy에서 구독

    // === 상태 ===
    private bool inHitstun = false;
    private bool invulnWhileDead = false;
    private float hitstunEndTime = 0f;
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;

    // 랜덤 애니 중복 방지
    private int lastWeavingIdx = -1;
    private int lastHitIdx = -1;

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

    public void SetDeadInvulnerable(bool on)
    {
        invulnWhileDead = on;

        // 죽는 순간 남은 히트스턴/락도 즉시 해제(옵션이지만 안전)
        if (on)
        {
            inHitstun = false;
            if (hitstunCo != null) { StopCoroutine(hitstunCo); hitstunCo = null; }
            moveRef?.RemoveMovementLock("HITSTUN", hardFreezePhysics: false);
        }
    }

    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable,
                      GameObject attacker = null, float hitstun = -1f)
    {
        if (invulnWhileDead || (combat != null && combat.HP <= 0f))
            return;

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
        var outcome = defense ? defense.Evaluate(facing, toEnemy, parryable) : DefenseOutcome.None;

        // ===== 위빙(패링) 성공 =====
        if (outcome == DefenseOutcome.Parry)
        {
            var parryProvider = GetComponentInParent<IParryWindowProvider>();
            bool parryBySkill = parryProvider != null && parryProvider.IsParryWindowActive;
            if (parryBySkill)
            {
                outcome = DefenseOutcome.Parry;
                parryProvider.OnParrySuccess();
            }

            if (debugLogs) Debug.Log($"[WEAVING OK] t={Time.time:F2}s, attacker={(attacker ? attacker.name : "null")}");
            OnTag?.Invoke(TAG_WEAVING_SUCCESS); // ★ 태그 이벤트 발행

            // 인덱스 1회만 뽑고 즉시 중복 방지
            int idx = NextVariantNoRepeat(Mathf.Max(1, weavingVariants), lastWeavingIdx);
            lastWeavingIdx = idx;

            // 위빙 애니/공격 카운터 매칭
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

            if (attack)
            {
                attack.ArmCounter(lockDur * 2f);
                if (debugLogs) Debug.Log($"[COUNTER-ARM] idx={idx}, window={(lockDur * 2f):F2}s");
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
            combat.AddStamina(-guardSpend);
            defense.RegisterGuardHitStaminaCost(guardSpend);
            if (combat && combat.Stamina <= 0f) defense.TriggerStaminaBreak();
            else
            {
                float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                StartHitstun(stun, playHitAnim: false); // 가드 중 Hit 애니 금지
            }

            animator?.SetTrigger("BlockHit");
            OnTag?.Invoke(TAG_GUARD_SUCCESS); // ★ 태그 이벤트 발행
            return;
        }

        // ===== 가드 실패 / 측·후방 / 비방어 =====
        combat?.ApplyDamage(damage);
        combat?.StartActionLock(actionLockOnUnguardedHit, false);
        ApplyKnockbackXOnly(toEnemy, knockback);

        // 피격 시 스태미나 감소
        if (combat) combat.AddStamina(-damage * staminaLossOnHitMul);
        if (combat && combat.Stamina <= 0f) defense.TriggerStaminaBreak();

        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw, playHitAnim: true);

        OnTag?.Invoke(TAG_GOT_HIT); // ★ 태그 이벤트 발행
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

        if (playHitAnim) PlayRandomHit_NoImmediateRepeat();
    }

    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        while (Time.time < hitstunEndTime) yield return null;
        inHitstun = false;
        moveRef?.RemoveMovementLock("HITSTUN", hardFreezePhysics: false);
        hitstunCo = null;
    }

    public void AddIFrames(float duration)
    {
        iFrameEndTime = Mathf.Max(iFrameEndTime, Time.time + Mathf.Max(0f, duration));
    }

    public void CancelHitstun()
    {
        hitstunEndTime = Time.time;
        if (hitstunCo != null) { StopCoroutine(hitstunCo); hitstunCo = null; }
        inHitstun = false;
        moveRef?.RemoveMovementLock("HITSTUN", hardFreezePhysics: false);
    }

    private void OnDisable()
    {
        if (hitstunCo != null) { StopCoroutine(hitstunCo); hitstunCo = null; }
        inHitstun = false;
        moveRef?.RemoveMovementLock("HITSTUN", false); // 잔존락 제거
    }

    // ===== 랜덤 애니: 즉시 중복 방지 버전 =====
    private int NextVariantNoRepeat(int count, int last)
    {
        if (count <= 1) return 1;
        int idx = Random.Range(1, count + 1);
        if (idx == last) idx = (idx % count) + 1; // 바로 다음 번호로 회피
        return idx;
    }

    private void PlayRandomHit_NoImmediateRepeat()
    {
        if (!animator || hitVariants <= 0) return;
        int idx = NextVariantNoRepeat(Mathf.Max(1, hitVariants), lastHitIdx);
        lastHitIdx = idx;
        animator.SetTrigger($"Hit{idx}");
    }

    // (Weaving 인덱스 수동 재생용 보조)
    public void PlayWeaving(int idx)
    {
        if (!animator) return;
        idx = Mathf.Clamp(idx, 1, Mathf.Max(1, weavingVariants));
        // 수동 호출 시에도 lastWeavingIdx 갱신
        lastWeavingIdx = idx;
        animator.SetTrigger($"Weaving{idx}");
    }
}
