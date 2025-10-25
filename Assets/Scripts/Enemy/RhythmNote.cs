using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    public int NoteType;
    public bool CanBeHit = false;
    public bool IsShrinking { get; private set; } = false;

    private bool reachedTarget = false;
    private Vector3 targetPos;
    private float speed;

    private Transform chargeBar;

    public void Initialize(Vector3 target, float moveSpeed, int noteType)
    {
        targetPos = target;
        speed = moveSpeed;
        NoteType = noteType;

        if (noteType == 3)
            chargeBar = transform.Find("Charge");
    }

    void Update()
    {
        if (!IsShrinking)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            if (!reachedTarget && Vector3.Distance(transform.position, targetPos) < 0.05f)
            {
                reachedTarget = true;
                Destroy(gameObject);
            }
        }

        if (IsShrinking && NoteType == 3 && chargeBar != null)
        {
            Vector3 scale = chargeBar.localScale;
            scale.x = Mathf.Lerp(scale.x, 0f, 2.5f * Time.deltaTime);
            chargeBar.localScale = scale;

            if (scale.x < 0.05f)
                Destroy(gameObject);
        }
    }

    public void StartShrink()
    {
        if (!IsShrinking && NoteType == 3)
            IsShrinking = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("JudgeLine"))
            CanBeHit = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("JudgeLine") && NoteType != 3)
            CanBeHit = false;
    }
}
