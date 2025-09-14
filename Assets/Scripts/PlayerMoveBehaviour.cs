using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMoveBehaviour : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 7.0f;
    [SerializeField] private float flipDeadzone = 0.05f;

    private PlayerMove playermoves;
    private Vector2 movement;
    private Rigidbody2D rb;

    // Player Animation / Visual
    private Animator Panimator;
    private SpriteRenderer PspriteRenderer;

    private bool movementLocked = false;
    private RigidbodyConstraints2D constraintsBeforeLock;


    // �ܺο��� ���� �� �ֵ��� ���� (Combat�� ����)
    public Vector2 LastFacing { get; private set; } = Vector2.right;
    private int lastFacingX = 1;

    private void Awake()
    {
        playermoves = new PlayerMove();
        rb = GetComponent<Rigidbody2D>();
        Panimator = GetComponent<Animator>();
        PspriteRenderer = GetComponent<SpriteRenderer>();
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

        // ������ ���� �ٶ󺸴� ���� ����
        if (movement.sqrMagnitude > 0.0001f)
            LastFacing = movement.normalized;

        if (Panimator != null)
        {
            Panimator.SetFloat("moveX", movement.x);
            Panimator.SetFloat("moveY", movement.y);
            Panimator.SetBool("isMoving", movement.sqrMagnitude > 0.0001f);
        }
    }
    public void SetMovementLocked(bool locked, bool hardFreezePhysics = true)
    {
        movementLocked = locked;

        if (locked)
        {
            // �Է�/�̵� ���� ���� + ������ ����
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (hardFreezePhysics)
            {
                // ���� ���� ���� �� ��ġ ����(ȸ�� ������ ���� �� ����)
                constraintsBeforeLock = rb.constraints;
                var keepRotation = constraintsBeforeLock & RigidbodyConstraints2D.FreezeRotation;
                rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY | keepRotation;
            }

            if (Panimator) Panimator.SetBool("isMoving", false);
        }
        else
        {
            // ���� �������� ����
            if (hardFreezePhysics) rb.constraints = constraintsBeforeLock;
        }
    }
    private void Move()
    {
        if (movementLocked) return;                 // �� ��� �� �÷��̾� �̵� ���� ����
        rb.MovePosition(rb.position + movement * (moveSpeed * Time.fixedDeltaTime));
    }


    private void AdjustPlayerFacingDirection()
    {
        if (PspriteRenderer == null) return;
        float x = movement.x;
        if (Mathf.Abs(x) < flipDeadzone) return;

        int dir = x > 0f ? 1 : -1;
        if (dir != lastFacingX)
        {
            // ���� ��������Ʈ�� �������� ���ٰ� ����
            PspriteRenderer.flipX = (dir == -1);
            lastFacingX = dir;

            if (Panimator) Panimator.SetFloat("lastMoveX", lastFacingX);
        }
    }
}
