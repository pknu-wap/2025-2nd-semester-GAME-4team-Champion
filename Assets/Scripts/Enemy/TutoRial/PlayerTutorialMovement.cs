using UnityEngine;

public class PlayerTutorialMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public BoxCollider2D movementArea;
    private Rigidbody2D rb;

    private Vector2 input;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
    }

    void FixedUpdate()
    {
        Vector2 newPos = rb.position + input * moveSpeed * Time.fixedDeltaTime;

        if (movementArea != null)
        {
            Bounds bounds = movementArea.bounds;

            float clampedX = Mathf.Clamp(newPos.x, bounds.min.x, bounds.max.x);
            float clampedY = Mathf.Clamp(newPos.y, bounds.min.y, bounds.max.y);

            newPos = new Vector2(clampedX, clampedY);
        }

        rb.MovePosition(newPos);
    }
}
