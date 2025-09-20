using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMoveBehaviour : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 7.0f;
    [SerializeField] private float flipDeadzone = 0.05f;
    [SerializeField] private PlayerCombat combat;

    private PlayerMove playermoves;     // .inputactions가 생성한 래퍼
    private Vector2 movement;
    private Rigidbody2D rb;

    // Anim / Visual
    private Animator Panimator;
    private SpriteRenderer PspriteRenderer;

    // 외부에서 읽을 수 있도록 공개 (Combat이 참조)
    public Vector2 LastFacing { get; private set; } = Vector2.right;
    private int lastFacingX = 1;

    // 이동 잠금(GuardBreak 동안 사용)
    private bool movementLocked = false;
    private RigidbodyConstraints2D constraintsBeforeLock;

    private void Awake()
    {
        playermoves = new PlayerMove();
        rb = GetComponent<Rigidbody2D>();
        Panimator = GetComponent<Animator>();
        PspriteRenderer = GetComponent<SpriteRenderer>();
        if (!combat) combat = GetComponent<PlayerCombat>(); // ★ 추가
    }


    private void OnEnable() => playermoves.Enable();
    private void OnDisable() => playermoves.Disable();

    private void Update()
    {
        PlayerInput();
    }

    private void FixedUpdate()
    {
        AdjustPlayerFacingDirection();
        Move();
    }

    private void PlayerInput()
    {
        movement = playermoves.Movement.Move.ReadValue<Vector2>();

        // 움직일 때만 바라보는 방향 갱신
        if (movement.sqrMagnitude > 0.0001f)
            LastFacing = movement.normalized;

        if (Panimator != null)
        {
            Panimator.SetFloat("moveX", movement.x);
            Panimator.SetFloat("moveY", movement.y);
            Panimator.SetBool("isMoving", movement.sqrMagnitude > 0.0001f);
        }
    }

    private void Move()
    {
        if (movementLocked) return;

        float yMul = (combat != null && combat.IsInCombat) ? combat.CombatYSpeedMul : 1f;
        rb.linearVelocity = new Vector2(movement.x * moveSpeed, movement.y * moveSpeed * yMul);
    }



    private void AdjustPlayerFacingDirection()
    {
        if (PspriteRenderer == null) return;
        float x = movement.x;
        if (Mathf.Abs(x) < flipDeadzone) return;

        int dir = x > 0f ? 1 : -1;
        if (dir != lastFacingX)
        {
            // 원본 스프라이트가 오른쪽을 본다고 가정
            PspriteRenderer.flipX = (dir == -1);
            lastFacingX = dir;

            if (Panimator) Panimator.SetFloat("lastMoveX", lastFacingX);
        }
    }

    // === 외부에서 이동 잠금 토글 (GuardBreak에서 사용) ===

    public void SetMovementLocked(bool locked, bool hardFreezePhysics = true, bool zeroVelocity = true)
    {
        movementLocked = locked;

        if (locked)
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

            if (Panimator) Panimator.SetBool("isMoving", false);
        }
        else
        {
            if (hardFreezePhysics) rb.constraints = constraintsBeforeLock;
        }
    }

}
