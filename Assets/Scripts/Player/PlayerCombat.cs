using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerDefense defense;
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerHit hit;   // ★ 피격 전용 컴포넌트

    // --------- Vitals (체력/스태미나) ---------
    [Header("Vitals")]
    [SerializeField] private float hpMax = 100f;
    [SerializeField] private float stamina = 100f;
    [SerializeField] private float staminaRegenPerSec = 25f;
    [Header("Stamina on Hit")]
    [SerializeField] private float staminaLossOnHitMul = 1.0f; // 비가드 피격 시 소모 배수(= 데미지 * 이 값)


    private float hp;
    public float Hp01 => Mathf.Clamp01(hp / Mathf.Max(1f, hpMax));

    public float Stamina { get; private set; }
    public float StaminaMax => stamina;
    public float Stamina01 => Mathf.Clamp01(Stamina / Mathf.Max(1f, stamina));

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public float StaminaLossOnHitMul => staminaLossOnHitMul;

    [Header("Heal (R to Cast)")]
    [SerializeField] private float healDuration = 0.5f;  // 캐스팅 시간(초)
    [SerializeField] private float healAmount = 30f;     // 회복량(HP)
    [SerializeField] private bool lockPhysicsOnHeal = true; // 캐스팅 동안 완전정지 여부

    private bool isHealing = false;
    public bool IsHealing => isHealing;
    private Coroutine healCo;
    private PlayerMove inputWrapper;
    private InputAction healAction;

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

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // === 공개 Getter ===
    public bool DebugLogs => debugLogs;
    public bool InHitstun => hit && hit.InHitstun;
    public bool IsParryLocked => defense != null && defense.IsParryLocked;
    public bool IsStaminaBroken => defense != null && defense.IsStaminaBroken;

    public PlayerMoveBehaviour MoveRef => moveRef;
    public Animator Anim => animator;
    public Rigidbody2D RB => rb;
    public bool IsInCombat => inCombat;
    public float CombatYSpeedMul => combatYSpeedMultiplier;
    public LayerMask EnemyMask => enemyMask;

    public bool IgnoreEnemyCollisionDuringActive => true;
    public float ExtraIgnoreTime => 0.02f;

    private void Reset()
    {
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!attack) attack = GetComponent<PlayerAttack>();
        if (!hit) hit = GetComponent<PlayerHit>();
    }

    private void Awake()
    {
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!attack) attack = GetComponent<PlayerAttack>();
        if (!hit) hit = GetComponent<PlayerHit>();
        inputWrapper = new PlayerMove();

        hp = hpMax;
        Stamina = stamina;
        OnHealthChanged?.Invoke(hp, hpMax);
        OnStaminaChanged?.Invoke(Stamina, stamina);

        if (defense) defense.Bind(this, moveRef, animator);
        if (attack) attack.Bind(this, moveRef, animator);
    }

    private void OnEnable()
    {
        StartCoroutine(VitalsTick());

        // ★ Heal 액션 바인딩 (Input Actions에 "Heal" 추가 필요. 예: <Keyboard>/r)
        inputWrapper.Enable();
        healAction = inputWrapper.asset.FindAction("Heal");
        if (healAction != null)
            healAction.started += OnHealStarted;
        else
            Debug.LogWarning("[PlayerCombat] 'Heal' 액션이 없습니다. .inputactions에 추가하세요 (예: R키).");
    }
    private void OnDisable()
    {
        StopAllCoroutines();

        if (healAction != null)
            healAction.started -= OnHealStarted;
        inputWrapper.Disable();
    }
    private void OnHealStarted(InputAction.CallbackContext _)
    {
        // 이미 힐 중 / 히트스턴 / 가드브레이크 상태면 힐 불가
        if (isHealing || (hit != null && hit.InHitstun) || (defense != null && defense.IsStaminaBroken))
            return;

        // “완전 무방비” 요구면 가드 해제(공격은 아래 코루틴에서 컴포넌트 비활성으로 막음)
        defense?.ForceUnblock();

        if (healCo != null) StopCoroutine(healCo);
        healCo = StartCoroutine(HealRoutine());
    }

    private IEnumerator HealRoutine()
    {
        isHealing = true;

        // 이동 잠금(원하면 완전 고정)
        if (lockPhysicsOnHeal) moveRef?.SetMovementLocked(true, hardFreezePhysics: true, zeroVelocity: true);
        else moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: true);

        // 가드/공격 입력 차단을 위해 컴포넌트 비활성
        bool defWasEnabled = defense && defense.enabled;
        bool atkWasEnabled = attack && attack.enabled;
        if (defense) defense.enabled = false;
        if (attack) attack.enabled = false;

        animator?.SetTrigger("HealStart");

        yield return new WaitForSeconds(healDuration);

        // 체력 회복
        hp = Mathf.Min(hpMax, hp + healAmount);
        OnHealthChanged?.Invoke(hp, hpMax);
        Debug.Log($"[HEAL] +{healAmount}HP => {hp}/{hpMax}");

        EndHealing();

        // 원상 복구
        if (defense) defense.enabled = defWasEnabled;
        if (attack) attack.enabled = atkWasEnabled;
    }
    private void EndHealing()
    {
        isHealing = false;
        moveRef?.SetMovementLocked(false, hardFreezePhysics: lockPhysicsOnHeal);
        healCo = null;
    }
    public void CancelHealing()
    {
        if (!isHealing) return;
        StopCoroutine(healCo);
        EndHealing();
    }
    // Combat State 제어
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
            if (defense && !defense.IsStaminaBroken && !defense.IsBlocking && Stamina < stamina)
            {
                AddStamina(staminaRegenPerSec * Time.deltaTime);
            }
            yield return wait;
        }
    }

    // 체력/스태미너 관리
    public void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Max(0f, hp - amount);
        OnHealthChanged?.Invoke(hp, hpMax);
        if (debugLogs) Debug.Log($"[HP] -{amount} (now {hp}/{hpMax})");
        if (hp <= 0f) OnDeath();
    }

    public void AddStamina(float delta)
    {
        Stamina = Mathf.Clamp(Stamina + delta, 0f, stamina);
        OnStaminaChanged?.Invoke(Stamina, stamina);

        // 스태미나가 0이 되면 확실히 가드브레이크 트리거
        if (Stamina <= 0f && defense != null && !defense.IsStaminaBroken)
        {
            defense.TriggerStaminaBreak();
        }
    }

    private void OnDeath()
    {
        if (debugLogs) Debug.Log("[Player] DEAD");
        moveRef?.SetMovementLocked(true, true);
        animator?.SetTrigger("Die");
    }
}
