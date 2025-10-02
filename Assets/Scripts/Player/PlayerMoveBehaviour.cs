using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMoveBehaviour : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float flipDeadzone = 0.05f;
    [SerializeField] private PlayerCombat combat;

    private PlayerMove inputWrapper;   // .inputactions 자동 생성 래퍼
    private Vector2 movement;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sprite;

    // 외부에서 읽기/사용
    public Vector2 LastFacing { get; private set; } = Vector2.right;
    public Vector2 CurrentInput => movement;

    // 이동 잠금 & 가드시 감속
    private bool movementLocked = false;
    private RigidbodyConstraints2D constraintsBeforeLock;
    private float guardSpeedScale = 1f;
    private readonly HashSet<string> moveLocks = new HashSet<string>();
    private const string LegacyLock = "__LEGACY__";
    private bool IsMovementLocked => moveLocks.Count > 0;

    // (선택) 이동으로 X-플립 막기용
    private bool flipFromMovementBlocked = false;

    private void Awake()
    {
        inputWrapper = new PlayerMove();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        if (!combat) combat = GetComponent<PlayerCombat>();
    }

    private void OnEnable() => inputWrapper.Enable();
    private void OnDisable() => inputWrapper.Disable();

    private void Update()
    {
        movement = inputWrapper.Movement.Move.ReadValue<Vector2>();

        if (!movementLocked && movement.sqrMagnitude > 0.0001f)
            LastFacing = movement.normalized;

        if (animator)
        {
            animator.SetFloat("moveX", movement.x);
            animator.SetFloat("moveY", movement.y);
            animator.SetBool("isMoving", movement.sqrMagnitude > 0.0001f);
        }
    }

    private void FixedUpdate()
    {
        AdjustFlipByX();
        Move();
    }
    private void Move()
    {
        if (IsMovementLocked) return;

        // 전투 중이면 Y축만 배수 적용 (기존 로직 그대로)
        float yMul = (combat != null && combat.IsInCombat) ? combat.CombatYSpeedMul : 1f;
        float vx = movement.x * moveSpeed * guardSpeedScale;
        float vy = movement.y * moveSpeed * guardSpeedScale * yMul;
        rb.linearVelocity = new Vector2(vx, vy);
    }


    private void AdjustFlipByX()
    {
        if (!sprite || flipFromMovementBlocked) return;

        float x = movement.x;
        if (Mathf.Abs(x) < flipDeadzone) return;

        // 원본이 오른쪽을 보는 스프라이트라고 가정
        sprite.flipX = (x < 0f);
    }

    // ===== 외부 제어 API =====
    // 추가: 키 기반 잠금 API (기존 SetMovementLocked도 함께 유지)
    public void AddMovementLock(string key, bool hardFreezePhysics = false, bool zeroVelocity = true)
    {
        if (string.IsNullOrEmpty(key)) key = "__ANON__";
        if (moveLocks.Add(key))
        {
            if (zeroVelocity)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            if (hardFreezePhysics)
            {
                constraintsBeforeLock = rb.constraints;
                var keepRot = constraintsBeforeLock & RigidbodyConstraints2D.FreezeRotation;
                rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY | keepRot;
            }
            if (animator) animator.SetBool("isMoving", false);
        }
    }

    public void RemoveMovementLock(string key, bool hardFreezePhysics = false)
    {
        if (string.IsNullOrEmpty(key)) key = "__ANON__";
        if (moveLocks.Remove(key))
        {
            if (hardFreezePhysics && moveLocks.Count == 0)
                rb.constraints = constraintsBeforeLock;
        }
    }

    // 변경: 기존 API는 레거시 키로 래핑하여 하위호환 유지
    public void SetMovementLocked(bool locked, bool hardFreezePhysics = true, bool zeroVelocity = true)
    {
        if (locked) AddMovementLock(LegacyLock, hardFreezePhysics, zeroVelocity);
        else RemoveMovementLock(LegacyLock, hardFreezePhysics);
    }


    public void SetGuardSpeedScale(float scale) => guardSpeedScale = Mathf.Max(0f, scale);

    // (선택) 다른 데서 호출하려면
    public void SetFlipFromMovementBlocked(bool blocked) => flipFromMovementBlocked = blocked;
    public void FaceTargetX(float targetX)
    {
        if (!sprite) return;
        bool left = targetX < transform.position.x;
        sprite.flipX = left;
        LastFacing = new Vector2(left ? -1f : 1f, 0f);
    }
}
