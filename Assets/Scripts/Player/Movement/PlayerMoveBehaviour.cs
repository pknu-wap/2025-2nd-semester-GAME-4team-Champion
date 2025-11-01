﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMoveBehaviour : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 7f;
    private float flipDeadzone = 0.05f;
    [SerializeField] private PlayerCombat combat;

    private PlayerMove inputWrapper;
    private Vector2 movement;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sprite;

    // 외부에서 읽기/사용
    public Vector2 LastFacing { get; private set; } = Vector2.right;
    public Vector2 CurrentInput => movement;

    // 이동 잠금 & 가드시 감속
    private RigidbodyConstraints2D constraintsBeforeLock;
    private float guardSpeedScale = 1f;
    [SerializeField] public Collider2D MovementArea;

    // 다중 락 관리(키 기반)
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
        constraintsBeforeLock = rb.constraints; // 초기값 저장
    }

    private void OnEnable() => inputWrapper.Enable();
    private void OnDisable() => inputWrapper.Disable();

    private void Update()
    {
        rb.position = ClampInside(rb.position);

        movement = inputWrapper.Movement.Move.ReadValue<Vector2>();

        if (!IsMovementLocked && movement.sqrMagnitude > 0.0001f)
            LastFacing = movement.normalized;

        if (animator)
        {
            animator.SetFloat("moveX", movement.x);
            animator.SetFloat("moveY", movement.y);
            animator.SetBool("isMoving", movement.sqrMagnitude > 0.0001f);
        }
    }

    public Vector2 ClampInside(Vector2 p)
    {
        if (MovementArea == null)
            return p;
        if (MovementArea.OverlapPoint(p))
            return p;

        Vector2 closest = MovementArea.ClosestPoint(p);
        Vector2 center = MovementArea.bounds.center;
        Vector2 inward = (center - closest).sqrMagnitude > 1e-8f ? (center - closest).normalized : Vector2.zero;
        return closest + inward * 0.1f;
    }

    private void FixedUpdate()
    {
        AdjustFlipByX();
        Move();
    }

    private void Move()
    {
        if (IsMovementLocked) return;

        float yMul = (combat != null && combat.IsInCombat) ? combat.CombatYSpeedMul : 1f;
        float vx = movement.x * moveSpeed * guardSpeedScale;
        float vy = movement.y * moveSpeed * guardSpeedScale * yMul;
        rb.linearVelocity = new Vector2(vx, vy); // ✅ 표준 속성
    }

    private void AdjustFlipByX()
    {
        if (!sprite || flipFromMovementBlocked) return;
        float x = movement.x;
        if (Mathf.Abs(x) < flipDeadzone) return;
        sprite.flipX = (x < 0f);
    }

    // ===== 외부 제어 API =====
    public void AddMovementLock(string key, bool hardFreezePhysics = false, bool zeroVelocity = true)
    {
        if (string.IsNullOrEmpty(key)) key = "__ANON__";
        bool firstLock = moveLocks.Count == 0;

        if (moveLocks.Add(key))
        {
            if (zeroVelocity)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            if (hardFreezePhysics)
            {
                // 첫 잠금 시점의 제약을 저장해 두고 Freeze
                if (firstLock)
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
        if (!moveLocks.Remove(key)) return;

        //  마지막 락이 해제되는 순간, 누가 해제하든 무조건 제약 복구
        if (moveLocks.Count == 0)
        {
            rb.constraints = constraintsBeforeLock;
        }
    }

    // 기존 API는 레거시 키로 래핑하여 하위호환 유지
    public void SetMovementLocked(bool locked, bool hardFreezePhysics = true, bool zeroVelocity = true)
    {
        if (locked) AddMovementLock(LegacyLock, hardFreezePhysics, zeroVelocity);
        else RemoveMovementLock(LegacyLock, hardFreezePhysics);
    }

    public void SetGuardSpeedScale(float scale) => guardSpeedScale = Mathf.Max(0f, scale);

    public void SetFlipFromMovementBlocked(bool blocked) => flipFromMovementBlocked = blocked;

    public void FaceTargetX(float targetX)
    {
        if (!sprite) return;
        bool left = targetX < transform.position.x;
        sprite.flipX = left;
        LastFacing = new Vector2(left ? -1f : 1f, 0f);
    }
}
