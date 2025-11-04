using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum DefenseOutcome { None, Block, Parry }

public class PlayerDefense : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    private PlayerMove inputWrapper;
    private InputAction blockAction;

    // ---- Block State ----
    private bool isBlocking = false;
    private float blockPressedTime = -999f;
    private Coroutine forcedBlockCo;

    public bool IsBlocking => isBlocking;
    public float LastBlockPressedTime => blockPressedTime;
    public float WeavingPostHold => postHold;

    // ---- Weaving / Guard common config (기존 값 유지해도 됨) ----
    [Header("Weaving / Block (Config)")]
    [SerializeField] private float guardAngle = 120f;
    [SerializeField] private float parryWindow = 0.30f;
    [SerializeField] private float postHold = 0.10f;                // 위빙 후 추가 유지

    [SerializeField] private float blockDamageMul = 0f;              // 가드시 HP피해 배수
    [SerializeField] private float blockKnockMul = 0.3f;             // 가드시 넉백 배수
    [SerializeField, Range(0f, 1f)] private float guardSpeedMultiplier = 0.6f;

    // ---- Guard Regain System ----
    [Header("Guard Regain System")]
    [SerializeField] private float guardStartCost = 15f;      // 가드 "시작" 1회 소모량
    [SerializeField, Range(0f, 1f)] private float guardHitStaminaCostMul = 0.5f; // 가드 상태에서 맞을 때 스태미나 소모 배율(기본의 50%)
    [SerializeField, Range(0f, 1f)] private float parryRegainPercent = 0.8f;     // 위빙 성공 시, 이번 가드 중 소모량의 회복 비율(기본 80%)

    private float lastGuardStartCost = 0f;    // 직전 가드시 소모한 양(패링 환급용)
    // 이번 “가드 세션(버튼 누른~해제)” 동안 스태미나로 실제 소모된 총량을 누적
    private float guardSpentThisSession = 0f;

    // ---- Parry Lock (움직임 잠금) ----
    private float parryLockEndTime = 0f;
    private Coroutine parryLockCo;
    public bool IsParryLocked => Time.time < parryLockEndTime;
    private const string LOCK_PARRY = "PARRYLOCK";

    // 외부에서 읽도록 노출(다른 스크립트가 참조)
    public float GuardAngle => guardAngle;
    public float ParryWindow => parryWindow;
    public float PostHold => postHold;
    public float BlockDamageMul => blockDamageMul;
    public float BlockKnockMul => blockKnockMul;
    public float GuardHitStaminaCostMul => guardHitStaminaCostMul;
    public float ParryRegainPercent => parryRegainPercent;

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
    }

    private void OnEnable()
    {
        inputWrapper.Enable();
        inputWrapper.Combat.Enable();

        blockAction = inputWrapper.asset.FindAction("Block");
        if (blockAction != null)
        {
            blockAction.started += OnBlockStarted;
            blockAction.canceled += OnBlockCanceled;
        }
        else
        {
            Debug.LogWarning("[PlayerDefense] 'Block' 액션이 없습니다.");
        }
    }

    private void OnDisable()
    {
        if (blockAction != null)
        {
            blockAction.started -= OnBlockStarted;
            blockAction.canceled -= OnBlockCanceled;
        }
        inputWrapper.Disable();

        if (forcedBlockCo != null) { StopCoroutine(forcedBlockCo); forcedBlockCo = null; }
        if (parryLockCo != null) { StopCoroutine(parryLockCo); parryLockCo = null; }
        moveRef?.RemoveMovementLock(LOCK_PARRY, false);
    }

    // === Input ===
    private void OnBlockStarted(InputAction.CallbackContext _)
    {
        if (combat == null || IsStaminaBroken) return;

        isBlocking = true;
        blockPressedTime = Time.time;
        animator?.SetBool("isBlocking", true);
        moveRef?.SetGuardSpeedScale(guardSpeedMultiplier);
        // ★ 가드 "시작" 1회 소모
        lastGuardStartCost = Mathf.Max(0f, guardStartCost);
        float lockduration = ParryWindow + postHold;
        combat?.BlockStaminaRegenFor(lockduration);
        if (lastGuardStartCost > 0f)
        {
            combat.AddStamina(-lastGuardStartCost);
            if (combat.IsStaminaBroken)
            {
                StopBlocking();
                return;
            }
        }
    }

    private void OnBlockCanceled(InputAction.CallbackContext _)
    {
        // 강제 유지 중이면 버튼 떼도 끝날 때까지 유지
        if (forcedBlockCo != null) return;

        StopBlocking();
    }

    private void StopBlocking()
    {
        isBlocking = false;
        animator?.SetBool("isBlocking", false);

        moveRef?.SetGuardSpeedScale(1f);
        guardSpentThisSession = 0f;
    }

    // 강제 가드 유지(위빙 윈도우+0.1)
    private IEnumerator ForceBlockRoutine(float seconds)
    {
        float end = Time.time + Mathf.Max(0f, seconds);
        isBlocking = true;
        animator?.SetBool("isBlocking", true);
        while (Time.time < end)
            yield return null;

        forcedBlockCo = null;

        bool stillPressed = blockAction != null && blockAction.IsPressed();
        if (!stillPressed)
            StopBlocking();
    }

    public void OnWeavingSuccessRegain()
    {
        if (lastGuardStartCost > 0f)
            combat.AddStamina((10 + lastGuardStartCost) * parryRegainPercent);
    }

    public void RegisterGuardHitStaminaCost(float amount)
    {
        if (amount > 0f) guardSpentThisSession += amount;
    }

    public void RegainStaminaOnParry()
    {
        if (combat == null || guardSpentThisSession <= 0f || parryRegainPercent <= 0f) return;
        float regain = (10 + guardSpentThisSession) * parryRegainPercent;
        combat.AddStamina(-regain);
        guardSpentThisSession = 0f;
    }

    public DefenseOutcome Evaluate(Vector2 facing, Vector2 inFrontToEnemy, bool parryable)
    {
        if (!isBlocking || combat == null || IsStaminaBroken) return DefenseOutcome.None;

        // 정면 콘 판정
        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing, inFrontToEnemy) >= cosHalf;
        if (!inFront) return DefenseOutcome.None;

        bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;
        return canParry ? DefenseOutcome.Parry : DefenseOutcome.Block;
    }

    // === Parry Lock ===
    public void StartParryLock(float duration, bool zeroVelocityOnStart = true)
    {
        if (duration <= 0f) return;
        parryLockEndTime = Mathf.Max(parryLockEndTime, Time.time + duration);
        if (parryLockCo == null) parryLockCo = StartCoroutine(ParryLockRoutine());

        moveRef?.AddMovementLock(LOCK_PARRY, hardFreezePhysics: false, zeroVelocity: zeroVelocityOnStart);
    }

    private IEnumerator ParryLockRoutine()
    {
        while (Time.time < parryLockEndTime) yield return null;
        moveRef?.RemoveMovementLock(LOCK_PARRY, hardFreezePhysics: false);
        parryLockCo = null;
    }

    public void ForceBlockFor(float seconds)
    {
        if (forcedBlockCo != null) StopCoroutine(forcedBlockCo);
        forcedBlockCo = StartCoroutine(ForceBlockRoutine(seconds));
    }

    // === Stamina Break ===
    public bool IsStaminaBroken => combat != null && combat.IsStaminaBroken;

    public void TriggerStaminaBreak()
    {
        // 가드 강제 해제 및 이동/애니 처리
        StopBlocking();
        combat?.OnStaminaBreak();
    }
}
