using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum DefenseOutcome { None, Block, Parry }

public class PlayerDefense : MonoBehaviour
{
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    private PlayerMove inputWrapper;
    private InputAction blockAction;

    private bool isBlocking = false;
    private float blockPressedTime = -999f;
    private Coroutine forcedBlockCo;

    public bool IsBlocking => isBlocking;
    public float LastBlockPressedTime => blockPressedTime;

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
    }

    private void OnBlockStarted(InputAction.CallbackContext _)
    {
        if (combat.IsStaminaBroken) return; // 브레이크 중 가드 불가
        isBlocking = true;
        blockPressedTime = Time.time;
        animator?.SetBool("isBlocking", true);
    }

    private void OnBlockCanceled(InputAction.CallbackContext _)
    {
        isBlocking = false;
        animator?.SetBool("isBlocking", false);
    }

    // 가드 브레이크 등으로 강제 해제
    public void ForceUnblock()
    {
        if (forcedBlockCo != null) { StopCoroutine(forcedBlockCo); forcedBlockCo = null; }
        isBlocking = false;
        animator?.SetBool("isBlocking", false);
    }

    // 패링 후 0.1초 동안 가드 유지
    public void ForceBlockFor(float seconds)
    {
        if (forcedBlockCo != null) StopCoroutine(forcedBlockCo);
        forcedBlockCo = StartCoroutine(ForceBlockRoutine(seconds));
    }

    private IEnumerator ForceBlockRoutine(float seconds)
    {
        float end = Time.time + Mathf.Max(0f, seconds);
        isBlocking = true;
        animator?.SetBool("isBlocking", true);

        while (Time.time < end)
            yield return null;

        bool stillPressed = blockAction != null && blockAction.IsPressed();
        if (!stillPressed)
        {
            isBlocking = false;
            animator?.SetBool("isBlocking", false);
        }
        forcedBlockCo = null;
    }

    /// <summary> PlayerCombat.OnHit에서 호출: 현재 상황으로 가드/패링/실패 판정 </summary>
    public DefenseOutcome Evaluate(bool inFront, bool parryable)
    {
        if (!isBlocking || combat.IsStaminaBroken) return DefenseOutcome.None;
        if (!inFront) return DefenseOutcome.None;

        bool canParry = parryable && (Time.time - blockPressedTime) <= combat.ParryWindow;
        return canParry ? DefenseOutcome.Parry : DefenseOutcome.Block;
    }
}
