using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;          // HP/STM/전투/전역락
    [SerializeField] private PlayerDefense defense;        // 가드/위빙(패링) 판정 & 락
    [SerializeField] private PlayerAttack attack;          // 카운터/위빙 인덱스 전달
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
        [SerializeField] private bool playWeavingAnimOnParry = false;
    [SerializeField] private int weavingVariants = 3;       // Weaving1..N
    [SerializeField] private int hitVariants = 3;           // Hit1..N

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // === 태그 이벤트(외부 인식용) ===
    public const string TAG_WEAVING_SUCCESS = "Tag.WeavingSuccess";
    public const string TAG_GUARD_SUCCESS = "Tag.GuardSuccess";
    public const string TAG_GOT_HIT = "Tag.GotHit";
    public event System.Action<string> OnTag; // GameManager/Enemy에서 구독

    // === 인터럽트 방송용 구조체 & 버퍼 ===
    public struct HitInterruptInfo
    {
        public bool Blocked;     // 가드 피해인지
        public bool Parried;     // 패링 성공인지
        public float Damage;     // 실제 들어간 피해(감쇠 후)
        public GameObject Attacker;
    }
    private static readonly List<IHitInterruptListener> _listenersBuf = new List<IHitInterruptListener>(8);

    // === 상태 ===
    private bool _inHitstun = false;        // 내부용 필드(이름 변경)
    private bool invulnWhileDead = false;
    private float hitstunEndTime = 0f;
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;
    private int lastWeavingIdx = -1;
    private int lastHitIdx = -1;

    // 🔸 외부에서 안전하게 읽게 해주는 공개 프로퍼티(읽기 전용)
    public bool InHitstun => _inHitstun;

    // 🔸 기존 외부 코드 호환용(소문자 이름으로 접근해도 동작)
    public bool inHitstun => _inHitstun;

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

    // 🔸 다른 곳에서 이걸 호출한다면 유지가 필요합니다. (없으면 호출부만 제거해도 무방)
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

        if (on)
        {
            _inHitstun = false;
            if (hitstunCo != null) { StopCoroutine(hitstunCo); hitstunCo = null; }
            moveRef?.RemoveMovementLock("HITSTUN", hardFreezePhysics: false);
        }
    }

    /// <summary>공격에 맞았을 때 들어오는 공통 진입점</summary>
    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable,
                      GameObject attacker = null, float hitstun = -1f)
    {
        if (invulnWhileDead || (combat != null && combat.HP <= 0f)) return;
        if (Time.time < iFrameEndTime) return; // i-frame

        if (healer && healer.IsHealing) healer.CancelHealing();

        combat?.EnterCombat("GotHit");

        Vector2 facing = FacingOrRight();
        Vector2 toEnemy = -hitDir.normalized; // 플레이어→적

        // 1) 방어/위빙(패링) 시스템 우선 판정
        var outcome = defense ? defense.Evaluate(facing, toEnemy, parryable) : DefenseOutcome.None;

        // 2) 스킬의 패링 윈도우 검사 (자식 방향 전체 스캔)
        IParryWindowProvider activeParry = FindActiveParryWindow();
        if (activeParry != null)
        {
            // 정책: parryable == false면 강제 패링하지 않음 (필요시 허용 가능)
            if (parryable)
            {
                outcome = DefenseOutcome.Parry;
                activeParry.OnParrySuccess();
            }
        }

        // 3) 결과 처리
        if (outcome == DefenseOutcome.Parry)
        {
            if (debugLogs) Debug.Log($"[WEAVING OK] t={Time.time:F2}s, attacker={(attacker ? attacker.name : "null")}");

            OnTag?.Invoke(TAG_WEAVING_SUCCESS);

            int idx = NextVariantNoRepeat(Mathf.Max(1, weavingVariants), lastWeavingIdx);
            lastWeavingIdx = idx;
            animator?.SetTrigger($"Weaving{idx}");
            attack?.SetLastWeavingIndex(idx);

            var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
            parryableTarget?.OnParried(transform.position);

            float windowEnd = defense.LastBlockPressedTime + defense.ParryWindow;
            float lockDur = Mathf.Max(0f, (windowEnd + defense.PostHold) - Time.time);
            defense.StartParryLock(lockDur, true);
            defense.ForceBlockFor(lockDur);
            defense.OnWeavingSuccessRegain();

            if (attack) attack.ArmCounter(lockDur * 2f);

            iFrameEndTime = Time.time + 0.05f;
            return; // 패링 성공 시 종료(스킬 인터럽트 방송 X)
        }

        if (outcome == DefenseOutcome.Block)
        {
            float finalDamage = damage * defense.BlockDamageMul;
            float finalKnock = knockback * defense.BlockKnockMul;

            combat?.ApplyDamage(finalDamage);
            ApplyKnockbackXOnly(toEnemy, finalKnock);

            float guardSpend = damage * defense.GuardHitStaminaCostMul;
            if (!(combat && combat.IsStaminaBroken))
            {
                combat.AddStamina(-guardSpend);
                defense.RegisterGuardHitStaminaCost(guardSpend);
                if (combat && combat.Stamina <= 0f) defense.TriggerStaminaBreak();
            }
            else
            {
                float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                StartHitstun(stun, playHitAnim: false);
            }

            animator?.SetTrigger("BlockHit");
            OnTag?.Invoke(TAG_GUARD_SUCCESS);

            // 가드시에도 스킬 인터럽트 방송(요청 사양)
            NotifyHitInterrupt(blocked: true, parried: false, damageApplied: finalDamage, attacker);
            return;
        }

        // ===== 가드 실패 / 비방어 =====
        combat?.ApplyDamage(damage);
        combat?.StartActionLock(actionLockOnUnguardedHit, false);
        ApplyKnockbackXOnly(toEnemy, knockback);

        if (!(combat && combat.IsStaminaBroken))
        {
            combat.AddStamina(-damage * staminaLossOnHitMul);
        }
        if (combat && combat.Stamina <= 0f) defense.TriggerStaminaBreak();

        float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
        StartHitstun(stunRaw, playHitAnim: true);

        OnTag?.Invoke(TAG_GOT_HIT);

        // 스킬 인터럽트 방송
        NotifyHitInterrupt(blocked: false, parried: false, damageApplied: damage, attacker);
    }

    /// <summary>플레이어 자식 트리에서 "현재 열려 있는" 패링 윈도우 제공자를 찾아 반환</summary>
    private IParryWindowProvider FindActiveParryWindow()
    {
        var providers = GetComponentsInChildren<IParryWindowProvider>(true);
        for (int i = 0; i < providers.Length; i++)
        {
            var p = providers[i];
            if (p != null && p.IsParryWindowActive) return p;
        }
        return null;
    }

    // === 스킬 인터럽트 방송 ===
    private void NotifyHitInterrupt(bool blocked, bool parried, float damageApplied, GameObject attacker)
    {
        if (parried) return;

        _listenersBuf.Clear();
        GetComponentsInChildren(_listenersBuf); // 플레이어 트리 아래의 모든 Listener 수집

        var info = new HitInterruptInfo
        {
            Blocked = blocked,
            Parried = parried,
            Damage = damageApplied,
            Attacker = attacker
        };

        for (int i = 0; i < _listenersBuf.Count; i++)
        {
            try { _listenersBuf[i].OnPlayerHitInterrupt(info); }
            catch (System.SystemException e)
            {
                if (debugLogs) Debug.LogWarning($"[Interrupt] listener error: {e.Message}");
            }
        }
    }

    // X축 넉백만 적용(상하 미끄러짐 방지)
    private void ApplyKnockbackXOnly(Vector2 dirToEnemy, float force)
    {
        if (!rb || force <= 0f) return;
        float x = -Mathf.Sign(dirToEnemy.x);
        if (Mathf.Abs(x) < 0.0001f)
            x = (moveRef && Mathf.Abs(moveRef.LastFacing.x) > 0.0001f)
              ? Mathf.Sign(moveRef.LastFacing.x) : 1f;

        // ✅ Rigidbody2D의 올바른 속성은 velocity 입니다.
        rb.linearVelocity = new Vector2(x * force, 0f);
    }

    public void StartHitstun(float duration, bool playHitAnim = true)
    {
        combat?.BlockStaminaRegenFor(duration);
        if (duration <= 0f) return;

        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);
        if (hitstunCo == null) hitstunCo = StartCoroutine(HitstunRoutine());

        moveRef?.AddMovementLock("HITSTUN", hardFreezePhysics: false, zeroVelocity: true);

        if (playHitAnim) PlayRandomHit_NoImmediateRepeat();
    }

    private IEnumerator HitstunRoutine()
    {
        _inHitstun = true;
        while (Time.time < hitstunEndTime) yield return null;
        _inHitstun = false;
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
        _inHitstun = false;
        moveRef?.RemoveMovementLock("HITSTUN", hardFreezePhysics: false);
    }

    private void OnDisable()
    {
        if (hitstunCo != null) { StopCoroutine(hitstunCo); hitstunCo = null; }
        _inHitstun = false;
        moveRef?.RemoveMovementLock("HITSTUN", false);
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

    public void PlayWeaving(int idx)
    {
        if (!animator) return;
        idx = Mathf.Clamp(idx, 1, Mathf.Max(1, weavingVariants));
        lastWeavingIdx = idx;
        animator.SetTrigger($"Weaving{idx}");
    }
}
