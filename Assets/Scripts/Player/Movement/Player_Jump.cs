using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Jump : MonoBehaviour
{
    [Header("Duration & Cooldown")]
    [SerializeField, Min(0f)] private float jumpDuration = 0.35f;
    [SerializeField, Min(0f)] private float cooldown = 0.35f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string jumpTriggerName = "Jump"; // 애니메이터 트리거명

    [Header("Obstacle Bypass (optional)")]
    [Tooltip("점프 중에만 충돌을 무시할 레이어들(예: LowObstacle, Trap 등)")]
    [SerializeField] private string[] ignoreLayersDuringJump;

    [Header("I-Frame (optional)")]
    [SerializeField, Min(0f)] private float iFrameDuration = -1f; // <= 0 이면 jumpDuration 사용

    // 입력
    [Header("Input")]
    [SerializeField] private string actionName = "Jump"; // Input Actions 내 액션명(없으면 TryJump()를 직접 호출)
    private PlayerMove inputWrapper;
    private InputAction jumpAction;

    // 외부 연동(선택)
    private PlayerHit hit;

    // 상태
    public bool IsJumping { get; private set; }
    private bool _cooling;

    // 태그 이벤트(카메라 FX 등에서 사용 가능)
    public const string TAG_JUMP_START = "Tag.Player.Jump.Start";
    public const string TAG_JUMP_END = "Tag.Player.Jump.End";
    public event System.Action<string> OnTag;

    // 내부
    private int _playerLayer;
    private int[] _ignoreLayerIdx;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        hit = GetComponent<PlayerHit>();

        _playerLayer = gameObject.layer;
        if (ignoreLayersDuringJump != null && ignoreLayersDuringJump.Length > 0)
        {
            _ignoreLayerIdx = new int[ignoreLayersDuringJump.Length];
            for (int i = 0; i < ignoreLayersDuringJump.Length; i++)
                _ignoreLayerIdx[i] = LayerMask.NameToLayer(ignoreLayersDuringJump[i]);
        }

        inputWrapper = new PlayerMove();
    }

    private void OnEnable()
    {
        inputWrapper.Enable();
        jumpAction = inputWrapper.asset.FindAction(actionName);
        if (jumpAction != null) jumpAction.started += OnJumpStarted;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.started -= OnJumpStarted;
        inputWrapper.Disable();
    }

    private void OnJumpStarted(InputAction.CallbackContext _)
    {
        TryJump();
    }

    public void TryJump()
    {
        if (IsJumping || _cooling) return;
        StartCoroutine(JumpRoutine());
    }

    private IEnumerator JumpRoutine()
    {
        IsJumping = true;
        _cooling = true;

        // 애니메이션
        if (animator && !string.IsNullOrEmpty(jumpTriggerName))
            animator.SetTrigger(jumpTriggerName);

        // 태그: 시작(전역 버스도 사용 중이면 같이 올림)
        OnTag?.Invoke(TAG_JUMP_START);
        TagBus.Raise(TAG_JUMP_START);

        // 충돌 무시(선택)
        SetIgnoreLayers(true);

        // 지속
        yield return new WaitForSeconds(jumpDuration);

        // 원복
        SetIgnoreLayers(false);

        IsJumping = false;

        // 태그: 종료
        OnTag?.Invoke(TAG_JUMP_END);
        TagBus.Raise(TAG_JUMP_END);

        // 쿨다운
        if (cooldown > 0f) yield return new WaitForSeconds(cooldown);
        _cooling = false;
    }

    private void SetIgnoreLayers(bool on)
    {
        if (_ignoreLayerIdx == null || _ignoreLayerIdx.Length == 0) return;
        for (int i = 0; i < _ignoreLayerIdx.Length; i++)
        {
            int other = _ignoreLayerIdx[i];
            if (other < 0) continue; // 존재하지 않는 레이어명 방지
            Physics2D.IgnoreLayerCollision(_playerLayer, other, on);
        }
    }
}
