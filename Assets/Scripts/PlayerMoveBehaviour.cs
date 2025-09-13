using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMoveBehaviour: MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private float moveSpeed = 1.0f;

    private PlayerMove playermoves;
    private Vector2 movement;
    private Rigidbody2D rb;
    private void Awake()
    {
        playermoves = new PlayerMove();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        playermoves.Enable();
    }
    private void Update()
    {
        PlayerInput();
    }
    private void FixedUpdate()
    {
        Move();
    }
    private void PlayerInput()
    {
        movement = playermoves.Movement.Move.ReadValue<Vector2>();
    }
    private void Move()
    {
        rb.MovePosition(rb.position + movement *(moveSpeed * Time.fixedDeltaTime));
    }
}
