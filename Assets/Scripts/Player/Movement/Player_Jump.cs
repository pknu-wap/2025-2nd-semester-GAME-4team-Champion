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
    [SerializeField] private string jumpTriggerName = "Jump"; // �ִϸ����� Ʈ���Ÿ�

    [Header("Obstacle Bypass (optional)")]
    [Tooltip("���� �߿��� �浹�� ������ ���̾��(��: LowObstacle, Trap ��)")]
    [SerializeField] private string[] ignoreLayersDuringJump;

    [Header("I-Frame (optional)")]
    [SerializeField, Min(0f)] private float iFrameDuration = -1f; // <= 0 �̸� jumpDuration ���

    // �Է�
    [Header("Input")]
    [SerializeField] private string actionName = "Jump"; // Input Actions �� �׼Ǹ�(������ TryJump()�� ���� ȣ��)
    private PlayerMove inputWrapper;
    private InputAction jumpAction;

    // �ܺ� ����(����)
    private PlayerHit hit;

    // ����
    public bool IsJumping { get; private set; }
    private bool _cooling;

    // �±� �̺�Ʈ(ī�޶� FX ��� ��� ����)
    public const string TAG_JUMP_START = "Tag.Player.Jump.Start";
    public const string TAG_JUMP_END = "Tag.Player.Jump.End";
    public event System.Action<string> OnTag;

    // ����
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

        // �ִϸ��̼�
        if (animator && !string.IsNullOrEmpty(jumpTriggerName))
            animator.SetTrigger(jumpTriggerName);

        // �±�: ����(���� ������ ��� ���̸� ���� �ø�)
        OnTag?.Invoke(TAG_JUMP_START);
        TagBus.Raise(TAG_JUMP_START);

        // �浹 ����(����)
        SetIgnoreLayers(true);

        // ����
        yield return new WaitForSeconds(jumpDuration);

        // ����
        SetIgnoreLayers(false);

        IsJumping = false;

        // �±�: ����
        OnTag?.Invoke(TAG_JUMP_END);
        TagBus.Raise(TAG_JUMP_END);

        // ��ٿ�
        if (cooldown > 0f) yield return new WaitForSeconds(cooldown);
        _cooling = false;
    }

    private void SetIgnoreLayers(bool on)
    {
        if (_ignoreLayerIdx == null || _ignoreLayerIdx.Length == 0) return;
        for (int i = 0; i < _ignoreLayerIdx.Length; i++)
        {
            int other = _ignoreLayerIdx[i];
            if (other < 0) continue; // �������� �ʴ� ���̾�� ����
            Physics2D.IgnoreLayerCollision(_playerLayer, other, on);
        }
    }
}
