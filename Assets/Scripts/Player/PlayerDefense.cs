using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum DefenseOutcome { None, Block, Parry }

public class PlayerDefense : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    // ===== Guard / Weaving Config =====
    [Header("Guard / Weaving (Config)")]
    [SerializeField] private float guardAngle = 120f;      // 정면 콘 각도
    [SerializeField] private float parryWindow = 0.3f;     // 패링 윈도우
    [SerializeField] private float blockDamageMul = 0f;    // 가드시 데미지 배수
    [SerializeField] private float blockKnockMul = 0.3f;   // 가드시 넉백 배수
    [SerializeField] private float weavingPostHold = 0.10f;// 패링 후 가드 유지/잠금 추가 시간
    [SerializeField] private float staminaBreakTime = 1.5f;// 가드 브레이크 지속
    [SerializeField] private float postHold = 0.10f;           // 추가 강제 가드 시간
    [SerializeField, Range(0f, 1.5f)] private float guardSpeedMultiplier = 0.6f;

    [Header("Block Animation Restart (Safe)")]
    [SerializeField] private string blockStartStateName = "Block"; // 블록 시작 스테이트 이름
    [SerializeField] private string[] safeStartTags = new[] { "Block" };

    // ==== States ====
    private bool isBlocking = false;
    private float blockPressedTime = -999f;

    // Parry Lock
    private float parryLockEndTime = 0f;
    private Coroutine parryLockCo;

    // Stamina Break
    private bool staminaBroken = false;
    private float staminaBreakEndTime = 0f;
    private Coroutine breakCo;

    // Input
    private PlayerMove inputWrapper;
    private InputAction blockAction;
    private Coroutine forcedBlockCo;

    // === Public getters ===
    public bool IsBlocking => isBlocking;
    public bool IsParryLocked => Time.time < parryLockEndTime;
    public bool IsStaminaBroken => staminaBroken;
    public float LastBlockPressedTime => blockPressedTime;

    public float GuardAngle => guardAngle;
    public float ParryWindow => parryWindow;
    public float BlockDamageMul => blockDamageMul;
    public float BlockKnockMul => blockKnockMul;
    public float WeavingPostHold => weavingPostHold;
    public float StaminaBreakTime => staminaBreakTime;

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
        if (inputWrapper == null) inputWrapper = new PlayerMove();
        inputWrapper.Enable();

        blockAction = inputWrapper.asset.FindAction("Block");
        if (blockAction != null)
        {
            blockAction.started += OnBlockStarted;
            blockAction.canceled += OnBlockCanceled;
        }
    }

    private void OnDisable()
    {
        if (blockAction != null)
        {
            blockAction.started -= OnBlockStarted;
            blockAction.canceled -= OnBlockCanceled;
        }
        if (inputWrapper != null) inputWrapper.Disable();

        if (forcedBlockCo != null) { StopCoroutine(forcedBlockCo); forcedBlockCo = null; }

        // 안전 복구
        moveRef?.SetGuardSpeedScale(1f);
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
    }

    private void OnBlockStarted(InputAction.CallbackContext _)
    {
        float now = Time.time;

        if (now < blockPressedTime)
        {
            isBlocking = true;
            animator?.SetBool("isBlocking", true);
            return;
        }

        // 평소 시작 로직
        isBlocking = true;
        blockPressedTime = now;
        animator?.SetBool("isBlocking", true);

        if (animator && !string.IsNullOrEmpty(blockStartStateName) && IsSafeToRestartBlockAnim())
        {
            // 즉시 재시작(연타 대응) 공격/히트/위빙 등에서는 호출되지 않음
        }
        // else: 공격/히트/위빙 중이면 절대 건들지 않음(=캔슬 없음)

        // (Weaving윈도 + 0.1) 동안 이동 완전 정지 + 재시작 락 타이머 설정
        float lockDuration = Mathf.Max(0f, parryWindow + postHold);
        blockPressedTime = now + lockDuration;

        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: true);

        // 가드 중 이동 감속
        moveRef?.SetGuardSpeedScale(guardSpeedMultiplier);

        // 강제 가드 유지(끝나면 버튼 상태로 유지/해제)
        if (forcedBlockCo != null) StopCoroutine(forcedBlockCo);
        forcedBlockCo = StartCoroutine(ForceBlockRoutine(lockDuration));
    }



    private void OnBlockCanceled(InputAction.CallbackContext _)
    {
        // 강제 유지 시간 동안은 해제하지 않음
        if (forcedBlockCo != null) return;

        isBlocking = false;
        animator?.SetBool("isBlocking", false);

        moveRef?.SetGuardSpeedScale(1f);
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
    }

    // === 외부에서 강제 제어 ===
    public void ForceUnblock()
    {
        if (forcedBlockCo != null) { StopCoroutine(forcedBlockCo); forcedBlockCo = null; }
        isBlocking = false;
        animator?.SetBool("isBlocking", false);
    }

    public void ForceBlockFor(float seconds)
    {
        if (forcedBlockCo != null) StopCoroutine(forcedBlockCo);
        forcedBlockCo = StartCoroutine(ForceBlockRoutine(seconds));
    }

    private IEnumerator ForceBlockRoutine(float seconds)
    {
        float end = Time.time + Mathf.Max(0f, seconds);

        // 강제로 가드 유지
        isBlocking = true;
        animator?.SetBool("isBlocking", true);

        // 정해진 시간 동안 대기
        while (Time.time < end) yield return null;

        forcedBlockCo = null;

        // 시간이 끝났을 때, 버튼이 여전히 눌려있으면 → 가드 계속, 이동락만 해제
        bool stillPressed = blockAction != null && blockAction.IsPressed();
        if (stillPressed)
        {
            moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
            // 감속은 가드 중이므로 유지(버튼 뗄 때 해제됨)
        }
        else
        {
            // 버튼도 안 눌림 → 가드 & 감속 모두 해제
            isBlocking = false;
            animator?.SetBool("isBlocking", false);
            moveRef?.SetGuardSpeedScale(1f);
            moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        }
    }


    public DefenseOutcome Evaluate(Vector2 facing, Vector2 dirToEnemy/*=플레이어→적*/, bool parryable)
    {
        if (!isBlocking || IsStaminaBroken) return DefenseOutcome.None;

        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing, dirToEnemy.normalized) >= cosHalf;
        if (!inFront) return DefenseOutcome.None;

        bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;
        return canParry ? DefenseOutcome.Parry : DefenseOutcome.Block;
    }



    // === 패링 성공 후 조작 잠금(속도 0 옵션) ===
    public void StartParryLock(float duration, bool zeroVelocityOnStart = true)
    {
        if (duration <= 0f) return;
        parryLockEndTime = Mathf.Max(parryLockEndTime, Time.time + duration);
        if (parryLockCo == null) parryLockCo = StartCoroutine(ParryLockRoutine(zeroVelocityOnStart));
    }

    private IEnumerator ParryLockRoutine(bool zeroVelocityOnStart)
    {
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: zeroVelocityOnStart);
        while (Time.time < parryLockEndTime) yield return null;
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        parryLockCo = null;
    }

    // === 스태미너 브레이크 처리 ===
    public void TriggerStaminaBreak()
    {
        staminaBroken = true;
        staminaBreakEndTime = Time.time + staminaBreakTime;

        ForceUnblock();
        moveRef?.SetMovementLocked(true, true);
        animator?.SetBool("GuardBroken", true);
        animator?.SetTrigger("GuardBreak");

        if (breakCo != null) StopCoroutine(breakCo);
        breakCo = StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        while (Time.time < staminaBreakEndTime) yield return null;

        staminaBroken = false;
        moveRef?.SetMovementLocked(false, true);
        animator?.SetBool("GuardBroken", false);
        breakCo = null;
    }
    private bool IsSafeToRestartBlockAnim()
    {
        if (animator == null) return false;

        // 베이스 레이어 기준(필요시 다른 레이어 인덱스 지정)
        int layer = 0;

        // 전이 중이면 건들지 않기
        if (animator.IsInTransition(layer)) return false;

        var st = animator.GetCurrentAnimatorStateInfo(layer);

        //  태그 화이트리스트
        if (safeStartTags != null)
        {
            foreach (var t in safeStartTags)
            {
                if (!string.IsNullOrEmpty(t) && st.IsTag(t))
                    return true;
            }
        }


        // 위 조건에 안 걸리면 안전하지 않음(=절대 재시작 금지)
        return false;
    }


    private void Start()
    {
        moveRef?.SetGuardSpeedScale(1f);
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
    }
}