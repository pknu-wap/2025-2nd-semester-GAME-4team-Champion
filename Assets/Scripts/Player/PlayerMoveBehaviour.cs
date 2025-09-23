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

    public Vector2 CurrentInput => movement;
    private PlayerMove playermoves;
    private Vector2 movement;
    private Rigidbody2D rb;

    [Header("Facing Control")]
    [SerializeField] private bool blockFlipFromMovement = false; // 이동 입력으로 인한 X플립 차단

    public bool IsFlipFromMovementBlocked => blockFlipFromMovement;
    public void SetFlipFromMovementBlocked(bool blocked) => blockFlipFromMovement = blocked;

    private Animator Panimator;
    private SpriteRenderer PspriteRenderer;

    public Vector2 LastFacing { get; private set; } = Vector2.right;
    private int lastFacingX = 1;

    // 이동 잠금
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

        if (!blockFlipFromMovement && movement.sqrMagnitude > 0.0001f)
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
        if (blockFlipFromMovement) return;

        if (PspriteRenderer == null) return;
        float x = movement.x;
        if (Mathf.Abs(x) < flipDeadzone) return;

        int dir = x > 0f ? 1 : -1;
        if (dir != lastFacingX)
        {
            PspriteRenderer.flipX = (dir == -1);
            lastFacingX = dir;
        }
    }

    // === 외부에서 이동 잠금 토글 ===
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

        }
        else
        {
            if (hardFreezePhysics) rb.constraints = constraintsBeforeLock;
        }
    }

    public void FaceTargetX(float targetWorldX)
    {
        int dir = (targetWorldX >= transform.position.x) ? 1 : -1;

        if (PspriteRenderer) PspriteRenderer.flipX = (dir == -1);

        lastFacingX = dir;
        LastFacing = new Vector2(dir, 0f);

        if (Panimator)
        {
            Panimator.SetFloat("moveX", 0f);
            Panimator.SetFloat("moveY", 0f);
        }
    }
}
