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
    [SerializeField] private PlayerDefense defense;   // 선택
    [SerializeField] private PlayerAttack attack;     // 선택
    [SerializeField] private PlayerHit hit;           // 선택
    [SerializeField] private Player_Heal healer;      // 선택
    [SerializeField] private CombatState combatState; // ★ 전투상태 전담 컴포넌트

    // ---------- Vitals ----------
    [Header("Vitals")]
    [SerializeField] private float hpMax = 100f;
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float staminaRegenPerSec = 25f;
    [SerializeField] private float staminaBreakTime = 1.5f;

    public event Action<float, float> OnHealthChanged;   // (current, max)
    public event Action<float, float> OnStaminaChanged;  // (current, max)

    // ===== Stamina regen block (공통) =====
    [Header("Stamina Regen Control")]
    [SerializeField] private float regenBlockExtra = 1f;   // 행동 종료 후 추가로 막을 시간
    private float noRegenUntil = 0f;

    public float RegenBlockExtra
    {
        get => regenBlockExtra;
        set => regenBlockExtra = Mathf.Max(0f, value);
    }
    public bool IsStaminaRegenBlocked => Time.time < noRegenUntil;

    private float hp;
    private float stamina;
    public bool IsStaminaBroken { get; private set; } = false;
    public float HP => hp;
    public float HPMax => hpMax;
    public float Stamina => stamina;
    public float StaminaMax => staminaMax;
    public float Hp01 => Mathf.Clamp01(hp / Mathf.Max(1f, hpMax));
    public float Stamina01 => Mathf.Clamp01(stamina / Mathf.Max(1f, staminaMax));

    // ---------- Debug ----------
    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool showActionLockDebug = false;
    public bool DebugLogs => debugLogs;

    // ---------- Global Action Lock ----------
    private float actionLockEndTime = 0f;
    private Coroutine actionLockCo;

    /// <summary>현재 전역 행동(공격/방어/입력) 잠금 여부</summary>
    public bool IsActionLocked => Time.time < actionLockEndTime;
    /// <summary>구버전 호환: IsAttackLocked</summary>
    public bool IsAttackLocked => IsActionLocked;
    public float ActionLockRemaining => Mathf.Max(0f, actionLockEndTime - Time.time);

    private void Reset()
    {
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!attack) attack = GetComponent<PlayerAttack>();
        if (!hit) hit = GetComponent<PlayerHit>();
        if (!healer) healer = GetComponent<Player_Heal>();
        if (!combatState) combatState = GetComponent<CombatState>();
    }

    private void Awake()
    {
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!attack) attack = GetComponent<PlayerAttack>();
        if (!hit) hit = GetComponent<PlayerHit>();
        if (!healer) healer = GetComponent<Player_Heal>();
        if (!combatState) combatState = GetComponent<CombatState>();
        if (!combatState) combatState = gameObject.AddComponent<CombatState>(); // 안전장치

        // 초기화
        hp = hpMax;
        stamina = staminaMax;
        OnHealthChanged?.Invoke(hp, hpMax);
        OnStaminaChanged?.Invoke(stamina, staminaMax);

        // 바인딩
        attack?.Bind(this, moveRef, animator);
        defense?.Bind(this, moveRef, animator);
        hit?.Bind(this, moveRef, animator, rb);
        healer?.Bind(this, moveRef, animator);
        combatState?.Bind(this, moveRef, animator, attack);
    }

    private void OnEnable() => StartCoroutine(VitalsTick());
    private void OnDisable() => StopAllCoroutines();

    // ---------------- Vitals Ops ----------------
    public void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Max(0f, hp - amount);
        OnHealthChanged?.Invoke(hp, hpMax);
        if (debugLogs) Debug.Log($"[HP] -{amount} => {hp}/{hpMax}");
        if (hp <= 0f) OnDeath();
    }

    public void OnStaminaBreak()
    {
        if (IsStaminaBroken) return;
        IsStaminaBroken = true;

        moveRef?.SetMovementLocked(true, hardFreezePhysics: true);
        animator?.SetBool("GuardBroken", true);
        animator?.SetTrigger("GuardBreak");

        StartCoroutine(CoRecoverFromStaminaBreak());
    }

    private IEnumerator CoRecoverFromStaminaBreak()
    {
        yield return new WaitForSeconds(staminaBreakTime);
        IsStaminaBroken = false;
        animator?.SetBool("GuardBroken", false);
        moveRef?.SetMovementLocked(false, hardFreezePhysics: true);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Min(hpMax, hp + amount);
        OnHealthChanged?.Invoke(hp, hpMax);
        if (debugLogs) Debug.Log($"[HP] +{amount} => {hp}/{hpMax}");
    }

    public void AddStamina(float delta)
    {
        float before = stamina;
        stamina = Mathf.Clamp(stamina + delta, 0f, staminaMax);
        if (!Mathf.Approximately(before, stamina))
            OnStaminaChanged?.Invoke(stamina, staminaMax);
        // 디버그라면 남겨둠
        // if (debugLogs) Debug.Log($"[STM] {(delta>=0?"+":"")}{delta} => {stamina}/{staminaMax}");
    }

    /// <summary>행동으로 인해 '못 움직이는 시간 + extra' 만큼 스태미나 재생을 막는다.</summary>
    public void BlockStaminaRegenFor(float baseSeconds)
    {
        float target = Time.time + Mathf.Max(0f, baseSeconds) + regenBlockExtra;
        if (target > noRegenUntil) noRegenUntil = target;
    }

    private IEnumerator VitalsTick()
    {
        var waitEndFrame = new WaitForEndOfFrame();
        while (true)
        {
            if (defense && !defense.IsStaminaBroken && !defense.IsBlocking
                && stamina < staminaMax && !IsStaminaRegenBlocked)
            {
                stamina = Mathf.Min(staminaMax, stamina + staminaRegenPerSec * Time.deltaTime);
                OnStaminaChanged?.Invoke(stamina, staminaMax);
            }
            yield return waitEndFrame;
        }
    }

    private void OnDeath()
    {
        if (debugLogs) Debug.Log("[Player] DEAD");
        moveRef?.SetMovementLocked(true, true);
        animator?.SetTrigger("Die");
    }

    // ---------------- Global Action Lock ----------------
    public void StartActionLock(float duration, bool zeroVelocityOnStart = false)
    {
        float until = Time.time + Mathf.Max(0f, duration);
        if (until <= actionLockEndTime && actionLockCo != null) return; // 이미 더 길게 잠금

        actionLockEndTime = until;
        if (actionLockCo != null) StopCoroutine(actionLockCo);
        actionLockCo = StartCoroutine(ActionLockRoutine(zeroVelocityOnStart));
    }

    private IEnumerator ActionLockRoutine(bool zeroVelocityOnStart)
    {
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: zeroVelocityOnStart);

        while (Time.time < actionLockEndTime)
            yield return null;

        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        actionLockCo = null;

        if (showActionLockDebug) Debug.Log("[ActionLock] released");
    }

    // ----------------- Combat State (위임/래핑) -----------------
    public bool IsInCombat => combatState != null && combatState.IsInCombat;
    public float CombatYSpeedMul => combatState ? combatState.CombatYSpeedMul : 1f;
    public LayerMask EnemyMask => combatState ? combatState.EnemyMask : default;

    public void EnterCombat(string reason = null) => combatState?.EnterCombat(reason);
    public void ExitCombat() => combatState?.ExitCombat();
}
