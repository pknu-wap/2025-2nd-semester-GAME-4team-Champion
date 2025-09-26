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
    [SerializeField] private float guardAngle = 120f;      // ���� �� ����
    [SerializeField] private float parryWindow = 0.3f;     // �и� ������
    [SerializeField] private float blockDamageMul = 0f;    // ����� ������ ���
    [SerializeField] private float blockKnockMul = 0.3f;   // ����� �˹� ���
    [SerializeField] private float weavingPostHold = 0.10f;// �и� �� ���� ����/��� �߰� �ð�
    [SerializeField] private float staminaBreakTime = 1.5f;// ���� �극��ũ ����
    [SerializeField] private float postHold = 0.10f;           // �߰� ���� ���� �ð�
    [SerializeField, Range(0f, 1.5f)] private float guardSpeedMultiplier = 0.6f;

    [Header("Block Animation Restart (Safe)")]
    [SerializeField] private string blockStartStateName = "Block"; // ��� ���� ������Ʈ �̸�
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

        // ���� ����
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

        // ��� ���� ����
        isBlocking = true;
        blockPressedTime = now;
        animator?.SetBool("isBlocking", true);

        if (animator && !string.IsNullOrEmpty(blockStartStateName) && IsSafeToRestartBlockAnim())
        {
            // ��� �����(��Ÿ ����) ����/��Ʈ/���� ����� ȣ����� ����
        }
        // else: ����/��Ʈ/���� ���̸� ���� �ǵ��� ����(=ĵ�� ����)

        // (Weaving���� + 0.1) ���� �̵� ���� ���� + ����� �� Ÿ�̸� ����
        float lockDuration = Mathf.Max(0f, parryWindow + postHold);
        blockPressedTime = now + lockDuration;

        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: true);

        // ���� �� �̵� ����
        moveRef?.SetGuardSpeedScale(guardSpeedMultiplier);

        // ���� ���� ����(������ ��ư ���·� ����/����)
        if (forcedBlockCo != null) StopCoroutine(forcedBlockCo);
        forcedBlockCo = StartCoroutine(ForceBlockRoutine(lockDuration));
    }



    private void OnBlockCanceled(InputAction.CallbackContext _)
    {
        // ���� ���� �ð� ������ �������� ����
        if (forcedBlockCo != null) return;

        isBlocking = false;
        animator?.SetBool("isBlocking", false);

        moveRef?.SetGuardSpeedScale(1f);
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
    }

    // === �ܺο��� ���� ���� ===
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

        // ������ ���� ����
        isBlocking = true;
        animator?.SetBool("isBlocking", true);

        // ������ �ð� ���� ���
        while (Time.time < end) yield return null;

        forcedBlockCo = null;

        // �ð��� ������ ��, ��ư�� ������ ���������� �� ���� ���, �̵����� ����
        bool stillPressed = blockAction != null && blockAction.IsPressed();
        if (stillPressed)
        {
            moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
            // ������ ���� ���̹Ƿ� ����(��ư �� �� ������)
        }
        else
        {
            // ��ư�� �� ���� �� ���� & ���� ��� ����
            isBlocking = false;
            animator?.SetBool("isBlocking", false);
            moveRef?.SetGuardSpeedScale(1f);
            moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        }
    }


    public DefenseOutcome Evaluate(Vector2 facing, Vector2 dirToEnemy/*=�÷��̾����*/, bool parryable)
    {
        if (!isBlocking || IsStaminaBroken) return DefenseOutcome.None;

        float cosHalf = Mathf.Cos(guardAngle * 0.5f * Mathf.Deg2Rad);
        bool inFront = Vector2.Dot(facing, dirToEnemy.normalized) >= cosHalf;
        if (!inFront) return DefenseOutcome.None;

        bool canParry = parryable && (Time.time - blockPressedTime) <= parryWindow;
        return canParry ? DefenseOutcome.Parry : DefenseOutcome.Block;
    }



    // === �и� ���� �� ���� ���(�ӵ� 0 �ɼ�) ===
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

    // === ���¹̳� �극��ũ ó�� ===
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

        // ���̽� ���̾� ����(�ʿ�� �ٸ� ���̾� �ε��� ����)
        int layer = 0;

        // ���� ���̸� �ǵ��� �ʱ�
        if (animator.IsInTransition(layer)) return false;

        var st = animator.GetCurrentAnimatorStateInfo(layer);

        //  �±� ȭ��Ʈ����Ʈ
        if (safeStartTags != null)
        {
            foreach (var t in safeStartTags)
            {
                if (!string.IsNullOrEmpty(t) && st.IsTag(t))
                    return true;
            }
        }


        // �� ���ǿ� �� �ɸ��� �������� ����(=���� ����� ����)
        return false;
    }


    private void Start()
    {
        moveRef?.SetGuardSpeedScale(1f);
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
    }
}